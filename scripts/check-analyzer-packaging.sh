#!/usr/bin/env bash
# Checks that the SnowBank.Core and FoundationDB.Client packages embed their analyzers,
# and that FoundationDB.Client lets the SnowBank.Core analyzers flow to its consumers.
set -euo pipefail
dir="${1:?usage: check-analyzer-packaging.sh <folder with the .nupkg files>}"
fail=0

check_dll() { # package, dll
	if ! unzip -l "$1" | grep -q "analyzers/dotnet/roslyn4.8/cs/$2"; then echo "MISSING $2 in $(basename "$1")"; fail=1; fi
}

core=$(ls "$dir"/SnowBank.Core.[0-9]*.nupkg | head -1)
client=$(ls "$dir"/FoundationDB.Client.[0-9]*.nupkg | head -1)

check_dll "$core" SnowBank.Core.Analyzers.dll
check_dll "$core" SnowBank.Core.CodeFixes.dll
check_dll "$client" FoundationDB.Client.Analyzers.dll
check_dll "$client" FoundationDB.Client.CodeFixes.dll

for pkg in "$core" "$client"; do
	if unzip -l "$pkg" | grep -qE ' (build|buildTransitive)/'; then echo "UNEXPECTED build asset in $(basename "$pkg")"; fail=1; fi
done

deps=$(unzip -p "$client" '*.nuspec' | grep 'id="SnowBank.Core"' || true)
echo "$deps"
if echo "$deps" | grep -q 'Analyzers'; then echo "the SnowBank.Core dependency still excludes Analyzers"; fail=1; fi
groups=$(unzip -p "$client" '*.nuspec' | grep -c '<group ' || true)
lines=$(echo "$deps" | grep -c 'SnowBank.Core' || true)
if [ "$groups" != "$lines" ]; then echo "expected one SnowBank.Core dependency per group: $groups groups, $lines lines"; fail=1; fi

if [ $fail = 0 ]; then echo "analyzer packaging OK"; fi
exit $fail
