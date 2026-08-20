# Artificer

**Fork mantido por:** alejandrofelipe  
**Autor original:** Asriel (WorkingRobot)  
**Repositório original:** https://github.com/WorkingRobot/Craftimizer  
**Versão atual:** 2.30.4.0 · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0

---

Plugin Dalamud para FFXIV que otimiza macros de crafting usando simulação e busca heurística. Gera automaticamente sequências de ações que maximizam HQ%, analisa receitas em tempo real e oferece sugestões durante a síntese ativa.

## Estrutura do Projeto

```
Artificer/             ← Plugin Dalamud (entry point, janelas, hooks, serviços)
Artificer.UI/          ← Biblioteca de UI compartilhada (sem Dalamud, ImGui.NET direto)
Artificer.UIStudio/    ← App desktop standalone para testar UI sem o FFXIV rodando
Artificer.Simulator/   ← Lógica de simulação de crafting (puro C#, sem dependências externas)
Artificer.Solver/      ← Algoritmos MCTS, genético e Raphael
Artificer.Test/        ← 260 testes cobrindo Simulator, Solver e UI
Artificer.Benchmark/   ← Benchmarks de performance do solver
```

### Artificer.UI

Biblioteca standalone (sem Dalamud) com todos os componentes ImGui reutilizáveis:

- `ImGuiUtils` — utilitários de layout, badges, empty state, barras de progresso, arcos, SearchableCombo
- `ProgressBarComponent` — componente robusto com modos Horizontal / Arc / Compact / Stacked, estados e tooltips ricos
- `ImRaii2` — wrappers RAII para GroupPanel e TextWrapPos
- `Theme` / `Colors` — paleta de cores e estilo escuro do plugin
- `IUiServices` / `UiServices` — interface que abstrai as dependências Dalamud de runtime (fonts, scale, links)

Pode ser referenciada por qualquer projeto .NET sem precisar de Dalamud instalado.

### Artificer.UIStudio

App desktop standalone (Silk.NET + OpenGL + GLFW) estilo Storybook para desenvolver e inspecionar componentes visuais sem precisar abrir o FFXIV.

**Como executar:**
```powershell
dotnet run --project Artificer.UIStudio
```

**Stories disponíveis:**

| Categoria | Story | O que mostra |
|---|---|---|
| Atoms | Colors | Swatches de todas as cores da paleta com nome e hex |
| Atoms | Theme | Controles comuns (botões, inputs, combo, progress bar, GroupPanel, badges) com tema aplicado |
| Molecules | Empty State | 4 variantes: só título, com subtítulo, 1 botão, 2 botões |
| Molecules | Progress Bar | Todos os estados, modos, temas de cor, agregado e slider interativo |
| Molecules | Charts | `DrawStatArc`: tamanhos, frações, cores de stat, rings concêntricos e slider interativo |
| Molecules | Bars | `DrawBarRow`: variações de valores, overflow, dados reais de crafting |
| Templates | Tabbed Window | BeginTabBar + GroupPanel + DrawBarRow (ex: MacroEditor, Settings) |
| Templates | List Window | Search input + lista scrollável + DrawEmptyState (ex: CraftingListWindow) |
| Templates | Stat Dashboard | DrawBarRow arcos + ProgressBarComponent horizontal, slider interativo |
| Templates | Floating Overlay | `DrawResearchTypeRow` em 4 estados × 2 modos (ex: CosmicTracker) |
| Templates | Dialog | 3 variantes lado a lado: informativa, confirmação, destrutiva (danger button) |
| Templates | Single Panel | GroupPanel sem footer e com footer + AlignRight (ex: RecipeNote, MacroClipboard) |

---

## Funcionalidades do Plugin

### Macro Editor
Editor completo de macros com simulação offline. Permite selecionar qualquer receita, configurar stats do personagem (Craftsmanship, Control, CP, nível), definir quantidade de HQ ingredientes e gerar a sequência de ações ideal via solver. Exibe porcentagem estimada de HQ, taxa de sucesso e step-by-step visual das ações.

### Recipe Note
Overlay integrado ao Crafting Log do jogo. Enquanto você navega nas receitas, exibe o macro ótimo e as estatísticas estimadas para cada receita. Suporta badges visuais para receitas Expert, Collectible, Splendorous, Specialist e Cosmic Exploration.

### Synthesis Helper
Assistente em tempo real durante a síntese ativa. Mostra a próxima ação recomendada, barras de progresso de Progress/Quality/Durability/CP com arcos circulares animados, indicador de condição (Normal/Good/Excellent/Poor) e progresso de execução do macro atual.

### Macro Library
Biblioteca de macros salvos com busca, filtro, importação/exportação e integração com macros da comunidade.

### CosmicTracker
Janela flutuante com todos os 7 tipos de research (Type I–VII): barras de progresso individuais com marcador de threshold de upgrade, modo compacto, filtro "ocultar concluídos", destaque de delta por 10 s e auto-refresh a cada 5 s. Botão estrela ★ nas janelas principais abre/fecha o tracker rapidamente.

### Lista de Coleta
Gestão de listas de materiais para crafting em lote. Inclui busca de receitas, resolução recursiva de ingredientes com consciência de inventário, preços de mercado (Universalis), teleporte integrado e exportação para texto/clipboard.

---

## Solver

O solver usa **Monte Carlo Tree Search (MCTS)** com múltiplas variantes configuráveis:

| Algoritmo | Descrição |
|---|---|
| OneShot | Passagem única (mais rápido) |
| OneshotForked | Exploração paralela de árvores |
| Stepwise | Uma ação por vez (suporta mid-craft) |
| StepwiseForked | Stepwise com paralelismo |
| **StepwiseGenetic** | Padrão — otimização genética + MCTS |
| Raphael | Pathfinding A* determinístico |
| NextActionForked | Avalia cada próxima ação via MCTS em paralelo; ideal para Synthesis Helper |

A função de score é multi-objetivo com pesos configuráveis:

- Quality (padrão: 80) — prioridade máxima, HQ%
- Progress (10), Steps (5), CP (3), Durability (2)

Parâmetros configuráveis: iterações (até 1.500.000), constante de exploração UCB, número de threads, max steps e pesos de score.

---

## Diferenças deste Fork

- Compatibilidade verificada com FFXIV 7.51+
- **`Artificer.UI`** — biblioteca de UI extraída para projeto standalone sem Dalamud; permite testes de componentes fora do jogo
- **`Artificer.UIStudio`** — app Storybook standalone (Silk.NET/OpenGL) para desenvolver e inspecionar componentes visuais sem o FFXIV rodando
- Barras de progresso redesenhadas (`ProgressBarComponent` com modos Horizontal/Arc/Compact/Stacked e progresso agregado)
- Cache de ícones migrado para `IMemoryCache` com eviction configurável
- Reorganização estrutural do projeto para layout .NET padrão
- Melhorias de qualidade de código: arquivos parciais, constantes para magic numbers, thread-safety em `Configuration.Save()`, error handlers em fire-and-forget tasks
- 0 build warnings
- Corrigido `ObjectDisposedException` em `RecipeNote` ao reabrir o Crafting Log
- CosmicTracker atualiza instantaneamente ao trocar de job
- 🐛 Fix: crash `C0000005` em `Theme.Push()` — plugin não deve shipar `cimgui.dll` pois o Dalamud fornece o contexto nativo; DLL duplicada causava `GImGui == NULL`
- 🐛 Fix: crash ao abrir Settings — `ImRaii.PushStyle` e `BeginGroupPanel` usavam valores ImGuiNET para `ImGuiStyleVar` (Dalamud moveu `DisabledAlpha` do índice 1 para 24, deslocando todos os outros -1); `ImRaiiShim` agora remapeia automaticamente via `ConfigureForDalamud()`
- 🐛 Fix: busca de receita (adicionar à Lista de Coleta) exibia a lista de resultados em branco — o `ImGuiListClipper` do ImGui.NET é incompatível com o cimgui do SDK 15; substituído por virtualização manual. A lista agora aparece em **colunas alinhadas** (ícone · nome · job · nível), com nomes longos truncados e sem overflow
- 🐛 Fix: suporte à condição **Robust** (FFXIV 7.41) no simulador/solver — modela corretamente a chance, a transição para Sturdy e o desconto de durabilidade pela metade; antes era tratada como Normal, gerando macros/cálculos incorretos em receitas expert 7.41+
- ⬆️ Solver óptimo atualizado para **Raphael.Net 5.0.0** + suporte às ações Stellar/Cosmic (RapidSynthesis, HastyTouch, DaringTouch) no pool enviado ao solver
- ⚙️ Solver com **objetivo de scoring lexicográfico** (paridade com o upstream): completar → quality até o alvo → menos passos. Durabilidade e CP saem do objetivo (não "enchem" mais o fim do craft); painel "Score Weights" removido e substituído por um único **Quality Target %**. Crafts de Cosmic Exploration deixam de ser limitados pelo cap de collectability
- 🐛 Fix: plugin não carregava com Dalamud SDK 15+ — `ImGui.NET.dll` foi removida do runtime do Dalamud; agora shipada com o plugin (wrapper gerenciado que P/Invoca no `cimgui.dll` nativo do Dalamud)
- Configuração de **Quality Target %**: slider 0–100% para limitar o alvo de qualidade
- Novo solver **Next Action Forked**
- Sync com upstream v2.11 (fix GC corruption, fix crash por RNG compartilhado, solver não gera ações supérfluas após progress completo)
- Aba **"Experimental"** nas Settings com banner de aviso
- Confirmação inline para ações destrutivas (remover receita, limpar Gear Wear)
- Reorganização de conteúdo das Settings por contexto de uso
- Empty states nas janelas que ficavam em branco
- **Lista de Coleta completa** (P0–P2): dados (SQLite, InventoryScanner, IngredientResolver), helpers (busca, restrições, coleta, mercado, exportação) e UI completa (List, Add, Detail, Merge windows)
- 🐛 Fix: "Suggested Macro" exibia card confuso com arcos de estatística e botão Copy inutilizável quando o solver retornava 0 ações; agora exibe estado de erro claro com botão "Suggest Again"
- 🐛 Fix: `MCTS.Solution()` retornava lista vazia porque `AvailableActions.PopRandom` (chamado durante a busca) esgotava o `ActionSet` do nó raiz, fazendo `IsComplete` ser `true` por `NoMoreActions`; corrigido usando `SimulationCompletionState` diretamente
- 🐛 Fix: indicator de carregamento ausente no MacroEditor ao regenerar macro com resultado anterior salvo (Caso A — primeira geração da sessão); snapshot `Indeterminate` agora emitido imediatamente ao iniciar o solver
- 🐛 Fix: layout shift no MacroEditor ao iniciar sugestão de macro com lista vazia — altura da área de progresso agora é reservada com 2 frames fixos independentemente de o solver estar rodando
- 🐛 Fix: barra de progresso do solver no SynthesisHelper ficava presa em "Solving..." após o solver encontrar steps suficientes — snapshot agora marcado como Completed antes do cancelamento automático
- 🐛 Fix: macro salva continuava marcada como "incompatível com os stats atuais" no CraftingHelper mesmo após ser atualizada com os stats correntes — `CharacterStatsHash` agora atualizado junto com actions e score no auto-save
- Revamp visual do progress component no MacroEditor (sugestão de macro): chip de estado + stage dots animados por DrawList (wave animation no estado indeterminate, pulse no estágio atual, cores por estágio) + nome do algoritmo right-aligned na mesma linha
- Gear condition no CraftingHelper redesenhado com componente `DrawAlert` consistente com o SynthesisHelper; lógica de mensagem compartilhada entre ambas as janelas via `PluginImGuiUtils.BuildGearMessage`
- 🐛 Fix: componentes vazando a borda de GroupPanels — `BeginGroupPanel` injeta sua largura interna como "largura ambiente" e os helpers de alinhamento clampam a `min(painel, célula)` em vez de `GetContentRegionAvail()` (que escapa até a borda da janela); label do algoritmo no progresso do solver ancorado à borda do painel
- 🐛 Fix: crash ao adicionar receita na Lista de Coleta (`EntryPointNotFoundException: igIsKeyPressed_Bool`) — `ImGui.IsKeyPressed` do ImGuiNET não existe no cimgui do Dalamud; a chamada agora roteia pelo shim `IUiServices` (`Dalamud.Bindings.ImGui` in-game, `ImGuiNET` no UIStudio), mesmo padrão do `PushStyleVar`
- **Entrada da Lista de Coleta repensada:** comando `/craftlist` (+ `/craftinglist`, `/coleta`) e botão em Configurações → Experimental abrem a janela; o FeatureHub flutuante (e o helper `DrawGuard`) foram removidos
- **Lista de Coleta — passe de intuitividade visual:** ícones de item em todas as superfícies (materiais, cristais, pré-crafts, receitas e busca via `PluginImGuiUtils.DrawItemIcon`); layout em 2 linhas (material com `tem/precisa/faltam` + barra + teleporte/zona/preço; header com toggle `[Detalhada|Simples]` + kebab "Mais opções" + barra de progresso; browser com barra + `N receitas`); 🐛 Fix: Detalhe atualiza ao adicionar receita de outra janela (assina `ListsChanged`) e o ✕ da receita não estoura mais a borda do painel
- **Lista de Coleta — Rota de Coleta:** nova janela (kebab → "🗺 Abrir Rota de Coleta") que agrupa os materiais base faltantes **por zona**, com teleporte por zona, **flag no mapa** por nó (coords reais via `ExportedGatheringPoint`) e preço de Market Board por unidade; grupo "Comprar / Outros" para itens sem nó de coleta; botão 📍 também na linha de material do Detalhe
- **CraftingHelper — card unificado "Best Macro":** os cards separados "Best Saved Macro" e "Suggested Macro" foram fundidos num único card que mostra automaticamente a macro de **maior score** (paridade com o `CalculateScore` usado nas salvas) e revela a alternativa num **rodapé de comparação** com toggle in-place ("View saved/suggested"). Faz **fallback para a macro salva** quando o solver não supera/não gera, exibe a exceção do solver quando ela ocorre, e o rodapé é honesto (marca `✗` quando a macro não completa o craft, `—` em receita sem quality). O card **Best Community Macro** permanece separado. Componente de seleção puro (`ImGuiUtils.PickBestMacroSource`) com testes; story do UIStudio cobrindo os estados do card
- **CosmicTracker — sigla do job no título:** o título da janela passa a mostrar a **sigla** do job (ex.: `WVR — Stage 3/5`) em vez do nome completo
- **SynthesisHelper — layout compacto:** reestruturação para reduzir a altura da janela — stats em **barras horizontais em 2 colunas** (no lugar dos arcos), painel de **buffs inline** (some quando não há buff ativo), **ícones de ação menores**, **botões lado a lado** e o progresso do solver reusando o componente compartilhado (`DrawSolverProgressArea`, dedup)
- 🐛 Fix + legibilidade das progress bars: (1) as barras 2-col do Synthesis Helper voltaram a **colorir** — o preenchimento é colorido direto por `Vector4`, não pelo slot `ImGuiCol.PlotHistogram` que tinha índice divergente entre os bindings Dalamud/ImGui.NET (bug só in-game na 2.28.0.0); (2) o texto do overlay das barras agora **se adapta ao fundo** via `Colors.ContrastText` + render duotone (contraste-com-fill na parte cheia, contraste-com-FrameBg na parte vazia) — legível em qualquer % de preenchimento
- 🔧 Fix + feat: **fluxos de macro (busca e auto-save) revisados** — score **unificado** ao objetivo do solver (a % na biblioteca passa a refletir a proximidade do **quality-target**, correta para collectables, em vez de fração da quality máxima); **auto-save não-destrutivo** via flag `Source` (User/Auto) + migration SQLite **V3**, que **nunca sobrescreve** macros que você criou/importou (cria/atualiza só a macro `Auto` da receita); a busca do card **filtra por `RecipeId`** e lê um **snapshot seguro** da lista (corrige uma corrida de concorrência com o `MacroLibrary`), e só promove no card uma macro que **completa** a receita. Decisões puras testadas (`MacroScoring`, `MacroSelection`); auto-save com `try/catch` (não perde retry em falha de escrita)
- 🔒 Segurança: **SQLite nativo atualizado para 3.53.x** (override do pacote `lib.e_sqlite3`, sobrepondo o 2.1.10 transitivo do `Microsoft.Data.Sqlite`) — resolve **CVE-2025-6965** e **CVE-2025-70873**; o `NoWarn NU1903` foi removido (o advisory é corrigido de verdade)
- ♻️ Refactor: o ciclo de execução do solver foi deduplicado num componente único **`SolverRun`** (poller de snapshot + early-stop + cancel), compartilhado por MacroEditor e SynthHelper; e a resolução de delineations virou `SolverConfig.ForDelineations`. Sem mudança de comportamento
- 🗑 Removida a janela **`/meldguide`** (guia de melding) do fork
- 🐛 Fix: o **auto-save** da macro de craft não ficava com o nome do item — o fallback só tratava `null`, então um nome vazio do item deixava a macro sem nome. Agora usa `MacroSelection.ResolveMacroName` (vazio/whitespace → `"Recipe {id}"`) e **auto-cura** o nome no overwrite quando o item resolve e o nome atual é um fallback ruim (sem sobrescrever um nome renomeado à mão)
- 🐛 Fix: o card **"Best Macro"** mostrava `"AI Suggestion"` no lugar do nome do item quando a sugestão viva do solver vencia (macro ainda não salva). Agora exibe o **nome do item craftado**; o badge **`✦ Suggested`** no topo do card continua marcando que é uma sugestão
- 🐛 Fix (P1): **reentrância entre gerações do solver** — cancelar um cálculo e iniciar outro imediatamente não deixa mais a execução antiga cancelar, marcar como concluída ou sobrescrever o estado visível da nova (ações efêmeras no MacroEditor, progresso no Recipe Note). Isolamento por **geração monotônica**: CTS local por `Run` (capturado pelos callbacks), sealing do snapshot terminal, guards de publicação de `Current`/snapshots/`BestMacroSolver`, e descarte determinístico de cada CTS. Contrato de geração testado sem `Thread.Sleep`
- 🐛 Fix (P1): **concorrência e comparação de preços de mercado** — fim da corrida entre a carga de preços (retomada em thread pool após `ConfigureAwait(false)`) e o `Draw()`/`SqliteConnection`: pipeline **por geração** com publicação **atômica na framework thread** e cancelamento no refresh, troca de lista e dispose. Além disso, a **economia no data center** volta a aparecer — `PriceCurrentServer` (menor no mundo atual) vs `PriceCheapestServer` (menor no DC), que antes recebiam o mesmo valor e escondiam a comparação
- 🐛 Fix (P2): **crash do solver com pool completo de ações** — `ArenaBuffer`/`NodeScoresBuffer` alocavam capacidade fixa (32) sem validar; com o pool completo (40 ações) e `StrictActions` desligado, a expansão do 33º filho no MCTS estourava `IndexOutOfRangeException`. Agora os buffers **crescem dinamicamente** (batches de 8 preservados pro caminho SIMD), com a capacidade reservada antes de qualquer incremento (filhos e scores nunca divergem). Coberto por testes nas fronteiras 32/33/40/65 + integração MCTS
- 🐛 Fix (P2): **progresso incorreto ao mover receitas entre listas** — o move inferia o progresso pelos IDs de **produto** das receitas, mas `material_progress` é indexado por **ingrediente**, então o destino perdia o progresso e a origem ficava com linhas órfãs (e mover pra uma lista que já tinha a receita duplicava). Agora o move **planeja** o estado final das receitas, **reconcilia** o progresso das duas listas pela árvore de ingredientes de cada uma (soma duplicatas, remove órfãos) e **persiste tudo numa única transação** (rollback em falha); a conclusão é re-avaliada após o move. `SyncWithInventoryAsync` também passou a remover órfãos. Componentes puros (`CraftingListMovePlanner`, `MaterialProgressReconciler`) + repositório transacional cobertos por testes

---

## Instalação

### Via Dalamud Plugin Installer (recomendado)

1. Abra **Dalamud → Settings → Experimental**
2. Em **Custom Plugin Repositories**, adicione:
   ```
   https://raw.githubusercontent.com/alejandrofelipe/afs-craftimizer/main/repo.json
   ```
3. Clique **Save and Close**
4. No **Plugin Installer**, procure por "Artificer" e instale

### Versão oficial

Para a versão estável sem customizações, instale o [Craftimizer original](https://github.com/WorkingRobot/Craftimizer) pelo repositório padrão do Dalamud.

---

## Build

Plugin:
```powershell
.\scripts\build.ps1 -Configuration Release
```

UI Studio:
```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run --project Artificer.UIStudio
```

Testes (260 testes cobrindo Simulator, Solver e UI):
```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test
```

---

## Stack Técnico

| Componente | Tecnologia |
|---|---|
| Runtime | .NET 10.0 via Dalamud.NET.Sdk 15.0.0 |
| UI (plugin) | Dalamud ImGui bindings |
| UI (shared lib) | ImGui.NET 1.90.9.1 |
| UI Studio | Silk.NET 2.22.0 (OpenGL + GLFW) |
| KDE / violin plots | MathNet.Numerics 5.0.0 |
| Persistência | Microsoft.Data.Sqlite 9.0.5 |
| Cache de ícones | Microsoft.Extensions.Caching.Memory 9.0.0 |
| Solver A* | Raphael.Net 4.1.0 |
| Performance | SIMD (Vector256) no MCTS para scoring de nodes |

---

## Créditos

Todo o crédito pelo desenvolvimento original vai para **Asriel (WorkingRobot)**.
