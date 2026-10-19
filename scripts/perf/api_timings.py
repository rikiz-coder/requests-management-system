#!/usr/bin/env python3
"""Measures API response times against the running system: 3 warm-up calls, then N timed calls per endpoint."""
import statistics
import sys
import time
import urllib.parse
import urllib.request

BASE = "http://localhost:5080"
RUNS = int(sys.argv[1]) if len(sys.argv) > 1 else 20

ENDPOINTS = [
    ("List, no filter, page 1", "/api/requests"),
    ("List, one status", "/api/requests?status=Waiting"),
    ("List, 2 statuses + priority + date", "/api/requests?status=InProgress&status=Waiting&priority=High&createdFrom=2025-06-01"),
    ("List, handler", "/api/requests?assignedTo=" + urllib.parse.quote("דוד פרידמן")),
    ("List, organization prefix", "/api/requests?organizationName=" + urllib.parse.quote("כרמל")),
    ("List, free-text search", "/api/requests?search=" + urllib.parse.quote("היתר")),
    ("List, page 500 sorted by title", "/api/requests?page=500&pageSize=100&sortBy=title&sortDirection=asc"),
    ("Summary, no filter (cache)", "/api/requests/summary"),
    ("Summary, priority High", "/api/requests/summary?priority=High"),
    ("Request details", "/api/requests/50000"),
    ("History", "/api/requests/50000/history"),
    ("Lookups", "/api/lookups"),
]


def call(path):
    start = time.perf_counter()
    with urllib.request.urlopen(BASE + path) as response:
        response.read()
        assert response.status == 200, path
    return (time.perf_counter() - start) * 1000


print("| Endpoint | Median ms | p95 ms |")
print("|---|---:|---:|")
for name, path in ENDPOINTS:
    for _ in range(3):
        call(path)
    times = sorted(call(path) for _ in range(RUNS))
    p95 = times[max(0, int(len(times) * 0.95) - 1)]
    print(f"| {name} (`{urllib.parse.unquote(path)}`) | {statistics.median(times):.1f} | {p95:.1f} |")
