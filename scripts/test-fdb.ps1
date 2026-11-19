#!/usr/bin/env pwsh
#
# Runs the real-cluster test suites with a chosen native client, server image and API version.
#
# Build the test projects first (dotnet build): every run uses the same test assemblies, and only the native
# client, the server image and the API version change between runs.
#
# Each client version is cached under artifacts/native/<version>/, for the current platform only. DownloadBinaries.ps1
# compares the cached files with the SHA-256 of manifest.json, and a file is downloaded only when it is missing or stale.
#
# Three environment variables set the combination of each run:
#   FDB_TEST_NATIVE_LIB_PATH   path of the native client library
#   FDB_TEST_DOCKER_TAG        tag of the foundationdb/foundationdb server image
#   FDB_TEST_API_VERSION       API version of the test process
#
# Usage:
#   ./scripts/test-fdb.ps1                                          # default run of test-matrix.json, every project
#   ./scripts/test-fdb.ps1 -Client 7.4                              # newest 7.4 client, 7.4 server, API 740
#   ./scripts/test-fdb.ps1 -Client 8.0 -Api 730 -Project FoundationDB.Tests -Filter "FullyQualifiedName~Tenant"
#   ./scripts/test-fdb.ps1 -Client 8.0 -Server 7.4 -Project FoundationDB.Tests -Filter "TestCategory=Fdb-Mismatch-Server"
#   ./scripts/test-fdb.ps1 -Matrix                                  # every run of test-matrix.json
#   ./scripts/test-fdb.ps1 -Matrix -Run 7.3-api730,mismatch-api     # some runs of test-matrix.json
#
# -Client and -Server take 'x.y' (the newest x.y entry of the manifest) or an exact 'x.y.z'. -Server defaults to the
# client version, and -Api to the highest version of both the binding and the client. -Project takes a project name
# (looked up under artifacts/bin of this repository and of a parent repository) or the path of a test assembly.
# The output of each run goes to artifacts/test-results/fdb/<run>/<project>.txt.
#
[CmdletBinding()]
param(
	[string] $Client,
	[string] $Server,
	[int] $Api = 0,
	[string[]] $Project,
	[string] $Filter,
	[switch] $Matrix,
	[string[]] $Run,
	[string] $Framework = 'net11.0',
	[string] $Configuration = 'debug',
	[switch] $Offline
)

$ErrorActionPreference = 'Stop'
# a comma-separated value arrives as one string under 'powershell -File'
$Run = @($Run | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Project = @($Project | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = Split-Path -Parent $PSScriptRoot
$nativeProject = Join-Path $repo 'FoundationDB.Client.Native'
$manifest = Get-Content -Raw (Join-Path $nativeProject 'manifest.json') | ConvertFrom-Json
$matrixFile = Get-Content -Raw (Join-Path $PSScriptRoot 'test-matrix.json') | ConvertFrom-Json

function Get-CurrentRid {
	$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
	if ($IsLinux) { return "linux-$arch" }
	if ($IsMacOS) { return "osx-$arch" }
	return "win-$arch"
}

# 'x.y' resolves to the newest x.y version of the manifest, 'x.y.z' must exist in the manifest
function Resolve-FdbVersion([string] $spec) {
	$versions = @($manifest.versions.PSObject.Properties.Name)
	if ($spec -match '^\d+\.\d+\.\d+$') {
		if ($versions -notcontains $spec) { throw "Version $spec is not in manifest.json." }
		return $spec
	}
	if ($spec -notmatch '^\d+\.\d+$') { throw "Version '$spec' is neither x.y nor x.y.z." }
	$match = $versions | Where-Object { $_.StartsWith("$spec.") } | Sort-Object { [version]$_ } | Select-Object -Last 1
	if (-not $match) { throw "manifest.json has no $spec.x version." }
	return $match
}

# downloads (or checks) the native client of a version for the current platform, and returns the path of the library
function Get-NativeClient([string] $version, [string] $rid) {
	$file = $manifest.versions.$version.files | Where-Object { $_.rid -eq $rid -and $_.name -notmatch 'fdbcli' } | Select-Object -First 1
	if (-not $file) { throw "manifest.json has no $rid native client for $version." }
	$cache = Join-Path $repo "artifacts/native/$version"
	$download = @{ version = $version; manifestPath = (Join-Path $nativeProject 'manifest.json'); outputDir = $cache; rid = @($rid) }
	if ($Offline) { $download.offline = $true }
	& (Join-Path $nativeProject 'DownloadBinaries.ps1') @download 6>$null | Out-Null
	$path = Join-Path $cache "runtimes/$rid/native/$($file.name)"
	if (-not (Test-Path $path)) { throw "The native client $version is missing at $path." }
	return $path
}

function Get-MatrixProject([string] $name) {
	$known = $matrixFile.projects | Where-Object { $_.name -eq $name } | Select-Object -First 1
	if ($known) { return $known }
	return [pscustomobject]@{ name = $name }
}

function Find-TestAssembly([string] $name) {
	if ($name.EndsWith('.dll') -and (Test-Path $name)) { return (Resolve-Path $name).Path }
	foreach ($root in @($repo, (Split-Path -Parent $repo))) {
		$dll = Join-Path $root "artifacts/bin/$name/$($Configuration)_$Framework/$name.dll"
		if (Test-Path $dll) { return $dll }
	}
	throw "No test assembly for '$name' ($Configuration, $Framework). Build it first."
}

# the list of runs: one from the parameters, or the runs of test-matrix.json
$runs = @()
if ($Matrix) {
	$runs = @($matrixFile.runs)
	if ($Run) { $runs = @($runs | Where-Object { $Run -contains $_.name }) }
	if ($runs.Count -eq 0) { throw "No run of test-matrix.json matches '$($Run -join ', ')'." }
} else {
	$spec = $Client
	if (-not $spec) { $spec = $matrixFile.default }
	$runs = @([pscustomobject]@{ name = 'custom'; client = $spec; server = $Server; api = $Api; filter = $Filter; projects = $Project })
}

$rid = Get-CurrentRid
$results = @()

foreach ($r in $runs) {
	$clientVersion = Resolve-FdbVersion $r.client
	$serverVersion = $clientVersion
	if ($r.server) { $serverVersion = Resolve-FdbVersion $r.server }
	$apiVersion = 0
	if ($r.api) { $apiVersion = [int]$r.api }
	$runName = $r.name
	if ($runName -eq 'custom') {
		$runName = "$clientVersion-srv$serverVersion"
		if ($apiVersion) { $runName += "-api$apiVersion" }
	}

	# the projects of a run: its own list, else the list of test-matrix.json; -Project narrows a matrix run to these projects
	$projects = @($matrixFile.projects)
	if ($r.projects) { $projects = @($r.projects | ForEach-Object { Get-MatrixProject $_ }) }
	if ($Matrix -and $Project) {
		if ($r.projects) { $projects = @($projects | Where-Object { $Project -contains $_.name }) }
		else { $projects = @($Project | ForEach-Object { Get-MatrixProject $_ }) }
	}
	if ($projects.Count -eq 0) { continue }

	$library = Get-NativeClient $clientVersion $rid

	foreach ($p in $projects) {
		$dll = Find-TestAssembly $p.name
		$testFilter = $r.filter
		if (-not $testFilter) { $testFilter = $p.filter }
		$label = [System.IO.Path]::GetFileNameWithoutExtension($dll)
		$outDir = Join-Path $repo "artifacts/test-results/fdb/$runName"
		New-Item -ItemType Directory -Force -Path $outDir | Out-Null
		$log = Join-Path $outDir "$label.txt"

		Write-Host "== $runName / $label : client $clientVersion, server $serverVersion, API $(if ($apiVersion) { $apiVersion } else { 'max' })" -ForegroundColor Cyan

		$env:FDB_TEST_NATIVE_LIB_PATH = $library
		$env:FDB_TEST_DOCKER_TAG = $serverVersion
		if ($apiVersion) { $env:FDB_TEST_API_VERSION = "$apiVersion" } else { Remove-Item Env:FDB_TEST_API_VERSION -ErrorAction SilentlyContinue }
		if (-not $env:SNOWBANK_TEST_LOG) { $env:SNOWBANK_TEST_LOG = 'report' }

		$testArgs = @($dll)
		if ($testFilter) { $testArgs += @('--filter', $testFilter) }
		try {
			& dotnet @testArgs 2>&1 | ForEach-Object { "$_" } | Out-File -FilePath $log -Encoding utf8
			$code = $LASTEXITCODE
		} finally {
			Remove-Item Env:FDB_TEST_NATIVE_LIB_PATH, Env:FDB_TEST_DOCKER_TAG, Env:FDB_TEST_API_VERSION -ErrorAction SilentlyContinue
		}

		$summary = (Select-String -Path $log -Pattern '^\s+(total|failed|succeeded|skipped): (\d+)' | ForEach-Object { "$($_.Matches[0].Groups[1].Value) $($_.Matches[0].Groups[2].Value)" }) -join ', '
		$status = if ($code -eq 0) { 'ok' } else { "exit $code" }
		Write-Host "   $status  $summary  ($log)"
		$results += [pscustomobject]@{ Run = $runName; Project = $label; Client = $clientVersion; Server = $serverVersion; Api = $(if ($apiVersion) { $apiVersion } else { 'max' }); Status = $status; Tests = $summary }
	}
}

$results | Format-Table -AutoSize | Out-String | Write-Host
if ($results | Where-Object { $_.Status -ne 'ok' }) { exit 1 }
exit 0
