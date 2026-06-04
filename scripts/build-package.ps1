#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build Craftimizer in Release mode and package it as a .zip for Dalamud distribution.

.PARAMETER NoBuild
    Skip build and just package whatever is already in the Release bin folder.

.PARAMETER OutputDir
    Output directory for the .zip file. Default: dist/

.EXAMPLE
    .\build-package.ps1
    .\build-package.ps1 -NoBuild
    .\build-package.ps1 -OutputDir "releases"
#>
param(
    [switch]$NoBuild,
    [string]$OutputDir = "dist"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root      = Split-Path $PSScriptRoot -Parent
$csproj    = "$root\Craftimizer\Craftimizer.csproj"
$binDir    = "$root\Craftimizer\bin\Release"
$outputDir = "$root\$OutputDir"

# Prefer Scoop-installed SDK; fall back to PATH
$scoopDotnet = "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"
$dotnet = if (Test-Path $scoopDotnet) { $scoopDotnet } else { "dotnet" }

# --- read version from csproj ---
$xml     = [xml](Get-Content $csproj)
$version = $xml.Project.PropertyGroup[0].Version

Write-Host "=== Craftimizer Package Builder ===" -ForegroundColor Cyan
Write-Host "Version: $version" -ForegroundColor Yellow

# --- build ---
if (-not $NoBuild) {
    Write-Host ""
    Write-Host "Building Release configuration..." -ForegroundColor Cyan
    & $dotnet build $csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    Write-Host "Build succeeded." -ForegroundColor Green
}

# --- verify bin directory exists ---
if (-not (Test-Path $binDir)) {
    throw "Release bin directory not found: $binDir. Run without -NoBuild or build manually first."
}

# --- create output directory ---
if (-not (Test-Path $outputDir)) {
    Write-Host ""
    Write-Host "Creating output directory: $outputDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# --- package ---
$zipName = "Craftimizer-v$version.zip"
$zipPath = "$outputDir\$zipName"

Write-Host ""
Write-Host "Packaging plugin..." -ForegroundColor Cyan
Write-Host "  Source: $binDir" -ForegroundColor Gray
Write-Host "  Output: $zipPath" -ForegroundColor Gray

# Remove old zip if exists
if (Test-Path $zipPath) {
    Write-Host "  Removing existing package..." -ForegroundColor Yellow
    Remove-Item $zipPath -Force
}

# Create zip archive
# We need to package everything in the Release folder including subdirectories (like win-x64/)
Compress-Archive -Path "$binDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

# --- verify ---
if (Test-Path $zipPath) {
    $size = (Get-Item $zipPath).Length / 1MB
    Write-Host ""
    Write-Host "Package created successfully!" -ForegroundColor Green
    Write-Host "  File: $zipName" -ForegroundColor Green
    Write-Host "  Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
    Write-Host "  Path: $zipPath" -ForegroundColor Gray
} else {
    throw "Failed to create package."
}

Write-Host ""
Write-Host "Package ready for distribution to Dalamud!" -ForegroundColor Cyan
