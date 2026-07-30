#!/usr/bin/env pwsh
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
# Usage:  ./scripts/pack.ps1 [-Output <dir>] [-Configuration Release] [-VersionSuffix <suffix>]
#
# -VersionSuffix appends a pre-release suffix to the version from VersionInfo.props (e.g. `rc.1`
# gives 7.4.3-rc.1), and the default output folder is named after the full version.
#
[CmdletBinding()]
param(
    [string] $Output,
    [string] $Configuration = 'Release',
    [string] $VersionSuffix
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repo 'FoundationDB.Client.slnx'

$version = "$(([xml](Get-Content (Join-Path $repo 'Common/VersionInfo.props'))).Project.PropertyGroup.VersionPrefix)".Trim()
if ($VersionSuffix) { $version = "$version-$VersionSuffix" }
if (-not $Output) { $Output = Join-Path $repo "artifacts/packages/$version" }

Write-Host "Packing $solution  (version $version, $Configuration)" -ForegroundColor Cyan
Write-Host "Output: $Output`n"
if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
New-Item -ItemType Directory -Force -Path $Output | Out-Null

# Complete target set; keep the two knowingly-suppressed warnings:
#   CS1591 - missing XML doc comments
#   NU5104 - the net11.0 target references .NET-11-preview packages while .NET 11 is in preview
#            (the net8.0 / net10.0 targets are clean; this self-resolves once .NET 11 ships stable)
# CORESDK_STANDALONE_BUILD is passed as an MSBuild property, NOT set as an environment variable:
# `.\pack.ps1` runs in the caller's own PowerShell process, so a `$env:` assignment would persist
# after the script exits and silently force every later `dotnet` command in that window into a
# standalone (net472 + netstandard2.0) build. A -p: property is scoped to this one invocation.
# [string[]] is load-bearing: PowerShell unwraps a single-element array coming out of an `if`,
# and splatting the resulting STRING passes it one character at a time (MSBuild: error MSB1001).
[string[]] $suffixArg = if ($VersionSuffix) { @("-p:VersionSuffix=$VersionSuffix") } else { @() }
dotnet pack $solution '-p:CORESDK_STANDALONE_BUILD=true' -c $Configuration '-p:ContinuousIntegrationBuild=true' '-nowarn:CS1591,NU5104' -o $Output @suffixArg
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed ($LASTEXITCODE)" }

# --- validate the produced packages ---
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = Get-ChildItem $Output -Filter *.nupkg
$problems = New-Object System.Collections.Generic.List[string]
Write-Host "`nProduced $($packages.Count) package(s):" -ForegroundColor Cyan
foreach ($pkg in $packages) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($pkg.FullName)
    try {
        $names = $zip.Entries | ForEach-Object { $_.FullName }
        $tfms = $names | Where-Object { $_ -match '^lib/[^/]+/' } | ForEach-Object { ($_ -split '/')[1] } | Sort-Object -Unique
        $hasReadme = @($names | Where-Object { $_ -ieq 'README.md' }).Count -gt 0
        Write-Host ("  {0,-54} {1}" -f $pkg.Name, $(if ($tfms) { $tfms -join ', ' } else { '(tool/analyzer)' }))
        if (-not $hasReadme) { $problems.Add("$($pkg.Name): no README.md") }
        if ($tfms -and ($tfms -notcontains 'net8.0')) { $problems.Add("$($pkg.Name): missing net8.0 (incomplete target set - was the standalone build honored?)") }
    } finally { $zip.Dispose() }
}

if ($problems.Count -gt 0) {
    Write-Host "`nVALIDATION PROBLEMS:" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "`nAll packages carry a README and net8.0 (complete target set)." -ForegroundColor Green
Write-Host "NOT pushed - publish the .nupkg files manually." -ForegroundColor Yellow
