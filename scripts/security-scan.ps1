#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Roda os 3 scanners de segurança no projeto Craftimizer sem precisar de Docker.

.DESCRIPTION
    1. dotnet list package --vulnerable  — CVEs nas dependências NuGet
    2. Security Code Scan (Roslyn)       — SAST via build (analyzer no csproj)
    3. Semgrep OSS                       — SAST profundo com ruleset csharp

.PARAMETER SkipBuild
    Pula o build (e o Security Code Scan). Útil se já fez build recentemente.

.PARAMETER SkipSemgrep
    Pula o Semgrep mesmo que esteja instalado.

.PARAMETER InstallSemgrep
    Tenta instalar o Semgrep via winget ou pip antes de rodar.

.EXAMPLE
    .\scripts\security-scan.ps1
    .\scripts\security-scan.ps1 -InstallSemgrep
    .\scripts\security-scan.ps1 -SkipBuild
#>
param(
    [switch]$SkipBuild,
    [switch]$SkipSemgrep,
    [switch]$InstallSemgrep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root   = Split-Path $PSScriptRoot -Parent
$csproj = "$root\Craftimizer\Craftimizer.csproj"

$scoopDotnet = "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"
$dotnet = if (Test-Path $scoopDotnet) { $scoopDotnet } else { "dotnet" }

# Garante que Python/semgrep do scoop estão no PATH
$scoopPython = "C:\Users\aleja\scoop\apps\python\current"
if (Test-Path $scoopPython) {
    $env:PATH = "$scoopPython\Scripts;$scoopPython;$env:PATH"
}

$issues = 0

function Write-Section([string]$title) {
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor DarkCyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor DarkCyan
}

# ---------------------------------------------------------------
# 1. NuGet Vulnerability Audit
# ---------------------------------------------------------------
Write-Section "1/3 — NuGet Vulnerability Audit (dotnet list package --vulnerable)"

# --source restringe ao nuget.org, evitando erro NU1301 do feed Dalamud (404)
$auditOutput = & $dotnet list $csproj package --vulnerable --include-transitive `
    --source https://api.nuget.org/v3/index.json 2>&1
$auditOutput | ForEach-Object { Write-Host $_ }

$vulnLines = $auditOutput | Where-Object { $_ -match "Critical|High|Moderate|Low" }
if ($vulnLines) {
    Write-Host ""
    Write-Host "VULNERABILIDADES ENCONTRADAS nas dependencias!" -ForegroundColor Red
    $issues += $vulnLines.Count
} else {
    Write-Host ""
    Write-Host "Nenhuma vulnerabilidade conhecida nas dependencias." -ForegroundColor Green
}

# ---------------------------------------------------------------
# 2. Security Code Scan (Roslyn analyzer via build)
# ---------------------------------------------------------------
Write-Section "2/3 — Security Code Scan (Roslyn SAST via build)"

if ($SkipBuild) {
    Write-Host "Pulando build (--SkipBuild definido)." -ForegroundColor Yellow
} else {
    Write-Host "Compilando com analyzers ativos..." -ForegroundColor Cyan

    $buildOutput = & $dotnet build $csproj -c Release --nologo /p:NodeReuse=false 2>&1
    $scsWarnings = $buildOutput | Where-Object { $_ -match "SCS\d{4}" }

    $buildOutput | ForEach-Object {
        if ($_ -match "SCS\d{4}") {
            Write-Host $_ -ForegroundColor Yellow
        }
    }

    if ($scsWarnings) {
        Write-Host ""
        Write-Host "$($scsWarnings.Count) aviso(s) do Security Code Scan encontrado(s)." -ForegroundColor Yellow
        $issues += $scsWarnings.Count
    } else {
        Write-Host ""
        Write-Host "Security Code Scan: nenhum problema detectado." -ForegroundColor Green
    }
}

# ---------------------------------------------------------------
# 3. Semgrep
# ---------------------------------------------------------------
Write-Section "3/3 — Semgrep OSS (SAST)"

$semgrepCmd = $null
try {
    $null = semgrep --version 2>&1
    if ($LASTEXITCODE -eq 0) { $semgrepCmd = "semgrep" }
} catch { }

if (-not $semgrepCmd -and $InstallSemgrep) {
    Write-Host "Semgrep nao encontrado. Tentando instalar..." -ForegroundColor Yellow

    # Tenta pip (método oficial no Windows)
    if (Get-Command pip -ErrorAction SilentlyContinue) {
        Write-Host "Instalando via pip..." -ForegroundColor Cyan
        pip install semgrep --quiet
        if ($LASTEXITCODE -eq 0) { $semgrepCmd = "semgrep" }
    } elseif (Get-Command pip3 -ErrorAction SilentlyContinue) {
        Write-Host "Instalando via pip3..." -ForegroundColor Cyan
        pip3 install semgrep --quiet
        if ($LASTEXITCODE -eq 0) { $semgrepCmd = "semgrep" }
    }

    # Tenta scoop como fallback
    if (-not $semgrepCmd -and (Get-Command scoop -ErrorAction SilentlyContinue)) {
        Write-Host "Instalando via scoop..." -ForegroundColor Cyan
        scoop install semgrep 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { $semgrepCmd = "semgrep" }
    }
}

if ($SkipSemgrep -or -not $semgrepCmd) {
    if (-not $semgrepCmd) {
        Write-Host "Semgrep nao encontrado." -ForegroundColor Yellow
        Write-Host "Para instalar: pip install semgrep  (requer Python 3.9+)" -ForegroundColor Gray
        Write-Host "  Python: https://www.python.org/downloads/" -ForegroundColor Gray
        Write-Host "Rode novamente com -InstallSemgrep para instalar automaticamente." -ForegroundColor Gray
    } else {
        Write-Host "Pulando Semgrep (--SkipSemgrep definido)." -ForegroundColor Yellow
    }
} else {
    Write-Host "Rodando semgrep com ruleset csharp..." -ForegroundColor Cyan
    $semgrepOutput = & semgrep scan --config "p/csharp" --config "p/secrets" $root 2>&1
    $semgrepOutput | ForEach-Object { Write-Host $_ }

    $semgrepFindings = $semgrepOutput | Where-Object { $_ -match "findings|Finding" -and $_ -notmatch "^0 findings" }
    $zeroFindings    = $semgrepOutput | Where-Object { $_ -match "^0 findings" }

    if ($zeroFindings) {
        Write-Host ""
        Write-Host "Semgrep: nenhum problema encontrado." -ForegroundColor Green
    } elseif ($semgrepFindings) {
        Write-Host ""
        Write-Host "Semgrep: problemas encontrados — veja o output acima." -ForegroundColor Red
        $issues++
    }
}

# ---------------------------------------------------------------
# Resumo
# ---------------------------------------------------------------
Write-Section "Resumo"

if ($issues -eq 0) {
    Write-Host "Todos os scanners passaram sem problemas!" -ForegroundColor Green
} else {
    Write-Host "$issues item(s) para revisar. Veja o output acima." -ForegroundColor Yellow
}
Write-Host ""
