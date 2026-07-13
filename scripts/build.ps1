#!/usr/bin/env pwsh
#
# Runs an ISOLATED (standalone) restore + clean + build of the SnowBank / FoundationDB solution.
#
# Forces CORESDK_STANDALONE_BUILD=true so the repo builds its OWN complete target set
# (net8.0/net10.0/net11.0 + netstandard2.0, plus the net472 validation targets) even when it is
# checked out as a submodule under a parent that trims the target frameworks. Use this to develop or
# validate the submodule on its own.
#
# CORESDK_STANDALONE_BUILD is passed as an MSBuild -p: property, NOT set as an environment variable,
# so it stays scoped to these invocations and cannot leak into the caller's shell (a persistent
# $env: assignment would silently force every later dotnet command in the same window into a
# standalone build).
#
# CAUTION, shared restore state: the restore step rewrites artifacts/obj/**/project.assets.json for
# the STANDALONE (superset) target set. After this runs, the FoundationDB submodule's assets no
# longer match the PARENT repo's trimmed set, so the next build FROM THE PARENT must re-run
# 'dotnet restore' there first, or it fails with NETSDK1005 (assets file has no target for the
# parent's TFM). The script reminds you of this at the end.
#
# Usage:  ./scripts/build.ps1 [-Configuration Debug|Release]
#
[CmdletBinding()]
param(
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repo 'FoundationDB.Client.slnx'
$standalone = '-p:CORESDK_STANDALONE_BUILD=true'

Write-Host "Isolated STANDALONE build of $solution ($Configuration)" -ForegroundColor Cyan
Write-Host "Complete target set: net8.0/net10.0/net11.0 + netstandard2.0 + net472`n"

Write-Host "[1/3] restore (standalone target set)..." -ForegroundColor Cyan
dotnet restore $solution $standalone
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)" }

Write-Host "`n[2/3] clean..." -ForegroundColor Cyan
dotnet clean $solution $standalone -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed ($LASTEXITCODE)" }

Write-Host "`n[3/3] build..." -ForegroundColor Cyan
dotnet build $solution $standalone -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

Write-Host "`nStandalone build complete." -ForegroundColor Green
Write-Host "NOTE: artifacts/obj now holds STANDALONE restore assets (net8.0 + netstandard2.0 + net472 included)." -ForegroundColor Yellow
Write-Host "      Before building the PARENT repo again, re-run 'dotnet restore' there so the FoundationDB" -ForegroundColor Yellow
Write-Host "      submodule's assets match the parent's trimmed target set, otherwise it fails with NETSDK1005." -ForegroundColor Yellow
