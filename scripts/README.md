# Craftimizer Build Scripts

Scripts PowerShell para build, versionamento e empacotamento do plugin Craftimizer.

## Scripts Disponíveis

### `build.ps1`
Build e deploy do plugin.

**Uso:**
```powershell
.\scripts\build.ps1                     # Build Debug
.\scripts\build.ps1 -Configuration Release
.\scripts\build.ps1 -Deploy             # Build Release + deploy para XIVLauncher
.\scripts\build.ps1 -Deploy -NoBuild    # Deploy sem rebuild
.\scripts\build.ps1 -Studio             # Build plugin + Craftimizer.UIStudio
```

**Parâmetros:**
- `-Configuration`: `Debug` ou `Release` (padrão: `Debug`)
- `-Deploy`: Copia o build para `%APPDATA%\XIVLauncher\installedPlugins\Craftimizer\{version}`
- `-NoBuild`: Pula o build e faz deploy do que já está em `bin\Release`
- `-Studio`: Também builda `Craftimizer.UIStudio` (não é referenciado pelo plugin, precisa ser explícito)

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

### `build-package.ps1`
Gera arquivo `.zip` para distribuição Dalamud.

**Uso:**
```powershell
.\scripts\build-package.ps1             # Build Release + cria zip em dist/
.\scripts\build-package.ps1 -NoBuild    # Apenas empacota (sem rebuild)
.\scripts\build-package.ps1 -OutputDir "releases"
```

**Parâmetros:**
- `-NoBuild`: Pula o build e empacota o que já está em `bin\Release`
- `-OutputDir`: Diretório de saída (padrão: `dist`)

**Saída:**
- Arquivo: `dist/Craftimizer-v{version}.zip`
- Inclui: Todos os arquivos de `bin\Release` (DLLs, JSON, dependências, pasta `win-x64/`)

---

## Workflow Típico

### 1. Desenvolvimento
```powershell
# Build e deploy para teste in-game
.\scripts\build.ps1 -Deploy
```

### 2. Preparar Release
```powershell
# Bumpar versão patch (ex: 2.9.4.31 → 2.9.5.0)
.\scripts\bump-version.ps1 -Type patch

# Build e gerar zip para distribuição
.\scripts\build-package.ps1
```

### 3. Deploy Manual
```powershell
# Build Release + deploy local
.\scripts\build.ps1 -Configuration Release -Deploy
```

---

## Requisitos

- **PowerShell 5.1+** ou **PowerShell Core 7+**
- **.NET 10 SDK** (ou versão especificada em `Craftimizer.csproj`)
- Scripts assumem que o SDK está em PATH ou no caminho Scoop: `C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe`

---

## Estrutura de Saída

```
Craftimizer/
├── dist/                          ← Pacotes .zip (gitignored)
│   └── Craftimizer-v{version}.zip
├── scripts/                       ← Scripts de build
│   ├── build.ps1
│   ├── bump-version.ps1
│   └── build-package.ps1
├── Craftimizer/
│   └── bin/Release/               ← Output do plugin (deploy source)
│       ├── Craftimizer.dll
│       ├── Craftimizer.UI.dll     ← biblioteca de UI standalone
│       ├── Craftimizer.Simulator.dll
│       ├── Craftimizer.Solver.dll
│       └── ...                    ← cimgui.dll e ImGui.NET.dll removidos pelo MSBuild
└── Craftimizer.UIStudio/
    └── bin/                       ← App desktop standalone (não deployado)
```

---

## Notas

- Pasta `dist/` é ignorada pelo git (veja `.gitignore`)
- Pasta `backlog/` é ignorada pelo git (arquivos de planejamento interno)
- Scripts usam `Set-StrictMode -Version Latest` e `$ErrorActionPreference = "Stop"` para segurança
