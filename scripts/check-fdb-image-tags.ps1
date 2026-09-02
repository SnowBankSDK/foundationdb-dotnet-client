#!/usr/bin/env pwsh
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
#   ./scripts/check-fdb-image-tags.ps1          # report only (default); exit 1 if any constant is stale
#   ./scripts/check-fdb-image-tags.ps1 -Fix     # patch the stale constants in place, then report
#
# Note: uses page_size=100 ordered by last_updated (newest first), so the latest usable tag is on page 1.
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
    [pscustomobject]@{ Const = 'LatestVersion74'; Major = 7; Minor = 4; EvenOnly = $false }
    [pscustomobject]@{ Const = 'LatestVersion73'; Major = 7; Minor = 3; EvenOnly = $true }
    [pscustomobject]@{ Const = 'LatestVersion72'; Major = 7; Minor = 2; EvenOnly = $true }
    [pscustomobject]@{ Const = 'LatestVersion71'; Major = 7; Minor = 1; EvenOnly = $true }
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

    # newest usable tag from Docker Hub (even patch only where the line still ships AVX pairs)
    $latest = $null
    try {
        $url = "https://registry.hub.docker.com/v2/repositories/foundationdb/foundationdb/tags?name=$prefix&page_size=100"
        $tags = (Invoke-RestMethod -Uri $url).results.name
        $latest = $tags |
            Where-Object { $_ -match "^$($b.Major)\.$($b.Minor)\.\d+$" } |
            Where-Object { -not $b.EvenOnly -or ([int]($_.Split('.')[2]) % 2 -eq 0) } |
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
        Latest    = if ($latest) { $latest } else { '?' }
        Status    = $status
        _b        = $b
        _new      = $latestBuild
    }
}

$rows | Format-Table Constant, Current, Latest, Status -AutoSize | Out-String | Write-Host

$stale = $rows | Where-Object { $_.Status -eq 'STALE' }

if (-not $stale) {
    if ($failed) { Write-Host "One or more branches could not be queried; see warnings above." -ForegroundColor Yellow; exit 2 }
    Write-Host "All four constants are current." -ForegroundColor Green
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
    Write-Host "Patched $($b.Const): $($row.Current) -> $($row.Latest)" -ForegroundColor Cyan
}

Set-Content -Path $source -Value $text -NoNewline
Write-Host "`nPatched $source. Review the diff and commit (submodule commit, no Co-Authored-By trailer)." -ForegroundColor Green
exit 0
