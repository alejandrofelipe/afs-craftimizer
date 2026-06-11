# Craftimizer Build Scripts

Scripts PowerShell para build, versionamento e empacotamento do plugin Craftimizer.

## Scripts Disponíveis

### `build.ps1`
Build, deploy e empacotamento do plugin. Ponto central para todas as operações de build.

**Uso:**
```powershell
.\scripts\build.ps1                          # Build Debug
.\scripts\build.ps1 -Configuration Release
.\scripts\build.ps1 -Deploy                  # Build Release + deploy para XIVLauncher
.\scripts\build.ps1 -Deploy -NoBuild         # Deploy sem rebuild
.\scripts\build.ps1 -Studio                  # Build plugin + Craftimizer.UIStudio
.\scripts\build.ps1 -Bump                    # Bump build number + Build Debug
.\scripts\build.ps1 -Bump -BumpType patch    # Bump patch + Build Debug
.\scripts\build.ps1 -Deploy -Bump            # Bump build + Build Release + Deploy
.\scripts\build.ps1 -Package                 # Build Release + gerar zip em dist/
.\scripts\build.ps1 -Package -NoBuild        # Apenas empacota (sem rebuild)
.\scripts\build.ps1 -Package -PackageOutputDir releases
```

**Parâmetros:**
- `-Configuration`: `Debug` ou `Release` (padrão: `Debug`)
- `-Deploy`: Copia o build para `%APPDATA%\XIVLauncher\installedPlugins\Craftimizer\{version}`; mantém apenas as 2 versões mais recentes
- `-NoBuild`: Pula o build e usa o que já está em `bin\Release`
- `-Studio`: Também builda `Craftimizer.UIStudio` (não referenciado pelo plugin, precisa ser explícito)
- `-Bump`: Bump de versão antes do build (padrão: build number)
- `-BumpType`: Nível do bump — `major`, `minor`, `patch`, `build` (padrão: `build`); só usado com `-Bump`
- `-Package`: Cria arquivo `.zip` em `dist/` (ou `-PackageOutputDir`)
- `-PackageOutputDir`: Diretório de saída do zip (padrão: `dist`)

---

### `bump-version.ps1`
Incrementa a versão no `Craftimizer.csproj`.

**Uso:**
```powershell
.\scripts\bump-version.ps1              # Incrementa build number (padrão)
.\scripts\bump-version.ps1 -Type patch
.\scripts\bump-version.ps1 -Type minor
.\scripts\bump-version.ps1 -Type major
.\scripts\bump-version.ps1 -Set "3.0.0.0"
```

**Parâmetros:**
- `-Type`: `major`, `minor`, `patch`, ou `build` (padrão: `build`)
  - `major` → X.0.0.0
  - `minor` → x.X.0.0
  - `patch` → x.x.X.0
  - `build` → x.x.x.X
- `-Set`: Define versão explícita (ex: `"3.0.0.0"`)

---

## Workflow Típico

### 1. Desenvolvimento
```powershell
# Build e deploy para teste in-game
.\scripts\build.ps1 -Deploy
```

### 2. Preparar Release
```powershell
# Bump patch + build Release + gerar zip
.\scripts\build.ps1 -Package -Bump -BumpType patch
```

### 3. Deploy com bump automático
```powershell
# Bump build number + deploy local
.\scripts\build.ps1 -Deploy -Bump
```

---

## Requisitos

- **PowerShell 7+** (pwsh)
- **.NET 10 SDK** (ou versão especificada em `Craftimizer.csproj`)
- Scripts assumem que o SDK está em PATH ou no caminho Scoop: `C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe`

---

## Estrutura de Saída

```
Craftimizer/
├── dist/                          ← Pacotes .zip (gitignored)
│   └── Craftimizer-v{version}.zip
├── scripts/
│   ├── build.ps1                  ← build, deploy, package, bump
│   └── bump-version.ps1           ← bump de versão isolado
├── Craftimizer/
│   └── bin/Release/               ← Output do plugin (deploy source)
│       ├── Craftimizer.dll
│       ├── Craftimizer.UI.dll
│       ├── ImGui.NET.dll
│       ├── Craftimizer.Simulator.dll
│       ├── Craftimizer.Solver.dll
│       └── ...                    ← cimgui.dll removida pelo MSBuild
└── Craftimizer.UIStudio/
    └── bin/                       ← App desktop standalone (não deployado)
```

---

## Notas

- Pasta `dist/` é ignorada pelo git (veja `.gitignore`)
- Pasta `backlog/` é ignorada pelo git (arquivos de planejamento interno)
- Scripts usam `Set-StrictMode -Version Latest` e `$ErrorActionPreference = "Stop"` para segurança
