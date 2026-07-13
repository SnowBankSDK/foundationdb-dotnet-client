#!/usr/bin/env bash
#
# Runs an ISOLATED (standalone) restore + clean + build of the SnowBank / FoundationDB solution.
#
# Forces CORESDK_STANDALONE_BUILD=true so the repo builds its OWN complete target set
# (net8.0/net10.0/net11.0 + netstandard2.0, plus the net472 validation targets) even when it is
# checked out as a submodule under a parent that trims the target frameworks. Use this to develop or
# validate the submodule on its own.
#
# CORESDK_STANDALONE_BUILD is passed as an MSBuild -p: property, not exported, so it stays scoped to
# these invocations and cannot leak into the caller's shell (a persistent export, if the script were
# sourced, would silently force every later dotnet command into a standalone build).
#
# CAUTION, shared restore state: the restore step rewrites artifacts/obj/**/project.assets.json for
# the STANDALONE (superset) target set. After this runs, the FoundationDB submodule's assets no
# longer match the PARENT repo's trimmed set, so the next build FROM THE PARENT must re-run
# 'dotnet restore' there first, or it fails with NETSDK1005 (assets file has no target for the
# parent's TFM). A reminder is printed at the end.
#
# Usage:  ./scripts/build.sh [Debug|Release]
#
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(dirname "$here")"
solution="$repo/FoundationDB.Client.slnx"
config="${1:-Debug}"
standalone="-p:CORESDK_STANDALONE_BUILD=true"

echo "Isolated STANDALONE build of $solution ($config)"
echo "Complete target set: net8.0/net10.0/net11.0 + netstandard2.0 + net472"
echo

echo "[1/3] restore (standalone target set)..."
dotnet restore "$solution" $standalone

echo
echo "[2/3] clean..."
dotnet clean "$solution" $standalone -c "$config"

echo
echo "[3/3] build..."
dotnet build "$solution" $standalone -c "$config" --no-restore

echo
echo "Standalone build complete."
echo "NOTE: artifacts/obj now holds STANDALONE restore assets (net8.0 + netstandard2.0 + net472 included)."
echo "      Before building the PARENT repo again, re-run 'dotnet restore' there so the FoundationDB"
echo "      submodule's assets match the parent's trimmed target set, otherwise it fails with NETSDK1005."
