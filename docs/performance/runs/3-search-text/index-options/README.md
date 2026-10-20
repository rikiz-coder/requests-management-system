# Search: which index for the SearchText column (experiments before the implementation)

Run with `scripts/perf/measure.py --experiments-only` on a GitHub Actions runner, 100,000 requests.
Each case = the COUNT + the first page (20 rows, newest first) that the API runs. Reads are logical reads (8 KB pages);
CPU is the median over 5 runs. Plan cache cleared before each case. The `.sqlplan` files of round 2 are in `plans/`.

Terms: frequent = "היתר" (~6,700 matches), rare = "רימון לוגיסטיקה" (one organization, ~200 matches),
none = no match at all (the API skips the page query in that case, measured anyway as a worst case).

## Round 1 – separate index on SearchText (the original proposal)

| Case | Search before | SearchText + `IX (SearchText)` | SearchText + `IX (SearchText) INCLUDE (CreatedAt)` |
|---|---:|---:|---:|
| frequent | 2,438 reads · 268 ms | 1,857 · 20 ms | 1,945 · 19 ms |
| rare | 51,254 · 493 ms | 44,848 · 45 ms | 44,937 · 47 ms |
| none | 315,964 · 856 ms | 307,718 · 193 ms | 307,806 · 197 ms |

The COUNT was cheap, but the page query kept the same plan in every variant: read `IX_Requests_CreatedAt_Id` in list order
and look up every row in the table to check the text, until 20 matches are found. For a rare term that is
tens of thousands of lookups – also in the search before the change.

## Round 2 – where the page query can check the text without lookups

| Case | `IX_Requests_CreatedAt_Id INCLUDE (SearchText)` | `IX (SearchText) INCLUDE (CreatedAt)` + no row goal hint | both |
|---|---:|---:|---:|
| frequent | **1,395 · 14 ms** | 12,777 · 70 ms | 1,411 · 13 ms |
| rare | **1,582 · 18 ms** | 14,015 · 68 ms | 1,595 · 16 ms |
| none | **2,640 · 26 ms** | 16,676 · 66 ms | 2,652 · 26 ms |
| status Waiting + rare | **2,702 · 28 ms** | 2,842 · 32 ms | 2,718 · 27 ms |

**Chosen:** include SearchText in the existing `IX_Requests_CreatedAt_Id`. The page query reads that index in list order
and checks the text inside the index (no lookups), and the COUNT scans the same index. One index fewer than the
alternatives, no query hints, and the best or equal result in every case.

## Persisted or not

A first full run with the column `PERSISTED` (stored in the table) showed every full table scan growing from 1,916 to
3,070 reads (+60%), because the table stored a copy of the title and the organization. The final version is
not persisted: the value is stored only in `IX_Requests_CreatedAt_Id`, the one place that reads it. Results: [`../results`](../results/summary.md).
