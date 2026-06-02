---
name: atualizar-craftimizer
description: >
  Atualiza o plugin Craftimizer para uma nova versão do FFXIV / Dalamud SDK.
  Inclui bump de dependências, análise de breaking changes, atualização de Lumina sheets
  e FFXIVClientStructs, validação de build e testes.
---

# Atualizar Craftimizer para nova versão do FFXIV/Dalamud

## Parâmetros
- **$VERSAO_JOGO**: Versão do FFXIV alvo (ex: 7.5)
- **$SDK_VERSION**: Versão do Dalamud.NET.Sdk alvo (ex: 15.0.0)

## Passos

### 1. Verificar estado atual
- Ler `Craftimizer/Craftimizer.csproj` para SDK atual e dependências
- Ler `Craftimizer/Craftimizer.json` para versão e ApplicableVersion

### 2. Atualizar SDK
- Bumpar `Dalamud.NET.Sdk` para `$SDK_VERSION` em `Craftimizer.csproj`
- Consultar https://dalamud.dev/ e https://github.com/goatcorp/Dalamud/releases para breaking changes

### 3. Tentar build inicial
```powershell
dotnet build Craftimizer/Craftimizer.csproj 2>&1
```
Listar todos os erros de compilação — esses são os breaking changes a resolver.

### 4. Resolver breaking changes por categoria

#### FFXIVClientStructs
- Verificar structs renomeadas: `grep_search` por nome antigo
- Consultar https://github.com/aers/FFXIVClientStructs para diff

#### Lumina Sheets  
- Verificar sheets em `LuminaSheets.cs` que foram renomeadas ou tiveram campos alterados
- Especialmente `WKSMissionToDoEvalutionRefin` (Dawntrail-specific)

#### Dalamud Services API
- Verificar interfaces `IDataManager`, `IGameGui`, etc. que possam ter mudado
- Verificar `ISeStringEvaluator` (adicionado em API recente)

### 5. Verificar IDs hardcoded
- Status IDs em `MacroEditor.cs` (48, 49, 356, 357)
- Addon IDs em `SimulatorUtils.cs` (226-241, 13454-13455, 14200-14215)
- Quest base ID em `SimulatorUtils.cs` (65720 + classJob)

### 6. Build final e testes
```powershell
dotnet build Craftimizer.sln -c Release
dotnet test Test/Craftimizer.Test.csproj
```

### 7. Atualizar versão do plugin
- Incrementar `<Version>` em `Craftimizer.csproj`
- Atualizar `Craftimizer.json` se necessário
