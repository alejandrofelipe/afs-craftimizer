#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Bump the Artificer version in Artificer.csproj.

.PARAMETER Type
    Which part of the version to increment.
    major  →  X.0.0.0
    minor  →  x.X.0.0
    patch  →  x.x.X.0
    build  →  x.x.x.X  (default)

.PARAMETER Set
    Set an explicit version string instead of incrementing (e.g. "3.0.0.0").

.EXAMPLE
    .\bump-version.ps1                  # bump build number
    .\bump-version.ps1 -Type patch      # bump patch, reset build to 0
    .\bump-version.ps1 -Type minor      # bump minor, reset patch and build
    .\bump-version.ps1 -Set "3.0.0.0"   # set explicit version
#>
param(
    [ValidateSet("major", "minor", "patch", "build")]
    [string]$Type = "build",

    [string]$Set
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root   = Split-Path $PSScriptRoot -Parent
$csproj = "$root\Artificer\Artificer.csproj"
$content = Get-Content $csproj -Raw

# --- parse current version ---
if ($content -notmatch '<Version>(\d+)\.(\d+)\.(\d+)\.(\d+)</Version>') {
    throw "Could not find <Version>X.X.X.X</Version> in $csproj"
}
$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$patch = [int]$Matches[3]
$build = [int]$Matches[4]
$old   = "$major.$minor.$patch.$build"

# --- compute new version ---
if ($Set) {
    if ($Set -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid version format '$Set'. Expected X.X.X.X" }
    $new = $Set
} else {
    switch ($Type) {
        "major" { $major++; $minor = 0; $patch = 0; $build = 0 }
        "minor" { $minor++; $patch = 0; $build = 0 }
        "patch" { $patch++; $build = 0 }
        "build" { $build++ }
    }
    $new = "$major.$minor.$patch.$build"
}

# --- update csproj ---
$updated = $content -replace "<Version>$([regex]::Escape($old))</Version>", "<Version>$new</Version>"
Set-Content $csproj $updated -NoNewline

Write-Host "Version bumped: $old  →  $new" -ForegroundColor Green
