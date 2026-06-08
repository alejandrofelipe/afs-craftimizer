#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and optionally deploy Craftimizer to XIVLauncher.

.PARAMETER Configuration
    Build configuration. Default: Debug. When -Deploy is set, also builds Release.

.PARAMETER Deploy
    Copy Release build output to XIVLauncher installedPlugins after building.

.PARAMETER NoBuild
    Skip build and just deploy whatever is already in the Release bin folder.

.PARAMETER Bump
    Bump the version before building. Accepted values: major, minor, patch, build (default).
    Calls bump-version.ps1 with the given type.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release
    .\build.ps1 -Deploy
    .\build.ps1 -Deploy -Bump build     # bump build number + build + deploy
    .\build.ps1 -Deploy -Bump patch     # bump patch + build + deploy
    .\build.ps1 -Deploy -NoBuild
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Deploy,
    [switch]$NoBuild,

    [ValidateSet("major", "minor", "patch", "build")]
    [string]$Bump
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root     = Split-Path $PSScriptRoot -Parent
$csproj   = "$root\Craftimizer\Craftimizer.csproj"

# Prefer Scoop-installed SDK; fall back to PATH
$scoopDotnet = "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"
$dotnet = if (Test-Path $scoopDotnet) { $scoopDotnet } else { "dotnet" }

# --- bump version (optional) ---
if ($Bump) {
    & "$PSScriptRoot\bump-version.ps1" -Type $Bump
}

# --- read version from csproj ---
$xml     = [xml](Get-Content $csproj)
$version = $xml.Project.PropertyGroup[0].Version

# --- build ---
if (-not $NoBuild) {
    # Always build the requested configuration
    Write-Host "Building Craftimizer $version ($Configuration)..." -ForegroundColor Cyan
    & $dotnet build $csproj -c $Configuration --nologo /p:NodeReuse=false
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($Configuration)." }

    # When deploying, also ensure Release is up to date
    if ($Deploy -and $Configuration -ne "Release") {
        Write-Host "Building Craftimizer $version (Release)..." -ForegroundColor Cyan
        & $dotnet build $csproj -c Release --nologo /p:NodeReuse=false
        if ($LASTEXITCODE -ne 0) { throw "Build failed (Release)." }
    }

    Write-Host "Build succeeded." -ForegroundColor Green
}

# --- deploy (always from Release) ---
if ($Deploy) {
    $binDir   = "$root\Craftimizer\bin\Release"
    $pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version"
    if (-not (Test-Path $pluginDir)) {
        Write-Host "Creating plugin directory: $pluginDir" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $pluginDir | Out-Null
    }
    Write-Host "Deploying to $pluginDir ..." -ForegroundColor Cyan
    Copy-Item "$binDir\*" $pluginDir -Force
    Write-Host "Deploy complete." -ForegroundColor Green
}
