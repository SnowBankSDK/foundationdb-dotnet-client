#!/usr/bin/env pwsh
#
# Release pre-flight: check (and optionally patch) the "latest known FoundationDB docker image" tags.
#
# FdbAspireHostingExtensions.cs hard-codes four constants (LatestVersion74/73/72/71) that the Latest*
# roll-forward policies resolve to when an app asks for the "latest" fdb image. They are maintained by
# hand and drift as FoundationDB publishes new images. This script queries the Docker Hub tags API for
# each 7.x branch, computes the newest NON-AVX tag, and compares it to what the source currently pins.
#
# AVX rule: FoundationDB ships each build as an even/odd pair of the SAME code. The EVEN patch is built
# WITHOUT AVX and runs everywhere (x64, and ARM64 / Apple Silicon laptops under Docker emulation); the
# ODD patch enables AVX and is x64-only (it will not run on an M-series Mac). For the local dev loop we
# always want the highest EVEN tag, so this script only ever considers even patch numbers.
#
# Usage:
#   ./scripts/check-fdb-image-tags.ps1          # report only (default); exit 1 if any constant is stale
#   ./scripts/check-fdb-image-tags.ps1 -Fix     # patch the stale constants in place, then report
#
# Note: uses page_size=100 ordered by last_updated (newest first), so the latest even tag is on page 1.
# That holds comfortably for the current release cadence; revisit if a branch ever gets 100+ newer tags.
#
[CmdletBinding()]
param(
    [switch] $Fix
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'FoundationDB.Aspire.Hosting/FdbAspireHostingExtensions.cs'

if (-not (Test-Path $source)) { throw "Source file not found: $source" }

$branches = @(
    [pscustomobject]@{ Const = 'LatestVersion74'; Major = 7; Minor = 4 }
    [pscustomobject]@{ Const = 'LatestVersion73'; Major = 7; Minor = 3 }
    [pscustomobject]@{ Const = 'LatestVersion72'; Major = 7; Minor = 2 }
    [pscustomobject]@{ Const = 'LatestVersion71'; Major = 7; Minor = 1 }
)

$text = Get-Content -Raw $source
$rows = @()
$failed = $false

foreach ($b in $branches) {
    $prefix = "$($b.Major).$($b.Minor)"

    # current pinned build, parsed straight from the source constant
    $current = $null
    if ($text -match "$($b.Const)\s*=\s*new Version\(\s*$($b.Major)\s*,\s*$($b.Minor)\s*,\s*(\d+)\s*\)") {
        $current = [int]$Matches[1]
    }

    # latest NON-AVX (even) tag from Docker Hub
    $latest = $null
    try {
        $url = "https://registry.hub.docker.com/v2/repositories/foundationdb/foundationdb/tags?name=$prefix&page_size=100"
        $tags = (Invoke-RestMethod -Uri $url).results.name
        $latest = $tags |
            Where-Object { $_ -match "^$($b.Major)\.$($b.Minor)\.\d+$" } |
            Where-Object { [int]($_.Split('.')[2]) % 2 -eq 0 } |
            Sort-Object { [version]$_ } |
            Select-Object -Last 1
    }
    catch {
        Write-Warning "Docker Hub query failed for $prefix : $($_.Exception.Message)"
        $failed = $true
    }

    $latestBuild = if ($latest) { [int]($latest.Split('.')[2]) } else { $null }

    $status =
        if ($null -eq $latestBuild) { 'QUERY FAILED' }
        elseif ($null -eq $current) { 'CONST NOT FOUND' }
        elseif ($current -eq $latestBuild) { 'ok' }
        else { 'STALE' }

    $rows += [pscustomobject]@{
        Constant  = $b.Const
        Current   = if ($null -ne $current) { "$prefix.$current" } else { '?' }
        LatestNonAvx = if ($latest) { $latest } else { '?' }
        Status    = $status
        _b        = $b
        _new      = $latestBuild
    }
}

$rows | Format-Table Constant, Current, LatestNonAvx, Status -AutoSize | Out-String | Write-Host

$stale = $rows | Where-Object { $_.Status -eq 'STALE' }

if (-not $stale) {
    if ($failed) { Write-Host "One or more branches could not be queried; see warnings above." -ForegroundColor Yellow; exit 2 }
    Write-Host "All four constants are current (latest non-AVX)." -ForegroundColor Green
    exit 0
}

if (-not $Fix) {
    Write-Host "$($stale.Count) constant(s) STALE. Re-run with -Fix to patch them in place, or edit $source by hand." -ForegroundColor Yellow
    exit 1
}

foreach ($row in $stale) {
    $b = $row._b
    $pattern = "($($b.Const)\s*=\s*new Version\(\s*$($b.Major)\s*,\s*$($b.Minor)\s*,\s*)\d+(\s*\))"
    $text = [regex]::Replace($text, $pattern, "`${1}$($row._new)`${2}")
    Write-Host "Patched $($b.Const): $($row.Current) -> $($row.LatestNonAvx)" -ForegroundColor Cyan
}

Set-Content -Path $source -Value $text -NoNewline
Write-Host "`nPatched $source. Review the diff and commit (submodule commit, no Co-Authored-By trailer)." -ForegroundColor Green
exit 0
