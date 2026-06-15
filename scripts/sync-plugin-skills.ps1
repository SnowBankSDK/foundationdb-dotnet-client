#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Sync the canonical Agent Skills (.claude/skills) into the plugin's skills/ folder.

.DESCRIPTION
    PowerShell equivalent of scripts/sync-plugin-skills.sh (for Windows PowerShell 5.1 or pwsh 7+).

    The plugin copy under plugins/foundationdb-skills/skills/ MUST be committed (the plugin
    marketplace reads the repo at a commit, with no build step on the consumer's side), but
    .claude/skills/ is the single source of truth. A committed symlink would not resolve reliably
    across Windows/macOS/Linux checkouts, so we keep a real copy and keep it in sync with this script.

.PARAMETER Check
    Verify the copy is in sync without modifying it; exits 1 if it has drifted (for CI).

.EXAMPLE
    ./scripts/sync-plugin-skills.ps1            # copy .claude/skills -> plugin (after editing skills)

.EXAMPLE
    ./scripts/sync-plugin-skills.ps1 -Check     # verify in sync (exit 1 if drifted)
#>
[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root '.claude/skills'
$dst = Join-Path $root 'plugins/foundationdb-skills/skills'

function Get-RelativeFileHashes([string] $base) {
    $map = @{}
    if (-not (Test-Path $base)) { return $map }
    $baseFull = (Resolve-Path $base).Path
    Get-ChildItem -Path $base -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($baseFull.Length).TrimStart('\', '/').Replace('\', '/')
        $map[$rel] = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    }
    return $map
}

if ($Check) {
    $a = Get-RelativeFileHashes $src
    $b = Get-RelativeFileHashes $dst
    $diffs = @()
    foreach ($k in $a.Keys) {
        if (-not $b.ContainsKey($k)) { $diffs += "missing in plugin: $k" }
        elseif ($a[$k] -ne $b[$k]) { $diffs += "differs:           $k" }
    }
    foreach ($k in $b.Keys) {
        if (-not $a.ContainsKey($k)) { $diffs += "extra in plugin:   $k" }
    }
    if ($diffs.Count -eq 0) {
        Write-Host "plugin skills are in sync with .claude/skills"
    }
    else {
        [Console]::Error.WriteLine("ERROR: plugins/foundationdb-skills/skills is out of sync with .claude/skills.")
        [Console]::Error.WriteLine("       Run: scripts/sync-plugin-skills.ps1")
        $diffs | ForEach-Object { [Console]::Error.WriteLine("  $_") }
        exit 1
    }
}
else {
    if (Test-Path $dst) { Remove-Item -Recurse -Force $dst }
    New-Item -ItemType Directory -Path $dst | Out-Null
    Copy-Item -Path (Join-Path $src '*') -Destination $dst -Recurse -Force
    Write-Host "synced .claude/skills -> plugins/foundationdb-skills/skills"
}
