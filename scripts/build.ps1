#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build, deploy, and package Craftimizer.

.PARAMETER Configuration
    Build configuration. Default: Debug. When -Deploy or -Package is set, also builds Release.

.PARAMETER Deploy
    Copy Release build output to XIVLauncher installedPlugins after building.
    Keeps only the two most recent version folders; removes older ones.

.PARAMETER NoBuild
    Skip build and just deploy/package whatever is already in the Release bin folder.

.PARAMETER Studio
    Also build Craftimizer.UIStudio after the main plugin build.
    UIStudio is not referenced by the plugin, so it must be built explicitly.

.PARAMETER Bump
    Bump the version before building. Use -BumpType to specify level (default: build).

.PARAMETER BumpType
    Version component to bump. One of: major, minor, patch, build. Default: build.
    Only used when -Bump is specified.

.PARAMETER Package
    Build Release and package as a .zip in the dist/ folder (or -PackageOutputDir).

.PARAMETER PackageOutputDir
    Output directory for the .zip file. Default: dist.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release
    .\build.ps1 -Deploy
    .\build.ps1 -Deploy -NoBuild
    .\build.ps1 -Studio
    .\build.ps1 -Bump                        # bump build number (default)
    .\build.ps1 -Bump -BumpType patch        # bump patch
    .\build.ps1 -Deploy -Bump
    .\build.ps1 -Deploy -Bump -BumpType minor
    .\build.ps1 -Package
    .\build.ps1 -Package -NoBuild
    .\build.ps1 -Package -PackageOutputDir releases
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Deploy,
    [switch]$NoBuild,
    [switch]$Studio,

    [switch]$Bump,
    [ValidateSet("major", "minor", "patch", "build")]
    [string]$BumpType = "build",

    [switch]$Package,
    [string]$PackageOutputDir = "dist"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root         = Split-Path $PSScriptRoot -Parent
$csproj       = "$root\Craftimizer\Craftimizer.csproj"
$studioCsproj = "$root\Craftimizer.UIStudio\Craftimizer.UIStudio.csproj"

# Prefer Scoop-installed SDK; fall back to PATH
$scoopDotnet = "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"
$dotnet = if (Test-Path $scoopDotnet) { $scoopDotnet } else { "dotnet" }

# --- bump version (optional) ---
if ($Bump) {
    & "$PSScriptRoot\bump-version.ps1" -Type $BumpType
}

# --- read version from csproj ---
$xml     = [xml](Get-Content $csproj)
$version = $xml.Project.PropertyGroup[0].Version

# --- build ---
if (-not $NoBuild) {
    Write-Host "Building Craftimizer $version ($Configuration)..." -ForegroundColor Cyan
    & $dotnet build $csproj -c $Configuration --nologo /p:NodeReuse=false
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($Configuration)." }

    # When deploying or packaging, also ensure Release is up to date
    if (($Deploy -or $Package) -and $Configuration -ne "Release") {
        Write-Host "Building Craftimizer $version (Release)..." -ForegroundColor Cyan
        & $dotnet build $csproj -c Release --nologo /p:NodeReuse=false
        if ($LASTEXITCODE -ne 0) { throw "Build failed (Release)." }
    }

    Write-Host "Build succeeded." -ForegroundColor Green
}

# --- UIStudio (optional) ---
if ($Studio -and -not $NoBuild) {
    Write-Host "Building Craftimizer.UIStudio ($Configuration)..." -ForegroundColor Cyan
    & $dotnet build $studioCsproj -c $Configuration --nologo /p:NodeReuse=false
    if ($LASTEXITCODE -ne 0) { throw "UIStudio build failed." }
    Write-Host "UIStudio build succeeded." -ForegroundColor Green
}

# --- deploy (always from Release) ---
if ($Deploy) {
    $binDir    = "$root\Craftimizer\bin\Release"
    $pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version"
    if (-not (Test-Path $pluginDir)) {
        Write-Host "Creating plugin directory: $pluginDir" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $pluginDir | Out-Null
    }
    Write-Host "Deploying to $pluginDir ..." -ForegroundColor Cyan
    Copy-Item "$binDir\*" $pluginDir -Force
    Write-Host "Deploy complete." -ForegroundColor Green

    # Keep only the two most recent version folders (current + previous)
    $installBase = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer"
    $allDirs = Get-ChildItem -Path $installBase -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending
    $toRemove = $allDirs | Select-Object -Skip 2
    foreach ($dir in $toRemove) {
        Write-Host "Removing old version: $($dir.Name)" -ForegroundColor DarkGray
        Remove-Item $dir.FullName -Recurse -Force
    }
}

# --- package (creates a .zip in dist/) ---
if ($Package) {
    $binDir    = "$root\Craftimizer\bin\Release"
    $outputDir = "$root\$PackageOutputDir"

    if (-not (Test-Path $binDir)) {
        throw "Release bin directory not found: $binDir. Run without -NoBuild or build manually first."
    }
    if (-not (Test-Path $outputDir)) {
        Write-Host "Creating output directory: $outputDir" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }

    $zipName = "Craftimizer-v$version.zip"
    $zipPath = "$outputDir\$zipName"

    Write-Host "Packaging $zipName ..." -ForegroundColor Cyan
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$binDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

    $sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "Package ready: $zipPath ($sizeMB MB)" -ForegroundColor Green
}
