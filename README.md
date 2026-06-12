# Artificer

**Fork mantido por:** alejandrofelipe  
**Autor original:** Asriel (WorkingRobot)  
**Repositório original:** https://github.com/WorkingRobot/Craftimizer  
**Versão atual:** 2.20.9.0 · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0

---

Plugin Dalamud para FFXIV que otimiza macros de crafting usando simulação e busca heurística. Gera automaticamente sequências de ações que maximizam HQ%, analisa receitas em tempo real e oferece sugestões durante a síntese ativa.

## Estrutura do Projeto

```
Artificer/            ← Plugin Dalamud (entry point, janelas, hooks, serviços)
Artificer.UI/         ← Biblioteca de UI compartilhada (sem Dalamud, ImGui.NET direto)
Artificer.UIStudio/   ← App desktop standalone para testar UI sem o FFXIV rodando
Simulator/              ← Lógica de simulação de crafting (puro C#, sem dependências externas)
Solver/                 ← Algoritmos MCTS, genético e Raphael
Test/                   ← 194 testes cobrindo Simulator e Solver
Benchmark/              ← Benchmarks de performance do solver
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
- 🐛 Fix: crash `C0000005` em `FeatureHubWindow.PostDraw()` — `ImGuiWindowFlags.NoBackground` causa Dalamud SDK 15 chamar `igCustom_WindowSetInheritNoInputs` com ponteiro inválido; `SetNextWindowPos` movido para `PreDraw()` (estava em `Draw()`, afetando janela seguinte)
- 🐛 Fix: crash `C0000005` em `Theme.Push()` — plugin não deve shipar `cimgui.dll` pois o Dalamud fornece o contexto nativo; DLL duplicada causava `GImGui == NULL`
- 🐛 Fix: crash ao abrir Settings — `ImRaii.PushStyle` e `BeginGroupPanel` usavam valores ImGuiNET para `ImGuiStyleVar` (Dalamud moveu `DisabledAlpha` do índice 1 para 24, deslocando todos os outros -1); `ImRaiiShim` agora remapeia automaticamente via `ConfigureForDalamud()`
- FeatureHubWindow só aparece enquanto um personagem está em jogo (`IsLoggedIn`); posição inicial no canto inferior direito preservada entre sessões via `ImGuiCond.FirstUseEver`
- 🐛 Fix: plugin não carregava com Dalamud SDK 15+ — `ImGui.NET.dll` foi removida do runtime do Dalamud; agora shipada com o plugin (wrapper gerenciado que P/Invoca no `cimgui.dll` nativo do Dalamud)
- Configuração de **Quality Target %**: slider 0–100% para limitar o alvo de qualidade
- Novo solver **Next Action Forked**
- Sync com upstream v2.11 (fix GC corruption, fix crash por RNG compartilhado, solver não gera ações supérfluas após progress completo)
- Aba **"Experimental"** nas Settings com banner de aviso
- Confirmação inline para ações destrutivas (remover receita, limpar Gear Wear)
- Reorganização de conteúdo das Settings por contexto de uso
- Empty states nas janelas que ficavam em branco
- **Lista de Coleta completa** (P0–P2): dados (SQLite, InventoryScanner, IngredientResolver), helpers (busca, restrições, coleta, mercado, exportação) e UI completa (FeatureHub, List, Add, Detail, Merge windows)
- 🐛 Fix: "Suggested Macro" exibia card confuso com arcos de estatística e botão Copy inutilizável quando o solver retornava 0 ações; agora exibe estado de erro claro com botão "Suggest Again"

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
dotnet build Artificer/Artificer.csproj -c Release
```

UI Studio:
```powershell
dotnet run --project Artificer.UIStudio
```

Testes (194 testes cobrindo Simulator e Solver):
```powershell
dotnet test
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
