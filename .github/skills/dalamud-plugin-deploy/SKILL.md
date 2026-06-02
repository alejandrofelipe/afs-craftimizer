---
name: dalamud-plugin-deploy
description: >
  Build e deploy do plugin Craftimizer para XIVLauncher.
  Compila Release build, copia para installedPlugins, verifica erros.
  Suporta deploy local (testing) e criação de packages para distribuição.
  
  Triggers: "fazer deploy do plugin", "build e instalar", "compilar Craftimizer",
  "deploy para XIVLauncher", "instalar plugin local", "criar release build",
  "package plugin for distribution".
  
  NOT for: análise de compatibilidade (use ffxiv-patch-compatibility-check),
  bump de versão (use craftimizer-version-bump), debug de erros (use agent principal).
---

# Skill: Dalamud Plugin Deploy

## Objetivo

Compilar o plugin Craftimizer em Release configuration e deployar para o diretório local do XIVLauncher, pronto para teste in-game.

## Quando Usar

- Após implementar nova feature
- Após corrigir bug
- Para testar mudanças in-game
- Antes de criar release tag no GitHub

## Pré-requisitos

1. .NET SDK 10.0+ instalado (verificar com `dotnet --version`)
2. Código compilável (0 erros)
3. XIVLauncher instalado em `%APPDATA%\XIVLauncher`
4. Versão do plugin definida em `Craftimizer/Craftimizer.csproj`

## Procedimento

### Método 1: Script Automatizado (Recomendado)

Usar o script PowerShell `scripts/build.ps1`:

```powershell
# Build Debug + Deploy
.\scripts\build.ps1 -Deploy

# Build Release apenas (sem deploy)
.\scripts\build.ps1 -Configuration Release

# Deploy sem rebuild (usa build existente)
.\scripts\build.ps1 -Deploy -NoBuild
```

**O que o script faz:**
1. Lê versão do `Craftimizer.csproj`
2. Build Release configuration
3. Cria diretório `installedPlugins\Craftimizer\{version}\`
4. Copia todos arquivos de `bin/Release/` para o diretório
5. Reporta sucesso/falha

### Método 2: Manual (Step-by-Step)

```powershell
# 1. Build Release
dotnet build Craftimizer/Craftimizer.csproj -c Release --nologo

# 2. Verificar sucesso (exit code 0)
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 3. Ler versão
$csproj = "Craftimizer/Craftimizer.csproj"
$xml = [xml](Get-Content $csproj)
$version = $xml.Project.PropertyGroup[0].Version

# 4. Preparar diretório destino
$pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version"
if (-not (Test-Path $pluginDir)) {
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
}

# 5. Copiar arquivos
Copy-Item "Craftimizer\bin\Release\*" $pluginDir -Force -Recurse

Write-Host "✅ Deploy complete: $pluginDir" -ForegroundColor Green
```

### Método 3: Criação de Package para Distribuição

Para criar ZIP distribuível:

```powershell
# Usar script de package
.\scripts\build-package.ps1

# Ou manualmente:
$version = "2.10.0.2"
$sourceDir = "Craftimizer\bin\Release"
$zipFile = "dist\Craftimizer-$version.zip"

# Criar dist/ se não existir
New-Item -ItemType Directory -Path "dist" -Force | Out-Null

# Comprimir
Compress-Archive -Path "$sourceDir\*" -DestinationPath $zipFile -Force

Write-Host "✅ Package created: $zipFile" -ForegroundColor Green
```

## Estrutura de Arquivos Deployados

Após deploy, o diretório contém:

```
%APPDATA%\XIVLauncher\installedPlugins\Craftimizer\{version}\
├─ Craftimizer.dll              ← Plugin principal
├─ Craftimizer.json             ← Manifesto (ApplicableVersion, DalamudApiLevel)
├─ Craftimizer.Simulator.dll    ← Biblioteca Simulator
├─ Craftimizer.Solver.dll       ← Biblioteca Solver
├─ Microsoft.Data.Sqlite.dll    ← Dependency (macro storage)
├─ Microsoft.Extensions.Caching.Memory.dll  ← Dependency (icon cache)
├─ Raphael.Net.dll              ← Dependency (solver)
├─ raphael.dll                  ← Rust native library
└─ [outros assemblies de dependências]
```

## Verificação de Deploy

### Checklist Pós-Deploy

1. ✅ **Arquivo principal existe**
   ```powershell
   Test-Path "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version\Craftimizer.dll"
   ```

2. ✅ **Manifesto JSON válido**
   ```powershell
   $json = Get-Content "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version\Craftimizer.json" | ConvertFrom-Json
   $json.Name  # Deve ser "Craftimizer"
   ```

3. ✅ **Dependências presentes**
   ```powershell
   Get-ChildItem "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version" | 
       Where-Object { $_.Extension -eq ".dll" } | 
       Measure-Object | Select-Object -ExpandProperty Count
   # Deve ser 8-10 DLLs
   ```

4. ✅ **Versão correta**
   ```powershell
   $dll = [System.Reflection.Assembly]::LoadFile("$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version\Craftimizer.dll")
   $dll.GetName().Version
   # Deve corresponder à versão esperada
   ```

### Teste In-Game (Manual)

Após deploy, iniciar FFXIV com Dalamud e verificar:

1. **Plugin carregado**: `/xlplugins` → "Craftimizer" aparece na lista
2. **Comando funciona**: `/craftimizer` abre janela principal
3. **Editor abre**: `/crafteditor` abre MacroEditor
4. **Overlays funcionam**: Abrir crafting log → overlay aparece

## Troubleshooting

### Erro: "Build failed"

**Sintomas:** `dotnet build` retorna exit code != 0

**Diagnóstico:**
```powershell
# Verificar erros de compilação
dotnet build Craftimizer/Craftimizer.csproj -c Release --verbosity detailed
```

**Soluções comuns:**
- Namespace incorreto após refactor → corrigir usings
- Dependência faltando → restaurar com `dotnet restore`
- SDK incompatível → atualizar .NET SDK

### Erro: "Plugin não carrega in-game"

**Sintomas:** Plugin ausente em `/xlplugins`

**Diagnóstico:**
1. Verificar Dalamud log: `%APPDATA%\XIVLauncher\dalamud.log`
2. Procurar por "Craftimizer" e mensagens de erro

**Soluções comuns:**
- DalamudApiLevel incompatível → atualizar SDK e recompilar
- Dependência faltando → verificar todas DLLs copiadas
- Versão duplicada → remover diretórios antigos

### Erro: "Access denied ao copiar arquivos"

**Sintomas:** Erro de permissão durante `Copy-Item`

**Causa:** Jogo está rodando e XIVLauncher tem lock nos arquivos

**Solução:**
```powershell
# 1. Fechar completamente FFXIV
# 2. Aguardar 5 segundos
Start-Sleep -Seconds 5
# 3. Tentar deploy novamente
.\scripts\build.ps1 -Deploy
```

### Erro: ".NET SDK não encontrado"

**Sintomas:** `dotnet: command not found` ou similar

**Diagnóstico:**
```powershell
# Verificar se dotnet está no PATH
$env:PATH -split ";" | Select-String "dotnet"

# Verificar versão
dotnet --version
```

**Solução:**
```powershell
# Se instalado via Scoop (como neste projeto)
$dotnetPath = "C:\Users\aleja\scoop\apps\dotnet-sdk\current"

# Usar caminho completo temporariamente
& "$dotnetPath\dotnet.exe" build Craftimizer/Craftimizer.csproj -c Release

# Ou adicionar ao PATH da sessão
$env:PATH = "$dotnetPath;$env:PATH"
```

## Otimizações

### Build Incremental

Para builds mais rápidos durante desenvolvimento:

```powershell
# Build apenas o que mudou
dotnet build Craftimizer/Craftimizer.csproj -c Debug

# Sem restore (se já foi feito)
dotnet build Craftimizer/Craftimizer.csproj -c Debug --no-restore

# Sem rebuild de dependências
dotnet build Craftimizer/Craftimizer.csproj -c Debug --no-dependencies
```

### Multi-Configuration Build

Build Debug e Release simultaneamente:

```powershell
# Debug para desenvolvimento local
dotnet build Craftimizer.sln -c Debug

# Release para distribuição
dotnet build Craftimizer.sln -c Release
```

### Clean Build

Quando há problemas de cache:

```powershell
# Limpar todos artifacts
dotnet clean Craftimizer.sln

# Rebuild completo
dotnet build Craftimizer.sln -c Release --no-incremental
```

## Output Esperado

### Build Bem-Sucedido

```
Building Craftimizer 2.10.0.2 (Release)...
  Craftimizer.Simulator net10.0 êxito (0.3s) → Simulator\bin\Release\net10.0\Craftimizer.Simulator.dll
  Craftimizer.Solver net10.0 êxito (0.6s) → Solver\bin\Release\net10.0\Craftimizer.Solver.dll
  Craftimizer net10.0-windows êxito (1.9s) → Craftimizer\bin\Release\Craftimizer.dll

Construir êxito em 3.2s
Build succeeded.
Deploying to C:\Users\aleja\AppData\Roaming\XIVLauncher\installedPlugins\Craftimizer\2.10.0.2 ...
Deploy complete.
```

### Warnings Aceitáveis

- `NU1900` — NuGet vulnerability check falhou (Dalamud feed não suporta)
- `CA1805` — Inicialização redundante (code quality, não crítico)
- `CA1822` — Método pode ser estático (otimização menor)

### Erros Críticos (Requerem Correção)

- `CS0246` — Tipo não encontrado (namespace missing)
- `CS0234` — Namespace não existe (using incorreto)
- `CS0104` — Referência ambígua (alias necessário)

## Integração com CI/CD (Futuro)

Skeleton para GitHub Actions:

```yaml
name: Build and Package Plugin
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build -c Release
      - run: dotnet test
      - run: .\scripts\build-package.ps1
      - uses: actions/upload-artifact@v3
        with:
          name: Craftimizer-${{ github.sha }}
          path: dist/*.zip
```

## Referências

- Script de build: `scripts/build.ps1`
- Script de package: `scripts/build-package.ps1`
- Script de bump: `scripts/bump-version.ps1`
- Documentação de scripts: `scripts/README.md`
- XIVLauncher Plugin Dev Guide: https://dalamud.dev/

## Manutenção da Skill

Atualizar quando:
- Estrutura de diretórios do projeto mudar
- Novo script de build for adicionado
- Novos troubleshooting patterns forem descobertos
