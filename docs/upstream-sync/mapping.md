# De-Para: Artificer (fork) ↔ Craftimizer (upstream)

Mapeamento estrutural entre este fork (**Artificer**, `alejandrofelipe/afs-craftimizer`) e o
projeto original (**Craftimizer**, `WorkingRobot/Craftimizer`). Use para localizar, ao trazer
mudanças do upstream, **onde cada coisa vive em cada projeto**.

- **Upstream:** https://github.com/WorkingRobot/Craftimizer (branch `main`)
- **Regra geral de namespace:** todo `Craftimizer.*` → `Artificer.*` (namespaces, pastas, projetos, `.sln`).
- **Estrutura verificada em:** 2026-06-26 (árvore do upstream via GitHub API + leitura local do fork).

> O core de simulação/solver é praticamente 1:1 (só rename). A divergência grande está no
> **projeto plugin** (reorganizado em camadas) e na **UI** (extraída para projetos novos).

---

## Projetos

| Upstream (Craftimizer) | Fork (Artificer) | Relação |
|---|---|---|
| `Craftimizer/` — plugin principal (`Dalamud.NET.Sdk`) | `Artificer/` (`Artificer.csproj`) | Rename + **reestruturação pesada**. `Utils/` virou `Utils/{Infrastructure,Application,UI}`; novas camadas `Application/`, `Data/`, `Models/`. Passa a referenciar `Artificer.UI`. |
| `Simulator/` (`Craftimizer.Simulator.csproj`) | `Artificer.Simulator/` | **Rename apenas.** Mapeamento ~1:1 (Condition, Effects, Simulator, Recipe, SimulationState/Input, CharacterStats, `Actions/*`). |
| `Solver/` (`Craftimizer.Solver.csproj`) | `Artificer.Solver/` | **Rename apenas.** 1:1 (MCTS, MCTSConfig, ArenaBuffer, NodeScoresBuffer, RaphaelUtils, SimulationNode, Solver, SolverConfig). |
| `Test/` (`Craftimizer.Test.csproj`) | `Artificer.Test/` | Rename + **expansão** (testes novos de UI, CraftingLists, ImRaiiShim, UIServices). |
| `Benchmark/` (`Craftimizer.Benchmark.csproj`) | `Artificer.Benchmark/` | Rename apenas (Bench.cs, Program.cs). |
| — (helpers de UI viviam dentro de `Craftimizer/`) | `Artificer.UI/` (`Artificer.UI.csproj`) | **Novo.** Lib de UI standalone, **sem Dalamud** (só `ImGui.NET`). |
| — | `Artificer.UIStudio/` | **Novo.** Host desktop (Veldrid/ImGui) para renderizar componentes/janelas sem abrir o FFXIV. |
| `Craftimizer.sln` | `Artificer.sln` | Renomeada; inclui os 2 projetos novos. |

---

## Arquivos / temas-chave (onde achar cada coisa)

| Tema | Upstream | Fork |
|---|---|---|
| **Engine de simulação** | `Simulator/Simulator.cs`, `SimulatorNoRandom.cs` | `Artificer.Simulator/Simulator.cs`, `SimulatorNoRandom.cs` |
| **Condition** (enum/lógica de condição de craft) | `Simulator/Condition.cs` | `Artificer.Simulator/Condition.cs` |
| **Actions** (todas as ações + base) | `Simulator/Actions/*.cs` | `Artificer.Simulator/Actions/*.cs` (mesmo conjunto) |
| **Solver / MCTS** | `Solver/{MCTS,MCTSConfig,Solver,SolverConfig}.cs` | `Artificer.Solver/{MCTS,MCTSConfig,Solver,SolverConfig}.cs` |
| **RaphaelUtils** (ponte Raphael.Net) | `Solver/RaphaelUtils.cs` | `Artificer.Solver/RaphaelUtils.cs` (usa alias `using Action = Raphael.Action;`) |
| **LuminaSheets** (acesso a sheets) | `Craftimizer/LuminaSheets.cs` | `Artificer/LuminaSheets.cs` (raiz, só renomeou namespace) |
| **Hooks** (hooks de função do jogo) | `Craftimizer/Utils/Hooks.cs` | `Artificer/Utils/Infrastructure/Hooks.cs` |
| **RecipeNote / CSRecipeNote** (detecção da receita ativa) | `Craftimizer/Utils/CSRecipeNote.cs` + `Windows/RecipeNote.cs` | `Artificer/Utils/Infrastructure/CSRecipeNote.cs` + **janela reestruturada** em `Windows/CraftingHelper.cs` / `SynthesisHelper.cs` + `Settings.RecipeNote.cs` |
| **Janelas** (UI do plugin) | `Craftimizer/Windows/{MacroEditor,Settings,SynthHelper,MacroList,RecipeNote,…}.cs` | `Artificer/Windows/*` — monolitos quebrados em partials (`MacroEditor.Solver/.Character/…`, `Settings.General/.Solver/…`); `SynthHelper`→`SynthesisHelper`, `MacroList`→`MacroLibrary`; + janelas só-do-fork |
| **ImGuiUtils** (helpers ImGui) | `Craftimizer/ImGuiUtils.cs` (arquivo único) | `Artificer.UI/ImGuiUtils*.cs` (partials por domínio) + `Artificer/Utils/UI/PluginImGuiUtils.*.cs` (lado Dalamud) |

---

## Só existe no fork (sem contraparte upstream)

| Fork | Propósito |
|---|---|
| `Artificer.UI/` | Lib de componentes ImGui **sem Dalamud** (só `ImGui.NET`): ImGuiUtils, Colors, Theme, ProgressBarComponent, FuzzyMatcher, ImRaii2, `ImRaiiShim`, shim de `FontAwesomeIcon`, abstração `IUiServices`. |
| `Artificer.UIStudio/` | Harness de dev que renderiza componentes e "stories" de janelas (`Stories/`, `Stories/Pages/`) sem o jogo. `StubUiServices` substitui serviços Dalamud. |
| `Artificer/Utils/Infrastructure/` | Utilidades de integração com o jogo (realocadas do `Utils/` flat do upstream): Hooks, CSRecipeNote, Gearsets, FoodStatus, IconManager, Ipc, Chat, SynthesisValues + só-do-fork `CosmicToolTracker`, `GearWearTracker`. |
| `Artificer/Utils/UI/` | Helpers de UI **dependentes de Dalamud** (não podem ir pro `Artificer.UI` Dalamud-free): `PluginImGuiUtils.*`, overloads plugin de ImGuiUtils, DynamicBars, ProgressBarComponent.Solver, SqText. |
| `Artificer/Utils/Application/` | Helpers de aplicação: CommunityMacros, MacroCopy, MacroImport. |
| `Artificer/Application/` (`Crafting/`, `CraftingLists/`) | Camada de domínio nova: `CraftingSession` + feature completa de listas de craft (CraftingListManager, IngredientResolver, InventoryScanner, MarketboardHelper, GatheringRoutePlanner, TeleportHelper, …). |
| `Artificer/Data/` + `Artificer/Models/` | Persistência/model: `CraftingListRepository` (Data/), `Macro` + `MacroRepository`/`IMacroStore` (Models/). Upstream guardava macros só em Configuration/SimulatedMacro. |
| `Artificer/Windows/CraftingList*.cs`, `CosmicTracker.cs`, `CraftingHelper.cs` | Janelas só-do-fork: gestão de listas de craft, tracker da Cosmic Exploration, overlay de crafting reestruturado. |

---

## Caveats

1. **Namespace rename total:** todo `namespace Craftimizer.*` → `Artificer.*`. Ao portar um arquivo do
   upstream, ajustar namespace + `using`s; o resto costuma ser literal (mesmas APIs do `Raphael.*`,
   `ImGui.NET`, etc.).
2. **Artefatos `obj/` stale:** arquivos gerados sob `*/obj/` ainda carregam a identidade antiga
   (`Craftimizer.UI.AssemblyInfo.cs`, `Craftimizer.Simulator.GlobalUsings.g.cs`). São **lixo de build
   pré-rename**, não fonte de verdade — ignorar nos diffs/mapping.
3. **`RecipeNote.cs` do upstream não tem 1:1 no fork** — foi reestruturado em `CraftingHelper.cs` /
   `SynthesisHelper.cs` (`SynthHelper`→`SynthesisHelper`) + partial `Settings.RecipeNote.cs`.
4. **Convenção de nomes de pasta:** upstream nomeia pastas pelo conteúdo (`Simulator/`, `Solver/`,
   `Benchmark/`) com csproj `Craftimizer.X`; o fork nomeia a pasta pelo assembly (`Artificer.Simulator/`,
   `Artificer.Benchmark/`).
5. **Áreas grandes só-do-fork** (sem upstream): listas de craft, Cosmic Exploration tracker, gear-wear
   tracking — mudanças upstream nessas áreas **não existem** (não há o que sincronizar).
