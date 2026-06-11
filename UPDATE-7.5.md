# Artificer — Plano de Atualização para FFXIV 7.5

> **Gerado em:** 12/05/2026  
> **Versão atual do plugin:** 2.9.1.1  
> **SDK atual:** Dalamud.NET.Sdk 14.0.1  
> **Alvo:** FFXIV 7.5 / Dalamud.NET.Sdk 15.0.0  

---

## 1. Diagnóstico do Estado Atual

### 1.1 Versões Instaladas

| Componente | Versão Atual | Versão Alvo |
|---|---|---|
| `Dalamud.NET.Sdk` | 14.0.1 | **15.0.0** |
| `Lumina` (via SDK) | bundled 7.x | **7.5.0** (bundled no SDK 15) |
| `FFXIVClientStructs` (via SDK) | bundled | atualizado via SDK 15 |
| `MathNet.Numerics` | 5.0.0 | verificar se há nova versão |
| `DotNext` (Solver) | 5.26.1 | verificar se há nova versão |
| `Raphael.Net` (Solver) | 4.1.0 | verificar se há nova versão |
| Plugin versão | 2.9.1.1 | **2.9.2.0** (após update) |
| .NET Runtime | net10.0 | net10.0 (sem mudança) |

### 1.2 Arquivos `.csproj` a Modificar

| Arquivo | Mudança |
|---|---|
| `Artificer/Artificer.csproj` | Bumpar SDK, `<Version>` |
| `Solver/Artificer.Solver.csproj` | Verificar `DotNext`, `Raphael.Net` |
| `Benchmark/Artificer.Benchmark.csproj` | Verificar `BenchmarkDotNet` |
| `Test/Artificer.Test.csproj` | Verificar `MSTest` |

---

## 2. Categorias de Risco e Breaking Changes Esperados

### 2.1 🔴 ALTO RISCO — FFXIVClientStructs

A cada patch do FFXIV, structs nativas do cliente podem ser renomeadas, movidas ou ter campos alterados. O SDK 15 vem com um novo snapshot de FFXIVClientStructs compatível com o patch 7.5.

**Structs/namespaces utilizados pelo plugin:**

| Namespace / Struct | Arquivo | Risco |
|---|---|---|
| `EventFramework` | `SimulatorUtils.cs` | **ALTO** — muda frequentemente com patches |
| `RecipeNote` (UI struct) | `CSRecipeNote.cs`, `RecipeNote.cs` | **ALTO** — struct da UI de crafting |
| `AddonSynthesis` | `SynthesisValues.cs` | **ALTO** — UI de síntese ativa |
| `PlayerState` | `SimulatorUtils.cs`, `CSRecipeNote.cs` | **MÉDIO** |
| `UIState` | `CSRecipeNote.cs`, `Gearsets.cs` | **MÉDIO** |
| `RaptureHotbarModule` | `Gearsets.cs`, `MacroCopy.cs` | **MÉDIO** |
| `RaptureShellModule` (macros) | `SynthHelper.cs` | **MÉDIO** |
| `AtkValues`/`AtkUnitBase` | `SynthesisValues.cs`, `RecipeNote.cs` | **BAIXO** |
| `InventoryContainer` | `Gearsets.cs` | **BAIXO** |

**Ação:** Após bumpar o SDK, compilar e analisar cada erro de CS (C# compile error) — cada erro é um breaking change de struct.

### 2.2 🟡 MÉDIO RISCO — Lumina Sheets

O Dalamud 15 embarca Lumina 7.5.0 (atualizado ontem — 11/05/2026 segundo commits). Sheets podem ter sido renomeadas ou ter colunas alteradas para refletir o patch 7.5.

**Sheets críticas a verificar:**

| Sheet | Arquivo | Motivo de Risco |
|---|---|---|
| `WKSMissionToDoEvalutionRefin` | `LuminaSheets.cs` | Nome suspeito (possível typo), muito específica do 7.x |
| `GathererCrafterLvAdjustTable` | `LuminaSheets.cs` | Tabela de ajuste de nível — pode mudar |
| `RecipeLevelTable` | `LuminaSheets.cs` | Tabela de dificuldade — pode ter novas entradas |
| `SharlayanCraftWorksSupply` | `RecipeData.cs` | Sheet do conteúdo WKS (Dawntrail) |
| `CollectablesShopRefine` | `RecipeData.cs` | Pode ter sido alterada |
| `Recipe` | `RecipeData.cs` | Receitas novas do 7.5 — verificar campos |

**Ação:** Compilar e checar erros de tipo em `LuminaSheets.cs` e `RecipeData.cs`. Se campos de sheet foram renomeados, o compilador vai apontar diretamente.

### 2.3 🟡 MÉDIO RISCO — Dalamud Services API

O SDK 15 pode introduzir novas interfaces ou deprecar métodos existentes.

**Serviços utilizados a verificar:**

| Serviço | Arquivo | Observação |
|---|---|---|
| `ISeStringEvaluator` | `Service.cs` | Interface relativamente nova — verificar se método mudou |
| `IDalamudAssetManager` | `Service.cs` | Verificar API |
| `IGameInteropProvider` | `Service.cs`, `Hooks.cs` | Usado para hooks — crítico |
| `INotificationManager` | `Service.cs` | Verificar API |

### 2.4 🟢 BAIXO RISCO — IDs Hardcoded

Esses valores raramente mudam, mas devem ser verificados manualmente após o patch.

| Valor | Localização | O que verificar |
|---|---|---|
| `StatusId 48` (Well Fed) | `MacroEditor.cs:65` | Confirmar que o ID do buff de comida não mudou |
| `StatusId 49` (Medicated) | `MacroEditor.cs:67` | Confirmar ID da medicina |
| `StatusId 356` (FC Crafts) | `MacroEditor.cs:69` | Confirmar FC buff |
| `StatusId 357` (FC Control) | `MacroEditor.cs:71` | Confirmar FC buff |
| Addon IDs 226–241 | `SimulatorUtils.cs` | Nomes das condições de crafting |
| Addon IDs 13454–13455 | `SimulatorUtils.cs` | Strings de UI |
| Addon IDs 14200–14215 | `SimulatorUtils.cs` | Strings de UI |
| Quest base `65720 + classJob` | `SimulatorUtils.cs` | Quests de desbloqueio DoH |
| Icon ID `1953` | `SimulatorUtils.cs` | Ícone fallback |

### 2.5 🟢 BAIXO RISCO — Simulator e Solver

Os projetos `Simulator/` e `Solver/` são **pure C#** sem dependências do jogo. Não são afetados por patches do FFXIV, exceto se:
- Novas ações de crafting forem adicionadas no patch 7.5
- A mecânica do sistema de crafting (fórmulas) for alterada

**Verificar:** Patch notes do FFXIV 7.5 para novas ações de crafting ou mudanças em fórmulas.

---

## 3. Ações Necessárias — Checklist Ordenado

### Fase 1: Preparação

- [ ] **Fazer backup / commit do estado atual** antes de qualquer mudança
  ```powershell
  git add -A && git commit -m "chore: pre-7.5 update snapshot"
  ```

- [ ] **Verificar patch notes do FFXIV 7.5** para identificar:
  - Novas ações de crafting
  - Mudanças em mecânicas de crafting
  - Novos tipos de collectables
  - Mudanças no WKS (Wondrous Kiteworks)

- [ ] **Consultar o Discord #plugin-dev do Dalamud** para lista de breaking changes conhecidos do SDK 15

### Fase 2: Atualização do SDK

- [ ] **Bumpar `Dalamud.NET.Sdk`** em `Artificer/Artificer.csproj`:
  ```xml
  <Project Sdk="Dalamud.NET.Sdk/15.0.0">
  ```

- [ ] **Executar build inicial** para obter lista completa de erros:
  ```powershell
  cd C:\Users\aleja\DEV\Artificer
  dotnet build Artificer/Artificer.csproj 2>&1 | Tee-Object -FilePath build-errors.txt
  ```

### Fase 3: Resolver Breaking Changes (por prioridade)

#### 3.1 Erros de FFXIVClientStructs
Para cada erro de compilação em structs nativas:
- [ ] Identificar o struct/campo renomeado usando https://github.com/aers/FFXIVClientStructs
- [ ] Atualizar o acesso nos arquivos afetados
- [ ] Prioridade: `CSRecipeNote.cs` → `SynthesisValues.cs` → `Hooks.cs` → `RecipeNote.cs` → `SimulatorUtils.cs`

#### 3.2 Erros de Lumina Sheets
Para cada erro em acesso a sheets:
- [ ] Verificar se a sheet foi renomeada ou teve campo renomeado
- [ ] Atualizar `LuminaSheets.cs` e/ou `RecipeData.cs`
- [ ] Verificar especialmente `WKSMissionToDoEvalutionRefin`

#### 3.3 Erros de Dalamud API
Para cada erro de interface/método do Dalamud:
- [ ] Consultar https://dalamud.dev/api para a nova assinatura
- [ ] Atualizar em `Service.cs` e nos arquivos que usam o serviço

### Fase 4: Novas Funcionalidades do Patch 7.5

- [ ] **Novas ações de crafting**: Se o patch 7.5 adicionar ações, criar arquivos em `Simulator/Actions/` seguindo o padrão dos existentes (ex: `BasicSynthesis.cs`)
- [ ] **Novos tipos de receitas/collectables**: Atualizar `RecipeData.cs` e potencialmente `LuminaSheets.cs`
- [ ] **Mudanças em stats de crafting**: Atualizar `CharacterStats.cs` no Simulator se fórmulas mudarem

### Fase 5: Validação

- [ ] **Build release limpa**:
  ```powershell
  dotnet build Artificer.sln -c Release
  ```

- [ ] **Executar testes**:
  ```powershell
  dotnet test Test/Artificer.Test.csproj -v normal
  ```

- [ ] **Verificar IDs hardcoded** com uma sessão ativa do jogo após o patch

- [ ] **Testar manualmente** as funcionalidades principais:
  - Abrir MacroEditor com receita válida
  - Executar solver (Raphael e MCTS)
  - SynthHelper durante craft ativo
  - Overlay RecipeNote no crafting log
  - Importar/exportar macro

### Fase 6: Release

- [ ] **Bumpar versão** em `Artificer/Artificer.csproj`:
  ```xml
  <Version>2.9.2.0</Version>
  ```

- [ ] **Atualizar `Artificer.json`** se ApplicableVersion precisar ser ajustada

- [ ] **Commit e push**:
  ```powershell
  git add -A && git commit -m "feat: update for FFXIV 7.5 / Dalamud SDK 15"
  ```

---

## 4. Estimativa de Complexidade

| Fase | Complexidade | Motivo |
|---|---|---|
| Bump SDK + build inicial | Baixa | Trivial, automatizado |
| Resolver FFXIVClientStructs | **Alta** | Exige correlação manual com diff do CS |
| Resolver Lumina sheets | Média | Compilador aponta diretamente |
| Resolver Dalamud API | Baixa-Média | APIs estáveis entre versões menores |
| Novas ações de crafting | Variável | Depende do que o patch adiciona |
| Testes e validação | Média | Requer sessão de jogo ativa |

---

## 5. Recursos de Referência

| Recurso | URL |
|---|---|
| Dalamud SDK NuGet | https://www.nuget.org/packages/Dalamud.NET.Sdk |
| Dalamud API Docs | https://dalamud.dev/ |
| Dalamud GitHub Releases | https://github.com/goatcorp/Dalamud/releases |
| FFXIVClientStructs | https://github.com/aers/FFXIVClientStructs |
| Lumina Releases | https://github.com/NotAdam/Lumina/releases |
| Raphael.Net NuGet | https://www.nuget.org/packages/Raphael.Net |
| FFXIV Patch Notes | https://www.finalfantasyxiv.com/lodestone/topics/ |
| Discord Dalamud Dev | https://discord.gg/3NMcUV5 (#plugin-dev) |
| Plugin original (upstream) | https://github.com/WorkingRobot/Craftimizer |

---

## 6. Notas Adicionais

### Sobre o Lumina 7.5.0
O commit mais recente no repositório do Dalamud (há ~15 horas no momento desta análise) inclui "Upgrade Lumina to 7.5.0", indicando que o SDK 15 está ativamente sendo preparado para o patch 7.5. Monitorar o NuGet para quando `15.x.x` for publicado como estável.

### Sobre o Raphael.Net
O solver externo `Raphael.Net` (versão 4.1.0) é uma biblioteca Rust compilada para Windows x64. Verificar se há nova versão compatível com as novas receitas e mecânicas do 7.5 em https://www.nuget.org/packages/Raphael.Net.

### Sobre o `WKSMissionToDoEvalutionRefin`
Esta sheet Lumina é específica do sistema WKS (Wondrous Kiteworks) introduzido no Dawntrail. O nome contém um possível typo ("Evalution" em vez de "Evaluation"). Verificar no patch 7.5 se a sheet foi renomeada corretamente pela SE.

### Sobre o `packages.lock.json`
O arquivo `Artificer/packages.lock.json` precisa ser atualizado após o bump do SDK:
```powershell
dotnet restore Artificer/Artificer.csproj --force-evaluate
```
