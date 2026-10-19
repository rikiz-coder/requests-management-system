#!/usr/bin/env python3
"""Extracts the SQL that EF Core actually executed from `docker compose logs api` (EF command logging on)."""
import re
import sys

text = sys.stdin.read()
lines = [re.sub(r"^\S+\s*\|\s?", "", l) for l in text.splitlines()]  # strip "api-1  | "
entries, current = [], None
for line in lines:
    if re.match(r"^(info|warn|fail|dbug|trce|crit): ", line):
        if current:
            entries.append(current)
        current = [line] if "Executed DbCommand" in line or "Microsoft.EntityFrameworkCore.Database.Command" in line else None
    elif current is not None:
        current.append(line)
if current:
    entries.append(current)

seen = set()
for e in entries:
    body = "\n".join(l.strip() for l in e[1:] if l.strip())
    key = re.sub(r"Executed DbCommand \(\d+ms\)", "", body)
    if "Executed DbCommand" not in body or key in seen:
        continue
    seen.add(key)
    print(body)
    print("-" * 100)
