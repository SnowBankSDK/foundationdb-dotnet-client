#!/usr/bin/env bash
#
# Release pre-flight: check (and optionally patch) the "latest known FoundationDB docker image" tags.
#
# FdbAspireHostingExtensions.cs hard-codes four constants (LatestVersion74/73/72/71) that the Latest*
# roll-forward policies resolve to when an app asks for the "latest" fdb image. They are maintained by
# hand and drift as FoundationDB publishes new images. This script queries the Docker Hub tags API for
# each 7.x branch, computes the newest usable tag, and compares it to what the source currently pins.
#
# AVX rule: the 7.3, 7.2 and 7.1 lines ship each build as an even/odd pair of the SAME code. The EVEN
# patch is built WITHOUT AVX and runs everywhere (x64, and ARM64 / Apple Silicon laptops under Docker
# emulation); the ODD patch enables AVX and is x64-only. For those lines the script keeps the highest
# EVEN tag. The 7.4 line stopped pairing after 7.4.5: every later 7.4 image is AVX and is published for
# amd64 and arm64, so 7.4 takes the highest tag regardless of parity.
#
# Usage:
#   ./scripts/check-fdb-image-tags.sh          # report only (default); exit 1 if any constant is stale
#   ./scripts/check-fdb-image-tags.sh --fix    # patch the stale constants in place, then report
#
# Note: uses page_size=100 ordered by last_updated (newest first), so the latest usable tag is on page 1.
# That holds comfortably for the current release cadence; revisit if a branch ever gets 100+ newer tags.
#
# Dependencies: curl + coreutils (grep/sed/awk/sort). No jq required.
#
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(dirname "$here")"
source_file="$repo/FoundationDB.Aspire.Hosting/FdbAspireHostingExtensions.cs"

fix=0
[[ "${1:-}" == "--fix" ]] && fix=1

if [[ ! -f "$source_file" ]]; then
    echo "Source file not found: $source_file" >&2
    exit 2
fi

# maj min suffix even_only (1 = the line still ships AVX pairs, keep the even patch)
branches=("7 4 74 0" "7 3 73 1" "7 2 72 1" "7 1 71 1")

failed=0
stale_count=0
stale_specs=()   # "suffix maj min newbuild current latest" for --fix

printf '%-16s %-9s %-13s %s\n' "Constant" "Current" "Latest" "Status"
printf '%-16s %-9s %-13s %s\n' "--------" "-------" "------" "------"

for b in "${branches[@]}"; do
    read -r maj min suffix even_only <<< "$b"
    prefix="$maj.$min"

    # current pinned build, parsed straight from the source constant
    current="$(sed -nE "s/.*LatestVersion${suffix}[[:space:]]*=[[:space:]]*new Version\([[:space:]]*${maj}[[:space:]]*,[[:space:]]*${min}[[:space:]]*,[[:space:]]*([0-9]+)[[:space:]]*\).*/\1/p" "$source_file" | head -1)"

    # newest usable tag from Docker Hub (even patch only where the line still ships AVX pairs)
    latest=""
    url="https://registry.hub.docker.com/v2/repositories/foundationdb/foundationdb/tags?name=${prefix}&page_size=100"
    if json="$(curl -fsSL "$url" 2>/dev/null)"; then
        latest="$(echo "$json" \
            | grep -oE '"name":"[^"]*"' \
            | sed -E 's/"name":"([^"]*)"/\1/' \
            | grep -E "^${maj}\.${min}\.[0-9]+$" \
            | awk -F. -v even_only="$even_only" 'even_only == 0 || $3 % 2 == 0' \
            | sort -V \
            | tail -1 || true)"
    else
        failed=1
    fi

    if [[ -z "$latest" ]]; then
        status="QUERY FAILED"
        [[ "$failed" -eq 0 ]] && failed=1
        current_disp="${current:+$prefix.$current}"; current_disp="${current_disp:-?}"
        printf '%-16s %-9s %-13s %s\n' "LatestVersion${suffix}" "$current_disp" "?" "$status"
        continue
    fi

    newbuild="${latest##*.}"

    if [[ -z "$current" ]]; then
        status="CONST NOT FOUND"
    elif [[ "$current" == "$newbuild" ]]; then
        status="ok"
    else
        status="STALE"
        stale_count=$((stale_count + 1))
        stale_specs+=("$suffix $maj $min $newbuild $current $latest")
    fi

    current_disp="${current:+$prefix.$current}"; current_disp="${current_disp:-?}"
    printf '%-16s %-9s %-13s %s\n' "LatestVersion${suffix}" "$current_disp" "$latest" "$status"
done

echo

if [[ "$stale_count" -eq 0 ]]; then
    if [[ "$failed" -ne 0 ]]; then
        echo "One or more branches could not be queried; see above."
        exit 2
    fi
    echo "All four constants are current."
    exit 0
fi

if [[ "$fix" -eq 0 ]]; then
    echo "$stale_count constant(s) STALE. Re-run with --fix to patch them in place, or edit $source_file by hand."
    exit 1
fi

tmp="$(mktemp)"
for spec in "${stale_specs[@]}"; do
    read -r suffix maj min newbuild current latest <<< "$spec"
    sed -E "s/(LatestVersion${suffix}[[:space:]]*=[[:space:]]*new Version\([[:space:]]*${maj}[[:space:]]*,[[:space:]]*${min}[[:space:]]*,[[:space:]]*)[0-9]+([[:space:]]*\))/\1${newbuild}\2/" "$source_file" > "$tmp"
    mv "$tmp" "$source_file"
    echo "Patched LatestVersion${suffix}: $maj.$min.$current -> $latest"
done

echo
echo "Patched $source_file. Review the diff and commit (submodule commit, no Co-Authored-By trailer)."
exit 0
