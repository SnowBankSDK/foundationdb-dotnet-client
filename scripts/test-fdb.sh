#!/usr/bin/env bash
#
# Runs the real-cluster test suites with a chosen native client, server image and API version.
# macOS/Linux companion to test-fdb.ps1: same options, same test-matrix.json, same output folders.
#
# Build the test projects first (dotnet build): every run uses the same test assemblies, and only the native
# client, the server image and the API version change between runs.
#
# Each client version is cached under artifacts/native/<version>/, for the current platform only. DownloadBinaries.sh
# compares the cached files with the SHA-256 of manifest.json, and a file is downloaded only when it is missing or stale.
#
# Three environment variables set the combination of each run:
#   FDB_TEST_NATIVE_LIB_PATH   path of the native client library
#   FDB_TEST_DOCKER_TAG        tag of the foundationdb/foundationdb server image
#   FDB_TEST_API_VERSION       API version of the test process
#
# Usage:
#   ./scripts/test-fdb.sh                                           # default run of test-matrix.json, every project
#   ./scripts/test-fdb.sh --client 7.4                              # newest 7.4 client, 7.4 server, API 740
#   ./scripts/test-fdb.sh --client 8.0 --api 730 --project FoundationDB.Tests --filter "FullyQualifiedName~Tenant"
#   ./scripts/test-fdb.sh --client 8.0 --server 7.4 --project FoundationDB.Tests --filter "TestCategory=Fdb-Mismatch-Server"
#   ./scripts/test-fdb.sh --matrix                                  # every run of test-matrix.json
#   ./scripts/test-fdb.sh --matrix --run 7.3-api730 --run mismatch-api
#
# --client and --server take 'x.y' (the newest x.y entry of the manifest) or an exact 'x.y.z'. --server defaults to the
# client version, and --api to the highest version of both the binding and the client. --project (repeatable) takes a
# project name (looked up under artifacts/bin of this repository and of a parent repository) or the path of a test
# assembly. Other options: --framework (net11.0), --configuration (debug), --offline.
# The output of each run goes to artifacts/test-results/fdb/<run>/<project>.txt.
#
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(dirname "$here")"
native_project="$repo/FoundationDB.Client.Native"

client=""; server=""; api="0"; filter=""; matrix=0; offline=0
framework="net11.0"; configuration="debug"
projects=(); runs=()

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
	case "$1" in
		--client)        client="${2:?--client needs a value}"; shift 2;;
		--server)        server="${2:?--server needs a value}"; shift 2;;
		--api)           api="${2:?--api needs a value}"; shift 2;;
		--project)       projects+=("${2:?--project needs a value}"); shift 2;;
		--filter)        filter="${2:?--filter needs a value}"; shift 2;;
		--matrix)        matrix=1; shift;;
		--run)           runs+=("${2:?--run needs a value}"); shift 2;;
		--framework)     framework="${2:?--framework needs a value}"; shift 2;;
		--configuration) configuration="${2:?--configuration needs a value}"; shift 2;;
		--offline)       offline=1; shift;;
		-h|--help)       sed -n '2,30p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0;;
		*)               die "unknown argument: $1 (see --help)";;
	esac
done

command -v python3 >/dev/null 2>&1 || die "python3 is required (to read manifest.json and test-matrix.json)."
command -v dotnet  >/dev/null 2>&1 || die "dotnet is required."

case "$(uname -s)" in
	Linux)  os="linux";;
	Darwin) os="osx";;
	*)      die "unsupported OS: $(uname -s) (use test-fdb.ps1 on Windows)";;
esac
case "$(uname -m)" in
	x86_64|amd64)  arch="x64";;
	arm64|aarch64) arch="arm64";;
	*)             die "unsupported architecture: $(uname -m)";;
esac
rid="${os}-${arch}"

# python3 resolves the versions and expands the runs into one "run<TAB>client<TAB>server<TAB>api<TAB>project<TAB>filter<TAB>library" line per test run
plan="$(python3 - "$repo" "$rid" "$matrix" "$client" "$server" "$api" "$filter" "${#runs[@]}" ${runs[@]+"${runs[@]}"} ${projects[@]+"${projects[@]}"} <<'PY'
import json, os, sys
repo, rid, matrix, client, server, api, flt, nruns = sys.argv[1:9]
rest = sys.argv[9:]
selected, projects = rest[:int(nruns)], rest[int(nruns):]
manifest = json.load(open(os.path.join(repo, "FoundationDB.Client.Native", "manifest.json"), encoding="utf-8"))
config = json.load(open(os.path.join(repo, "scripts", "test-matrix.json"), encoding="utf-8"))
versions = list(manifest["versions"].keys())

def resolve(spec):
	parts = spec.split(".")
	if len(parts) == 3:
		if spec not in versions: sys.exit("version %s is not in manifest.json" % spec)
		return spec
	if len(parts) != 2: sys.exit("version '%s' is neither x.y nor x.y.z" % spec)
	match = sorted((v for v in versions if v.startswith(spec + ".")), key=lambda v: [int(x) for x in v.split(".")])
	if not match: sys.exit("manifest.json has no %s.x version" % spec)
	return match[-1]

def library(version):
	for f in manifest["versions"][version]["files"]:
		if f["rid"] == rid and "fdbcli" not in f["name"]:
			return f["name"]
	sys.exit("manifest.json has no %s native client for %s" % (rid, version))

if matrix == "1":
	runs = config["runs"]
	if selected: runs = [r for r in runs if r["name"] in selected]
	if not runs: sys.exit("no run of test-matrix.json matches %s" % ", ".join(selected))
else:
	runs = [{ "name": "custom", "client": client or config["default"], "server": server, "api": int(api), "filter": flt, "projects": projects }]

known = { p["name"]: p for p in config["projects"] }
for r in runs:
	c = resolve(r["client"])
	s = resolve(r["server"]) if r.get("server") else c
	a = int(r.get("api") or 0)
	name = r["name"]
	if name == "custom":
		name = "%s-srv%s" % (c, s) + ("-api%d" % a if a else "")
	plist = [known.get(n, { "name": n }) for n in r["projects"]] if r.get("projects") else config["projects"]
	if matrix == "1" and projects:
		# --project narrows a matrix run to these projects
		plist = [p for p in plist if p["name"] in projects] if r.get("projects") else [known.get(n, { "name": n }) for n in projects]
	for p in plist:
		print("\t".join([name, c, s, str(a), p["name"], r.get("filter") or p.get("filter") or "", library(c)]))
PY
)" || die "failed to plan the test runs"

find_assembly() {
	local name="$1"
	if [[ "$name" == *.dll && -f "$name" ]]; then echo "$name"; return; fi
	local root
	for root in "$repo" "$(dirname "$repo")"; do
		local dll="$root/artifacts/bin/$name/${configuration}_${framework}/$name.dll"
		if [ -f "$dll" ]; then echo "$dll"; return; fi
	done
	die "no test assembly for '$name' ($configuration, $framework). Build it first."
}

failures=0
summary=()
while IFS=$'\t' read -r run_name c s a project test_filter lib_name; do
	[ -n "$run_name" ] || continue
	cache="$repo/artifacts/native/$c"
	dl_args=(--version "$c" --manifest "$native_project/manifest.json" --output "$cache" --rid "$rid")
	[ "$offline" -eq 1 ] && dl_args+=(--offline)
	"$native_project/DownloadBinaries.sh" "${dl_args[@]}" > /dev/null
	library="$cache/runtimes/$rid/native/$lib_name"
	[ -f "$library" ] || die "the native client $c is missing at $library"

	dll="$(find_assembly "$project")"
	label="$(basename "$dll" .dll)"
	out_dir="$repo/artifacts/test-results/fdb/$run_name"
	mkdir -p "$out_dir"
	log="$out_dir/$label.txt"
	api_label="$a"; [ "$a" = "0" ] && api_label="max"
	printf '== %s / %s : client %s, server %s, API %s\n' "$run_name" "$label" "$c" "$s" "$api_label"

	test_args=("$dll")
	[ -n "$test_filter" ] && test_args+=(--filter "$test_filter")
	set +e
	if [ "$a" = "0" ]; then
		FDB_TEST_NATIVE_LIB_PATH="$library" FDB_TEST_DOCKER_TAG="$s" SNOWBANK_TEST_LOG="${SNOWBANK_TEST_LOG:-report}" dotnet "${test_args[@]}" > "$log" 2>&1
	else
		FDB_TEST_NATIVE_LIB_PATH="$library" FDB_TEST_DOCKER_TAG="$s" FDB_TEST_API_VERSION="$a" SNOWBANK_TEST_LOG="${SNOWBANK_TEST_LOG:-report}" dotnet "${test_args[@]}" > "$log" 2>&1
	fi
	code=$?
	set -e

	counts="$(grep -E '^\s+(total|failed|succeeded|skipped): [0-9]+' "$log" | sed -E 's/^\s+//; s/: / /' | paste -sd ',' - | sed 's/,/, /g')"
	status="ok"; [ "$code" -ne 0 ] && { status="exit $code"; failures=$((failures + 1)); }
	printf '   %s  %s  (%s)\n' "$status" "$counts" "$log"
	summary+=("$(printf '%-24s %-28s %-8s %-8s %-5s %-8s %s' "$run_name" "$label" "$c" "$s" "$api_label" "$status" "$counts")")
done <<< "$plan"

printf '\n%-24s %-28s %-8s %-8s %-5s %-8s %s\n' "Run" "Project" "Client" "Server" "Api" "Status" "Tests"
printf '%s\n' "${summary[@]}"
[ "$failures" -eq 0 ]
