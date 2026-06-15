#!/usr/bin/env bash
#
# Sync the canonical Agent Skills (.claude/skills) into the plugin's skills/ folder.
#
# Why: the plugin copy under plugins/foundationdb-skills/skills/ MUST be committed (the plugin
# marketplace reads the repo at a given commit — there is no build step on the consumer's side),
# but .claude/skills/ is the single source of truth (it is what auto-loads for agents working IN
# this repo). A committed symlink would not resolve reliably across Windows/macOS/Linux checkouts,
# so we keep a real copy and keep it in sync with this script.
#
# Usage:
#   scripts/sync-plugin-skills.sh           # copy .claude/skills -> plugin (run after editing skills)
#   scripts/sync-plugin-skills.sh --check   # verify they are in sync (exit 1 if drifted) — for CI
#
# Windows: use the PowerShell equivalent scripts/sync-plugin-skills.ps1 (or run this from Git Bash).

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/.claude/skills"
DST="$ROOT/plugins/foundationdb-skills/skills"

if [[ "${1:-}" == "--check" ]]; then
	if diff -r "$SRC" "$DST" >/dev/null 2>&1; then
		echo "✓ plugin skills are in sync with .claude/skills"
	else
		echo "ERROR: plugins/foundationdb-skills/skills is out of sync with .claude/skills." >&2
		echo "       Run: scripts/sync-plugin-skills.sh" >&2
		diff -r "$SRC" "$DST" || true
		exit 1
	fi
else
	rm -rf "$DST"
	mkdir -p "$DST"
	cp -R "$SRC/." "$DST/"
	echo "✓ synced .claude/skills -> plugins/foundationdb-skills/skills"
fi
