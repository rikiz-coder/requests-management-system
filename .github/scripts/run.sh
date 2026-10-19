#!/usr/bin/env bash
# Runs a command; if it fails, re-emits its error lines as GitHub annotations so they are visible
# in the run summary and through the checks API (not only in the full log).
set -o pipefail
log=$(mktemp)
"$@" 2>&1 | tee "$log"
status=${PIPESTATUS[0]}
if [ "$status" -ne 0 ]; then
  grep -E "error|Error|FAIL|Failed|Assert|Exception|Msg [0-9]+|Traceback|line [0-9]+" "$log" | grep -v "^::" | sort -u | head -40 | while IFS= read -r line; do
    echo "::error::${line:0:900}"
  done
fi
exit "$status"
