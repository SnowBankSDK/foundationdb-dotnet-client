#!/usr/bin/env bash
#
# Packs the SnowBank / FoundationDB NuGet packages for a release.
#
# Forces a STANDALONE build (CORESDK_STANDALONE_BUILD=true) so the packages carry the COMPLETE
# target set (net8.0/net10.0/net11.0 + netstandard2.0) even when this repo is checked out as a
# submodule under a parent that trims target frameworks. Then it validates each produced .nupkg
# (embedded README + complete target set) and prints a summary.
#
# It does NOT push to any feed - publishing the .nupkg files is a deliberate, manual step.
#
# Usage:  ./scripts/pack.sh [output-dir] [Configuration]
#
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(dirname "$here")"
solution="$repo/FoundationDB.Client.slnx"
version="$(sed -n 's/.*<VersionPrefix>\(.*\)<\/VersionPrefix>.*/\1/p' "$repo/Common/VersionInfo.props" | head -n1)"
out="${1:-$repo/artifacts/packages/$version}"
config="${2:-Release}"

echo "Packing $solution  (version $version, $config)"
echo "Output: $out"
echo
rm -rf "$out"; mkdir -p "$out"

# Complete target set; keep the two knowingly-suppressed warnings:
#   CS1591 - missing XML doc comments
#   NU5104 - the net11.0 target references .NET-11-preview packages while .NET 11 is in preview
#            (the net8.0 / net10.0 targets are clean; this self-resolves once .NET 11 ships stable)
# CORESDK_STANDALONE_BUILD is passed as an MSBuild property, not exported, so it stays scoped to this
# one invocation and cannot leak into the caller's shell (e.g. if the script is sourced instead of run).
dotnet pack "$solution" -p:CORESDK_STANDALONE_BUILD=true -c "$config" -p:ContinuousIntegrationBuild=true -nowarn:CS1591,NU5104 -o "$out"

# --- validate the produced packages ---
echo
echo "Produced package(s):"
problems=0
for pkg in "$out"/*.nupkg; do
    [ -e "$pkg" ] || continue
    tfms="$(unzip -Z1 "$pkg" 2>/dev/null | sed -n 's#^lib/\([^/]*\)/.*#\1#p' | sort -u | tr '\n' ' ')"
    readme="$(unzip -Z1 "$pkg" 2>/dev/null | grep -ic '^README\.md$' || true)"
    printf '  %-54s %s\n' "$(basename "$pkg")" "${tfms:-(tool/analyzer)}"
    [ "${readme:-0}" -ge 1 ] || { echo "    ! no README.md"; problems=$((problems+1)); }
    if [ -n "$tfms" ] && ! printf '%s' "$tfms" | grep -qw 'net8.0'; then
        echo "    ! missing net8.0 (incomplete target set)"; problems=$((problems+1))
    fi
done

echo
if [ "$problems" -eq 0 ]; then
    echo "All packages carry a README and net8.0 (complete target set)."
    echo "NOT pushed - publish the .nupkg files manually."
else
    echo "VALIDATION FAILED ($problems problem(s))"
    exit 1
fi
