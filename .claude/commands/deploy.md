# /deploy

Compila o plugin Craftimizer em Release e deploya para o XIVLauncher local, pronto para teste in-game.

## Pré-requisitos

- .NET SDK 10.0+ (`dotnet --version`)
- Código compilável (0 erros)
- XIVLauncher instalado em `%APPDATA%\XIVLauncher`
- Versão definida em `Craftimizer/Craftimizer.csproj`

## Procedimento Padrão

```powershell
# 1. Build Release
dotnet build Craftimizer/Craftimizer.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 2. Ler versão
$xml = [xml](Get-Content Craftimizer/Craftimizer.csproj)
$version = $xml.Project.PropertyGroup[0].Version

# 3. Criar diretório de deploy
$pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version"
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

# 4. Copiar arquivos
Copy-Item "Craftimizer\bin\Release\*" $pluginDir -Force -Recurse

Write-Host "Deploy completo: $pluginDir" -ForegroundColor Green

# Manter apenas as 2 versões mais recentes
$installBase = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer"
$allDirs = Get-ChildItem -Path $installBase -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending
$allDirs | Select-Object -Skip 2 | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force
}
```

## Via Script (Recomendado)

```powershell
.\scripts\build.ps1 -Deploy             # Build + Deploy
.\scripts\build.ps1 -Configuration Release   # Só build
.\scripts\build.ps1 -Deploy -NoBuild    # Só deploy (usa build existente)
```

## Estrutura Deployada

```
%APPDATA%\XIVLauncher\installedPlugins\Craftimizer\{version}\
├─ Craftimizer.dll
├─ Craftimizer.json
├─ Craftimizer.UI.dll
├─ Craftimizer.Simulator.dll
├─ Craftimizer.Solver.dll
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
$version = ([xml](Get-Content Craftimizer/Craftimizer.csproj)).Project.PropertyGroup[0].Version
Test-Path "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version\Craftimizer.dll"
```

## Testes In-Game (Manual)

1. **Plugin carregado**: `/xlplugins` → "Craftimizer" na lista
2. **Comando**: `/craftimizer` abre janela principal
3. **Editor**: `/crafteditor` abre MacroEditor
4. **Overlays**: Abrir crafting log → overlay aparece

## Troubleshooting

**Build falhou:**
```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release --verbosity detailed
```

**Plugin não carrega in-game:**
```powershell
Get-Content "$env:APPDATA\XIVLauncher\dalamud.log" | Select-String "Craftimizer"
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
dotnet build Craftimizer/Craftimizer.csproj -c Release
.\scripts\build.ps1 -Deploy -NoBuild
```

## Warnings Aceitáveis

- `NU1900` — NuGet vulnerability check (Dalamud feed não suporta)
- `CA1805` — Inicialização redundante
- `CA1822` — Método pode ser estático
