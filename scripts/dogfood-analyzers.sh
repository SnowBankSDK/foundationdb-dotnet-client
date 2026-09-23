#!/usr/bin/env bash
# Builds the solution with the SnowBank.Core and FoundationDB.Client analyzers attached to the test and sample projects,
# and lists their diagnostics. Exit code 1 when the build fails or reports any SBK or FDB diagnostic.
set -uo pipefail
cd "$(dirname "$0")/.."
log=$(mktemp)
dotnet build FoundationDB.Client.slnx -c Debug -p:DogfoodAnalyzers=true -nologo -v:minimal --no-incremental > "$log" 2>&1
status=$?
grep -oE '[^ ]+\([0-9]+,[0-9]+\): (warning|error) (SBK|FDB)[0-9]{4}: .*' "$log" | sed 's/ \[[^]]*\]$//' | sort -u > "$log.hits"
count=$(wc -l < "$log.hits")
cat "$log.hits"
echo "$count analyzer diagnostics"
if [ $status != 0 ]; then echo "build failed, log: $log"; grep -E ' error ' "$log" | grep -vE ' (SBK|FDB)[0-9]{4}:' | sort -u | head -20; exit 1; fi
[ "$count" = 0 ]
