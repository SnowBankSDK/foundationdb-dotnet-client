#!/usr/bin/env bash
#
# DownloadBinaries.sh - macOS/Linux companion to DownloadBinaries.ps1
#
# Downloads the native FoundationDB client (and, with --full, the fdbcli) binaries listed in
# manifest.json into runtimes/<rid>/native/, skipping any file already present with the correct
# SHA-256 checksum. Uses curl for a fast native download with a progress bar.
#
# Usage:
#   ./DownloadBinaries.sh [--version <v>] [--manifest <path>] [--output <dir>]
#                         [--rid <rid>] [--full] [--offline] [--force]
#
#   --version <v>    Version to fetch, or "latest" (default: the manifest's "latest").
#   --manifest <p>   Path to manifest.json (default: next to this script).
#   --output <dir>   Root under which runtimes/<rid>/native/ is created (default: this script's dir).
#   --rid <rid>      Only fetch this runtime id (e.g. osx-arm64, linux-x64). Repeatable. Default: all.
#   --full           Also fetch the fdbcli binaries (skipped by default).
#   --offline        Report cache status but never download.
#   --force          Re-download even when the local file already matches the checksum.
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

version="latest"
manifest="${script_dir}/manifest.json"
output="${script_dir}"
full=0
offline=0
force=0
rids=""

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
	case "$1" in
		--version|-version)   version="${2:?--version needs a value}"; shift 2;;
		--version=*)          version="${1#*=}"; shift;;
		--manifest|-manifest) manifest="${2:?--manifest needs a value}"; shift 2;;
		--manifest=*)         manifest="${1#*=}"; shift;;
		--output|-output|--outputDir) output="${2:?--output needs a value}"; shift 2;;
		--output=*)           output="${1#*=}"; shift;;
		--rid|-rid)           rids="${rids} ${2:?--rid needs a value}"; shift 2;;
		--rid=*)              rids="${rids} ${1#*=}"; shift;;
		--full|-full)         full=1; shift;;
		--offline|-offline)   offline=1; shift;;
		--force|-force)       force=1; shift;;
		-h|--help)            sed -n '2,25p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0;;
		*)                    die "unknown argument: $1 (see --help)";;
	esac
done

command -v curl    >/dev/null 2>&1 || die "curl is required but was not found on PATH."
command -v python3 >/dev/null 2>&1 || die "python3 is required (to parse manifest.json) but was not found on PATH."
[ -f "$manifest" ] || die "manifest not found: $manifest"

# pick a SHA-256 tool (macOS ships 'shasum', most Linux ship 'sha256sum')
if command -v sha256sum >/dev/null 2>&1; then
	sha256_of() { sha256sum "$1" | awk '{print $1}'; }
elif command -v shasum >/dev/null 2>&1; then
	sha256_of() { shasum -a 256 "$1" | awk '{print $1}'; }
else
	die "neither sha256sum nor shasum found; cannot verify checksums."
fi

# colors, only when writing to a terminal
if [ -t 1 ]; then
	c_dim=$'\033[2m'; c_yellow=$'\033[33m'; c_green=$'\033[32m'; c_red=$'\033[31m'; c_cyan=$'\033[36m'; c_reset=$'\033[0m'
else
	c_dim=""; c_yellow=""; c_green=""; c_red=""; c_cyan=""; c_reset=""
fi

# Resolve the version and emit one "rid<TAB>name<TAB>url<TAB>checksum" line per selected file.
# python3 does the JSON parsing; the download/verify loop below stays in portable shell.
parsed="$(python3 - "$manifest" "$version" "$full" $rids <<'PY'
import json, sys
manifest_path, version, full = sys.argv[1], sys.argv[2], sys.argv[3] == "1"
rid_filter = set(sys.argv[4:])
with open(manifest_path, encoding="utf-8") as f:
	m = json.load(f)
if not version or version == "latest":
	version = m.get("latest")
versions = m.get("versions", {})
if version not in versions:
	sys.stderr.write("version '%s' does not exist in the manifest\n" % version)
	sys.exit(1)
print("VERSION\t%s" % version)
for f in versions[version].get("files", []):
	name = f.get("name", ""); rid = f.get("rid", ""); url = f.get("url", ""); checksum = (f.get("checksum", "") or "").lower()
	if not full and "fdbcli" in name:
		continue
	if rid_filter and rid not in rid_filter:
		continue
	print("FILE\t%s\t%s\t%s\t%s" % (rid, name, url, checksum))
PY
)" || die "failed to read manifest.json"

resolved_version=""
files=()
while IFS=$'\t' read -r tag a b c d; do
	case "$tag" in
		VERSION) resolved_version="$a";;
		FILE)    files+=("${a}"$'\t'"${b}"$'\t'"${c}"$'\t'"${d}");;
	esac
done <<< "$parsed"

[ "${#files[@]}" -gt 0 ] || die "no matching files for version '${resolved_version}'${rids:+ and rid(s)${rids}}."

printf '%sDownloading %d file(s) for %s%s\n' "$c_reset" "${#files[@]}" "$resolved_version" "$c_reset"

downloaded=0; cached=0; skipped=0
for entry in "${files[@]}"; do
	IFS=$'\t' read -r rid name url checksum <<< "$entry"

	target_dir="${output}/runtimes/${rid}/native"
	target="${target_dir}/${name}"

	printf '\n%s- %s (%s)%s\n' "$c_yellow" "$name" "$rid" "$c_reset"
	printf '  %starget  :%s %s\n' "$c_dim" "$c_reset" "$target"
	printf '  %surl     :%s %s\n' "$c_dim" "$c_reset" "$url"
	printf '  %schecksum:%s %s\n' "$c_dim" "$c_reset" "$checksum"

	# already present with the right checksum? skip unless --force
	if [ -f "$target" ]; then
		local_sum="$(sha256_of "$target")"
		if [ "$local_sum" = "$checksum" ]; then
			if [ "$force" -eq 1 ]; then
				printf '  %s=> re-downloading (--force)%s\n' "$c_yellow" "$c_reset"
			else
				printf '  %s=> CACHED%s\n' "$c_green" "$c_reset"
				cached=$((cached + 1)); continue
			fi
		else
			printf '  %slocal   :%s %s\n' "$c_dim" "$c_reset" "$local_sum"
			printf '  %s=> checksum mismatch, re-downloading%s\n' "$c_red" "$c_reset"
		fi
	fi

	if [ "$offline" -eq 1 ]; then
		printf '  %s=> SKIPPING (offline)%s\n' "$c_cyan" "$c_reset"
		skipped=$((skipped + 1)); continue
	fi

	mkdir -p "$target_dir"
	# download to a temp file first so an interrupted transfer never leaves a corrupt target
	tmp="${target}.download"
	rm -f "$tmp"
	if ! curl --fail --location --progress-bar --output "$tmp" "$url"; then
		rm -f "$tmp"
		die "download failed: $url"
	fi

	# verify before publishing
	got="$(sha256_of "$tmp")"
	if [ "$got" != "$checksum" ]; then
		rm -f "$tmp"
		die "checksum verification failed for ${target} (expected ${checksum}, got ${got})"
	fi
	mv -f "$tmp" "$target"
	printf '  %s=> OK%s\n' "$c_green" "$c_reset"
	downloaded=$((downloaded + 1))
done

# Record the version runtimes/ now holds. The csproj refuses to pack when this stamp differs from the package version.
if [ "$offline" -eq 0 ]; then
	mkdir -p "$output/runtimes"
	printf '%s\n' "$resolved_version" > "$output/runtimes/fdb-native-version.txt"
	printf '%sStamped runtimes/fdb-native-version.txt = %s%s\n' "$c_dim" "$resolved_version" "$c_reset"
fi

printf '\n%sDone.%s downloaded=%d cached=%d skipped=%d\n' "$c_green" "$c_reset" "$downloaded" "$cached" "$skipped"
