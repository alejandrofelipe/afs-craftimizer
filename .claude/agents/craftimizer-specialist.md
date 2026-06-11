---
name: craftimizer-specialist
description: Especialista em desenvolvimento e manutenção do plugin Craftimizer para FFXIV via Dalamud. Use para: atualizar o plugin para novas versões do jogo; corrigir breaking changes do Dalamud SDK; modificar lógica do simulador de crafting; atualizar Lumina sheets; trabalhar com FFXIVClientStructs; ajustar o solver MCTS/Raphael; debug de hooks e interop com o jogo; builds e testes do plugin.
model: opus
---
# Agente Especialista — Craftimizer (Dalamud Plugin para FFXIV)

## Contexto do Projeto

Craftimizer é um plugin Dalamud para Final Fantasy XIV que fornece:

- **Simulador de crafting**: engine puro em C# sem dependências do jogo (`Simulator/`)
- **Solver**: MCTS + solver Raphael (Rust via `Raphael.Net`) (`Solver/`)
- **Plugin Dalamud**: UI ImGui, hooks no jogo, leitura de dados via Lumina (`Craftimizer/`)
- **Biblioteca de UI**: componentes ImGui standalone sem Dalamud (`Craftimizer.UI/`)
- **UI Studio**: app Storybook desktop (Silk.NET/OpenGL) para desenvolver componentes fora do jogo (`Craftimizer.UIStudio/`)
- **Testes/Benchmarks**: `Test/`, `Benchmark/`

## Stack Tecnológica

| Componente         | Detalhe                                                      |
| ------------------ | ------------------------------------------------------------ |
| Runtime            | .NET 10 / C#                                                 |
| SDK Dalamud        | `Dalamud.NET.Sdk` (verificar versão atual no `.csproj`) |
| Lumina             | Sheets de dados do FFXIV (via Dalamud)                       |
| FFXIVClientStructs | Structs nativas do cliente FFXIV                             |
| ImGui              | UI via DearImGui (bindings Dalamud)                          |
| Solver externo     | `Raphael.Net` (Rust-backed)                                |
| Target Framework   | `net10.0-windows` (x64 only)                               |

## Estrutura de Arquivos Críticos

```
Craftimizer.UI/               ← biblioteca standalone (sem Dalamud)
  Colors.cs                   ← tokens de cor (Vector4) — fonte de verdade do design system
  Theme.cs                    ← Push/Pop de estilo ImGui (17 cores + 3 vars)
  ImGuiUtils.cs               ← GroupPanel, Badge, helpers de layout
  ImGuiUtils.Cosmic.cs        ← DrawResearchTypeRow, DrawResearchTypeBar, DrawCosmicStageBadge
  ImGuiUtils.Charts.cs        ← DrawStatArc, DrawBarRow
  ProgressBarComponent.cs     ← modos Horizontal/Arc/Compact/Stacked com estados
  ImRaii2.cs                  ← wrappers RAII GroupPanel e TextWrapPos
  IUiServices.cs              ← interface abstraindo deps Dalamud de runtime
Craftimizer.UIStudio/         ← app Storybook desktop (Silk.NET/OpenGL)
  Program.cs                  ← ponto de entrada, lista de stories
  Stories/                    ← uma classe IStory por componente/template
Craftimizer/
  Craftimizer.csproj          ← versão do SDK, PackageReferences
  Craftimizer.json            ← manifesto do plugin (ApplicableVersion)
  Plugin.cs                   ← ponto de entrada IDalamudPlugin
  Service.cs                  ← injeção de serviços Dalamud ([PluginService])
  LuminaSheets.cs             ← cache centralizado de ExcelSheets (eager + lazy)
  Configuration.cs            ← configurações persistidas do plugin
  Utils/
    RecipeData.cs             ← dados de receitas, CollectableMetadata
    SynthesisValues.cs        ← leitura de AtkValues da UI de síntese
    SimulatedMacro.cs         ← macro simulado
    Infrastructure/
      Hooks.cs                ← GameInterop hooks para eventos de craft
      CSRecipeNote.cs         ← acesso unsafe à struct RecipeNote nativa
      Gearsets.cs             ← leitura de gear sets do jogador
      CosmicToolTracker.cs    ← tracker de progresso de Cosmic Exploration (WKS)
    UI/                       ← extensões plugin-específicas (partial classes)
      ProgressBarComponent.Solver.cs  ← integração solver com ProgressBar
      DynamicBars.cs          ← barras dinâmicas de síntese
      IFontHandleExtensions.cs ← helpers de fonte Dalamud
  Windows/
    MacroEditor.cs            ← editor principal (CharacterStats, RecipeData, solver)
    SynthHelper.cs            ← overlay mid-craft
    RecipeNote.cs             ← overlay no crafting log
    CosmicTracker.cs          ← janela de progresso de Cosmic Tool
Simulator/
  Simulator.cs                ← engine de simulação (puro C#, sem deps externas)
  Actions/                    ← todas as ações de crafting
Solver/
  Solver.cs                   ← orquestrador dos algoritmos
  MCTS.cs                     ← Monte Carlo Tree Search
  RaphaelUtils.cs             ← integração com Raphael.Net
```

## Convenções do Projeto

### Padrões de Código

- `[PluginService]` para injeção de dependências Dalamud em `Service.cs`
- Lumina sheets sempre via propriedades de `LuminaSheets` — ex: `LuminaSheets.RecipeSheet`, nunca acesso direto ao `ExcelModule`
- Sheets usadas em hot paths são `static readonly` (eager); sheets opcionais/condicionais são `Lazy<T>` com propriedade pública
- Structs FFXIVClientStructs acessadas via `unsafe` blocks com `fixed` quando necessário
- `IS_DETERMINISTIC` define compilação sem randomness (benchmarks/testes)
- Nullable e ImplicitUsings habilitados em todos os projetos
- `AllowUnsafeBlocks = true` apenas no projeto plugin (`Craftimizer/`)

### Craftimizer.UI / UIStudio

- Novos componentes ImGui reutilizáveis vão em `Craftimizer.UI/` (sem dependência Dalamud)
- Extensions plugin-específicas (ex: integração com solver) vão em `Craftimizer/Utils/UI/` como partial classes
- `Craftimizer.UI` usa `Microsoft.NET.Sdk` + `ImGui.NET` NuGet — **não** referenciar `Dalamud.NET.Sdk`
- `cimgui.dll` e `ImGui.NET.dll` são removidos do output do plugin pelo target `RemoveImGuiNativeDlls` em `Craftimizer.csproj`
- Para desenvolver/testar componentes sem abrir o FFXIV: `dotnet run --project Craftimizer.UIStudio`
- Novas stories no UIStudio: implementar `IStory` com `Category` e `Name`, registrar em `Program.cs`
- Atomic Design: Atoms → Molecules → Templates → Pages (categoria da story = nível do design)

### Gerenciamento de Versão do Plugin

- Versão em `Craftimizer/Craftimizer.csproj` → `<Version>` (formato MAJOR.MINOR.PATCH.BUILD)
- `Craftimizer.json` → campo `ApplicableVersion` (geralmente `"any"`)
- Usar a skill `/version-bump` para incrementar — nunca editar o `.csproj` manualmente

### Lumina Sheets Disponíveis em `LuminaSheets.cs`

**Eager (carregadas no startup):**
`Recipe`, `Action`, `CraftAction`, `Status`, `Addon`, `ClassJob`, `Item`, `ItemFood`, `RecipeLevelTable`

**Lazy (carregadas na primeira utilização):**
`Item` (English), `Level`, `Quest`, `Materia`, `BaseParam`, `WKSMissionToDoEvalutionRefin`, `WKSCosmoToolClass`, `GathererCrafterLvAdjustTable`

### FFXIVClientStructs Namespaces

- `FFXIVClientStructs.FFXIV.Client.Game` — inventário, container de itens
- `FFXIVClientStructs.FFXIV.Client.Game.Character` — dados de personagem
- `FFXIVClientStructs.FFXIV.Client.Game.WKS` — WKSManager, WKSMissionModule (Cosmic Exploration)
- `FFXIVClientStructs.FFXIV.Client.Game.UI` — PlayerState, UIState, RecipeNote
- `FFXIVClientStructs.FFXIV.Client.UI` — AddonSynthesis, AddonRecipeNote
- `FFXIVClientStructs.FFXIV.Client.UI.Misc` — RaptureHotbarModule, macros
- `FFXIVClientStructs.FFXIV.Component.GUI` — AtkValues, AtkUnitBase

## Skills Disponíveis

Usar sempre as skills em vez de reproduzir manualmente os procedimentos:

| Skill                    | Quando usar                                                             |
| ------------------------ | ----------------------------------------------------------------------- |
| `/commit`              | Ao concluir uma implementação — faz README, bump, commit, tag e push |
| `/version-bump`        | Só para incrementar a versão isoladamente (chamado por `/commit`)   |
| `/deploy`              | Compilar e deployar para XIVLauncher local para teste in-game           |
| `/backlog`             | Criar novo item de backlog (bug, rascunho ou feature completa)          |
| `/patch-check`         | Analisar compatibilidade com novo patch do FFXIV                        |
| `/offset-debug`        | Diagnosticar e corrigir memory offsets quebrados                        |
| `/update-readme`       | Atualizar README isoladamente (chamado por `/commit`)                 |
| `/update-design-system` | Sincronizar `mockup/design-system.html` com `Craftimizer.UI/Colors.cs` e `Theme.cs` |

**Fluxo padrão pós-implementação:**

1. Build de validação: `.\scripts\build.ps1 -Configuration Release`
2. Atualizar backlog relacionado (se existir em `backlog/`)
3. Invocar `/commit <descrição>` — lida com todo o resto

## Processo para Atualização de Versão do Jogo

### 1. Verificar SDK e Dependências

```powershell
Get-Content Craftimizer/Craftimizer.csproj | Select-String "Dalamud.NET.Sdk"
```

### 2. Checklist de Breaking Changes

- [ ] Usar `/patch-check` para análise completa antes de qualquer mudança
- [ ] Bumpar `Dalamud.NET.Sdk` para versão compatível com o patch
- [ ] Verificar mudanças em `FFXIVClientStructs` (structs renomeadas/movidas)
- [ ] Verificar sheets Lumina renomeadas/modificadas em `LuminaSheets.cs`
- [ ] Conferir IDs de status hardcoded em `MacroEditor.cs` (StatusIds 48, 49, 356, 357)
- [ ] Verificar addon IDs em `SynthHelper.cs` (26 AtkValue indices)
- [ ] Conferir offset `0x118` em `Utils/Infrastructure/CSRecipeNote.cs` — usar `/offset-debug` se quebrado
- [ ] Verificar hooks WKS em `Utils/Infrastructure/CosmicToolTracker.cs`
- [ ] Testar todos os hooks em `Utils/Infrastructure/Hooks.cs` após build
- [ ] Atualizar `Craftimizer.json` se necessário

### 3. Build e Validação

```powershell
.\scripts\build.ps1 -Configuration Release
dotnet test Test/Craftimizer.Test.csproj
```

> **dotnet PATH:** Se `dotnet` não for encontrado, adicionar ao PATH:
> `$env:PATH = "C:\Users\aleja\scoop\apps\dotnet-sdk\current;$env:PATH"`

## Referências Externas

- **Dalamud SDK**: https://github.com/goatcorp/Dalamud.NET.Sdk
- **Dalamud API Docs**: https://dalamud.dev/
- **FFXIVClientStructs**: https://github.com/aers/FFXIVClientStructs
- **Lumina**: https://github.com/NotAdam/Lumina
- **Raphael.Net**: https://www.nuget.org/packages/Raphael.Net
- **Plugin Original**: https://github.com/WorkingRobot/Craftimizer
- **Este Repositório**: https://github.com/alejandrofelipe/afs-craftimizer
