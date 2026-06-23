---
name: deploy
description: Compila o plugin Artificer em Release e deploya para o XIVLauncher local, pronto para teste in-game. Usar quando pedirem para deployar, instalar localmente ou testar o plugin no jogo.
---

# /deploy

Compila o plugin Artificer em Release e deploya para o XIVLauncher local, pronto para teste in-game.

## Pré-requisitos

- .NET SDK 10.0+ (`dotnet --version`)
- Código compilável (0 erros)
- XIVLauncher instalado em `%APPDATA%\XIVLauncher`
- Versão definida em `Artificer/Artificer.csproj`

## Procedimento Padrão

```powershell
# 1. Build Release
dotnet build Artificer/Artificer.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 2. Ler versão
$xml = [xml](Get-Content Artificer/Artificer.csproj)
$version = $xml.Project.PropertyGroup[0].Version

# 3. Criar diretório de deploy
$pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Artificer\$version"
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

# 4. Copiar arquivos
Copy-Item "Artificer\bin\Release\*" $pluginDir -Force -Recurse

Write-Host "Deploy completo: $pluginDir" -ForegroundColor Green

# Manter apenas as 2 versões mais recentes
$installBase = "$env:APPDATA\XIVLauncher\installedPlugins\Artificer"
$allDirs = Get-ChildItem -Path $installBase -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending
$allDirs | Select-Object -Skip 2 | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force
}
```

## Via Script (Recomendado)

```powershell
.\scripts\build.ps1 -Deploy                   # Build + Deploy
.\scripts\build.ps1 -Deploy -NoBuild          # Só deploy (usa build existente)
.\scripts\build.ps1 -Deploy -Bump             # Bump build number + Build + Deploy
.\scripts\build.ps1 -Deploy -Bump -BumpType patch   # Bump patch + Build + Deploy
.\scripts\build.ps1 -Package                  # Build Release + gerar zip em dist/
```

## Estrutura Deployada

```
%APPDATA%\XIVLauncher\installedPlugins\Artificer\{version}\
├─ Artificer.dll
├─ Artificer.json
├─ Artificer.UI.dll
├─ Artificer.Simulator.dll
├─ Artificer.Solver.dll
├─ Microsoft.Data.Sqlite.dll
├─ Microsoft.Extensions.Caching.Memory.dll
├─ Raphael.Net.dll
└─ raphael.dll
```

> `cimgui.dll` **não** está presente — o target MSBuild `RemoveCimguiDll` o remove pós-build.
> Dalamud fornece o `cimgui.dll` nativo; shipar cópia duplicada causaria `GImGui == NULL`.
> `ImGui.NET.dll` **está presente** — Dalamud SDK 15+ não distribui mais essa DLL; o plugin a shipa.

## Verificação Pós-Deploy

```powershell
$version = ([xml](Get-Content Artificer/Artificer.csproj)).Project.PropertyGroup[0].Version
Test-Path "$env:APPDATA\XIVLauncher\installedPlugins\Artificer\$version\Artificer.dll"
```

## Testes In-Game (Manual)

1. **Plugin carregado**: `/xlplugins` → "Artificer" na lista
2. **Comando**: `/Artificer` abre janela principal
3. **Editor**: `/crafteditor` abre MacroEditor
4. **Overlays**: Abrir crafting log → overlay aparece

## Troubleshooting

**Build falhou:**
```powershell
dotnet build Artificer/Artificer.csproj -c Release --verbosity detailed
```

**Plugin não carrega in-game:**
```powershell
Get-Content "$env:APPDATA\XIVLauncher\dalamud.log" | Select-String "Artificer"
```

**Access denied ao copiar (jogo rodando):**
- Fechar FFXIV completamente, aguardar 5s, tentar novamente

**SDK não encontrado:**
```powershell
# .NET via Scoop
$env:PATH = "C:\Users\aleja\scoop\apps\dotnet-sdk\current;$env:PATH"
```

**Versão antiga in-game:**
```powershell
dotnet clean
dotnet build Artificer/Artificer.csproj -c Release
.\scripts\build.ps1 -Deploy -NoBuild
```

## Warnings Aceitáveis

- `NU1900` — NuGet vulnerability check (Dalamud feed não suporta)
- `CA1805` — Inicialização redundante
- `CA1822` — Método pode ser estático
