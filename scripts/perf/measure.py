#!/usr/bin/env python3
"""
Runs the scenarios of docs/performance/measure.sql against the SQL Server of docker compose and records,
for every scenario: logical reads (STATISTICS IO), CPU time and elapsed time (median of several warm
runs), and the actual execution plans.

Every scenario is measured:
  * with all indexes ("indexed"),
  * with no nonclustered index at all ("no-indexes"),
  * once per index with only that index disabled ("without:<index>") - what each index is worth,
  * plus experiments that try improvements for the bottleneck.

Output: docs/performance/results/measurements.json, results/summary.md and plans/*.sqlplan.

Usage (from the repository root, with `docker compose up` running and the database seeded):
    python3 scripts/perf/measure.py
"""
import argparse
import json
import re
import statistics
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCENARIO_FILE = ROOT / "docs" / "performance" / "measure.sql"
RESULTS_DIR = ROOT / "docs" / "performance" / "results"
PLANS_DIR = ROOT / "docs" / "performance" / "plans"

SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"


def sqlcmd(sql: str, password: str) -> str:
    """Runs a script inside the sqlserver container and returns its output (UTF-8)."""
    cmd = ["docker", "compose", "exec", "-T", "sqlserver", SQLCMD, "-S", "localhost", "-U", "sa", "-P", password,
           "-C", "-I", "-d", "RequestsManagement", "-b", "-y", "0", "-f", "65001", "-i", "/dev/stdin"]
    result = subprocess.run(cmd, input=sql.encode("utf-8"), capture_output=True, cwd=ROOT)
    out = result.stdout.decode("utf-8", errors="replace")
    if result.returncode != 0:
        details = (out + result.stderr.decode("utf-8", errors="replace")).strip().splitlines()
        for line in details[-15:]:
            print(f"::error::sqlcmd: {line[:500]}")  # visible as a GitHub annotation
        raise RuntimeError(f"sqlcmd failed ({result.returncode})")
    return out


def load_scenarios():
    """Splits measure.sql into scenarios: '-- @scenario name: description' ... 'GO'."""
    scenarios, current = [], None
    for line in SCENARIO_FILE.read_text(encoding="utf-8").splitlines():
        header = re.match(r"^-- @scenario ([\w-]+): (.*)$", line)
        if header:
            current = {"name": header.group(1), "description": header.group(2).strip(), "sql": []}
            scenarios.append(current)
        elif line.strip().upper() == "GO":
            current = None
        elif current is not None:
            current["sql"].append(line)
    for s in scenarios:
        s["sql"] = "\n".join(s["sql"]).strip()
    return scenarios


def measure(sql_body: str, password: str, runs: int, capture_plans: bool):
    """Warm-up run, then `runs` timed runs; IO statistics and plans are taken from the last run."""
    parts = ["SET NOCOUNT ON;",
             "IF OBJECT_ID('tempdb..#m') IS NOT NULL DROP TABLE #m;",
             "CREATE TABLE #m (run int, phase char(1), t datetime2(7), cpu int);",
             "GO", sql_body, "GO"]  # warm-up: plan compiled, pages in memory
    for i in range(1, runs + 1):
        last = i == runs
        parts += [f"INSERT #m SELECT {i}, 's', SYSDATETIME(), cpu_time FROM sys.dm_exec_sessions WHERE session_id = @@SPID;",
                  "GO"]
        if last:
            parts.append("SET STATISTICS IO ON;" + (" SET STATISTICS XML ON;" if capture_plans else ""))
        parts.append(sql_body)
        if last:
            parts.append(("SET STATISTICS XML OFF; " if capture_plans else "") + "SET STATISTICS IO OFF;")
        parts += ["GO",
                  f"INSERT #m SELECT {i}, 'e', SYSDATETIME(), cpu_time FROM sys.dm_exec_sessions WHERE session_id = @@SPID;",
                  "GO"]
    parts += ["SELECT 'RUN|' + CAST(s.run AS varchar(10)) + '|' + CAST(DATEDIFF(microsecond, s.t, e.t) AS varchar(20))"
              " + '|' + CAST(e.cpu - s.cpu AS varchar(20)) FROM #m s JOIN #m e ON e.run = s.run AND s.phase = 's' AND e.phase = 'e';",
              "GO"]
    out = sqlcmd("\n".join(parts), password)

    elapsed, cpu = [], []
    for m in re.finditer(r"RUN\|(\d+)\|(\d+)\|(-?\d+)", out):
        elapsed.append(int(m.group(2)) / 1000.0)
        cpu.append(int(m.group(3)))

    reads = {}
    for m in re.finditer(r"Table '([^']+)'\. Scan count (\d+), logical reads (\d+)", out):
        table = m.group(1)
        reads[table] = reads.get(table, 0) + int(m.group(3))

    plans = [line.strip() for line in out.splitlines() if line.strip().startswith("<ShowPlanXML")]
    return {
        "logicalReads": sum(reads.values()),
        "readsByTable": reads,
        "elapsedMsMedian": round(statistics.median(elapsed), 2) if elapsed else None,
        "cpuMsMedian": statistics.median(cpu) if cpu else None,
        "plans": plans,
    }


def plan_operators(plan_xml: str):
    """Physical operators on table/index access, for a quick textual summary of a plan."""
    ops = []
    for m in re.finditer(r'<RelOp [^>]*PhysicalOp="([^"]+)"[^>]*>', plan_xml):
        op = m.group(1)
        if op in ("Index Seek", "Index Scan", "Clustered Index Seek", "Clustered Index Scan", "Key Lookup",
                  "Sort", "Top N Sort", "Hash Match", "Table Scan"):
            ops.append(op)
    indexes = sorted(set(re.findall(r'Index="\[([^\]]+)\]"', plan_xml)))
    return {"operators": ops, "indexes": indexes}


def list_indexes(password: str):
    out = sqlcmd("SET NOCOUNT ON; SELECT 'IDX|' + OBJECT_NAME(object_id) + '|' + name FROM sys.indexes "
                 "WHERE object_id IN (OBJECT_ID('dbo.Requests'), OBJECT_ID('dbo.RequestStatusHistory')) "
                 "AND name LIKE 'IX[_]%' ORDER BY name;", password)
    return [tuple(m.groups()) for m in re.finditer(r"IDX\|(\w+)\|(\w+)", out)]


def set_indexes(indexes, enabled: bool, password: str):
    if not indexes:
        return
    action = "REBUILD" if enabled else "DISABLE"
    sqlcmd("\n".join(f"ALTER INDEX [{name}] ON dbo.[{table}] {action};" for table, name in indexes), password)


EXPERIMENTS = [
    {
        "name": "search-before-searchtext",
        "description": "The free-text search as it was before the SearchText column (LIKE on Title OR "
                       "OrganizationName, linguistic collation), for comparison with list-search",
        "setup": "DBCC FREEPROCCACHE;",
        "sql": """
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @term_contains ESCAPE N''\\'' OR [r].[OrganizationName] LIKE @term_contains ESCAPE N''\\''', N'@term_contains nvarchar(200)', @term_contains = N'%היתר%';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @term_contains ESCAPE N''\\'' OR [r].[OrganizationName] LIKE @term_contains ESCAPE N''\\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@term_contains nvarchar(200),@p int,@p0 int', @term_contains = N'%היתר%', @p = 0, @p0 = 20;
""",
        "teardown": "SELECT 1;",
    },
    {
        "name": "search-before-searchtext-rare",
        "description": "The free-text search as it was before the SearchText column (LIKE on Title OR "
                       "OrganizationName, linguistic collation), for comparison with list-search-rare",
        "setup": "DBCC FREEPROCCACHE;",
        "sql": """
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @term_contains ESCAPE N''\\'' OR [r].[OrganizationName] LIKE @term_contains ESCAPE N''\\''', N'@term_contains nvarchar(200)', @term_contains = N'%רימון לוגיסטיקה%';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @term_contains ESCAPE N''\\'' OR [r].[OrganizationName] LIKE @term_contains ESCAPE N''\\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p0 ROWS ONLY', N'@term_contains nvarchar(200),@p int,@p0 int', @term_contains = N'%רימון לוגיסטיקה%', @p = 0, @p0 = 20;
""",
        "teardown": "SELECT 1;",
    },
    {
        "name": "deep-page-keyset",
        "description": "The same deep page (sorted by title) with keyset paging: continue after the last row "
                       "of the previous page instead of skipping 49,900 rows, with an index on (Title, Id)",
        "setup": """
CREATE INDEX IX_Requests_Title_Id ON dbo.Requests (Title, Id);
SELECT Title, Id INTO dbo.PerfKeysetStart FROM dbo.Requests ORDER BY Title, Id OFFSET 49899 ROWS FETCH NEXT 1 ROWS ONLY;
""",
        "sql": """
DECLARE @lastTitle nvarchar(200), @lastId int;
SELECT @lastTitle = Title, @lastId = Id FROM dbo.PerfKeysetStart;
EXEC sp_executesql N'SELECT TOP(@n) [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Title] > @lastTitle OR ([r].[Title] = @lastTitle AND [r].[Id] > @lastId)
ORDER BY [r].[Title], [r].[Id]', N'@n int,@lastTitle nvarchar(200),@lastId int', @n = 100, @lastTitle = @lastTitle, @lastId = @lastId;
""",
        "teardown": "DROP INDEX IX_Requests_Title_Id ON dbo.Requests; DROP TABLE dbo.PerfKeysetStart;",
    },
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--password", default="Requests_Dev_Passw0rd!")
    parser.add_argument("--runs", type=int, default=5)
    parser.add_argument("--quick", action="store_true", help="only the indexed and no-index modes")
    args = parser.parse_args()

    scenarios = load_scenarios()
    indexes = list_indexes(args.password)
    print(f"{len(scenarios)} scenarios, indexes: {', '.join(n for _, n in indexes)}")
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    PLANS_DIR.mkdir(parents=True, exist_ok=True)

    modes = [("indexed", [])] + [("no-indexes", indexes)]
    if not args.quick:
        modes += [(f"without:{name}", [(table, name)]) for table, name in indexes]

    results = {"scenarios": {s["name"]: s["description"] for s in scenarios}, "modes": {}, "experiments": {}}
    for mode, disabled in modes:
        print(f"== {mode}")
        set_indexes(disabled, enabled=False, password=args.password)
        try:
            capture = mode in ("indexed", "no-indexes")
            mode_results = {}
            for s in scenarios:
                r = measure(s["sql"], args.password, args.runs, capture)
                for i, plan in enumerate(r.pop("plans"), start=1):
                    (PLANS_DIR / f"{s['name']}.{mode}.{i}.sqlplan").write_text(plan, encoding="utf-8")
                    r.setdefault("planSummary", []).append(plan_operators(plan))
                mode_results[s["name"]] = r
                print(f"   {s['name']:<28} reads={r['logicalReads']:>7}  cpu={r['cpuMsMedian']}ms  elapsed={r['elapsedMsMedian']}ms")
            results["modes"][mode] = mode_results
        finally:
            set_indexes(disabled, enabled=True, password=args.password)

    for exp in EXPERIMENTS:
        print(f"== experiment {exp['name']}")
        sqlcmd(exp["setup"], args.password)
        try:
            r = measure(exp["sql"], args.password, args.runs, capture_plans=True)
            for i, plan in enumerate(r.pop("plans"), start=1):
                (PLANS_DIR / f"experiment.{exp['name']}.{i}.sqlplan").write_text(plan, encoding="utf-8")
                r.setdefault("planSummary", []).append(plan_operators(plan))
            r["description"] = exp["description"]
            results["experiments"][exp["name"]] = r
            print(f"   reads={r['logicalReads']}  cpu={r['cpuMsMedian']}ms  elapsed={r['elapsedMsMedian']}ms")
        finally:
            sqlcmd(exp["teardown"], args.password)

    (RESULTS_DIR / "measurements.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
    write_summary(results)


def write_summary(results):
    lines = ["# Measurements (generated by scripts/perf/measure.py)", "",
             "Logical reads (8 KB pages) · CPU ms (median) · elapsed ms (median, includes returning rows to sqlcmd).", ""]
    indexed, none_ = results["modes"]["indexed"], results["modes"]["no-indexes"]
    lines += ["| Scenario | Reads (indexed) | CPU | Elapsed | Reads (no indexes) | CPU | Elapsed | Plan (indexed) |",
              "|---|---:|---:|---:|---:|---:|---:|---|"]
    for name in results["scenarios"]:
        a, b = indexed[name], none_[name]
        plan = "; ".join(" → ".join(p["operators"]) + (f" [{', '.join(p['indexes'])}]" if p["indexes"] else "")
                         for p in a.get("planSummary", []))
        lines.append(f"| {name} | {a['logicalReads']:,} | {a['cpuMsMedian']} | {a['elapsedMsMedian']} | "
                     f"{b['logicalReads']:,} | {b['cpuMsMedian']} | {b['elapsedMsMedian']} | {plan} |")
    lines += ["", "## What each index is worth (reads when only that index is disabled)", "",
              "| Index | Scenario | Reads with | Reads without | CPU with | CPU without |", "|---|---|---:|---:|---:|---:|"]
    for mode, mres in results["modes"].items():
        if not mode.startswith("without:"):
            continue
        index = mode.split(":", 1)[1]
        for name, r in mres.items():
            base = indexed[name]
            if r["logicalReads"] > base["logicalReads"] * 1.2 + 5:
                lines.append(f"| {index} | {name} | {base['logicalReads']:,} | {r['logicalReads']:,} | "
                             f"{base['cpuMsMedian']} | {r['cpuMsMedian']} |")
    lines += ["", "## Experiments", ""]
    for name, r in results["experiments"].items():
        plan = "; ".join(" → ".join(p["operators"]) for p in r.get("planSummary", []))
        lines.append(f"* **{name}** – {r['description']}: reads {r['logicalReads']:,}, CPU {r['cpuMsMedian']} ms, "
                     f"elapsed {r['elapsedMsMedian']} ms. Plan: {plan}")
    (RESULTS_DIR / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
