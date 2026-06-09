# Craftimizer

**Fork mantido por:** alejandrofelipe  
**Autor original:** Asriel (WorkingRobot)  
**Repositório original:** https://github.com/WorkingRobot/Craftimizer  
**Versão atual:** 2.16.2.0 · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0

---

Plugin Dalamud para FFXIV que otimiza macros de crafting usando simulação e busca heurística. Gera automaticamente sequências de ações que maximizam HQ%, analisa receitas em tempo real e oferece sugestões durante a síntese ativa.

## Funcionalidades

### Macro Editor
Editor completo de macros com simulação offline. Permite selecionar qualquer receita, configurar stats do personagem (Craftsmanship, Control, CP, nível), definir quantidade de HQ ingredientes e gerar a sequência de ações ideal via solver. Exibe porcentagem estimada de HQ, taxa de sucesso e step-by-step visual das ações.

### Recipe Note
Overlay integrado ao Crafting Log do jogo. Enquanto você navega nas receitas, exibe o macro ótimo e as estatísticas estimadas para cada receita. Suporta badges visuais para receitas Expert, Collectible, Splendorous, Specialist e Cosmic Exploration.

### Synthesis Helper
Assistente em tempo real durante a síntese ativa. Mostra a próxima ação recomendada, barras de progresso de Progress/Quality/Durability/CP com arcos circulares animados, indicador de condição (Normal/Good/Excellent/Poor) e progresso de execução do macro atual.

### Macro Library
Biblioteca de macros salvos com busca, filtro, importação/exportação e integração com macros da comunidade.

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
| NextActionForked | Avalia cada candidato da próxima ação via MCTS em paralelo e escolhe o melhor; ideal para Synthesis Helper |

A função de score é multi-objetivo com pesos configuráveis:

- Quality (padrão: 80) — prioridade máxima, HQ%
- Progress (10), Steps (5), CP (3), Durability (2)

Parâmetros configuráveis: iterações (até 1.500.000), constante de exploração UCB, número de threads, max steps e pesos de score.

## Diferenças deste Fork

- Compatibilidade verificada com FFXIV 7.51+
- Barras de progresso redesenhadas (`ProgressBarComponent` com suporte a progresso agregado)
- Cache de ícones migrado para `IMemoryCache` com eviction configurável (sliding + absolute expiration)
- Reorganização estrutural do projeto para layout .NET padrão
- Melhorias de qualidade de código: `Settings.cs` e `ImGuiUtils.cs` divididos em arquivos parciais, constantes para magic numbers do jogo, thread-safety em `Configuration.Save()`, error handlers em fire-and-forget tasks
- 0 build warnings
- Corrigido `ObjectDisposedException` em `RecipeNote` ao reabrir o Crafting Log (texturas de badges buscadas no draw em vez de armazenadas como campos)
- Janela flutuante **CosmicTracker** com todos os 7 tipos de research (Type I–VII): barras de progresso individuais com marcador de threshold de upgrade, modo compacto, filtro "ocultar concluídos", destaque de delta por 10 s e auto-refresh a cada 5 s (`CosmicToolTracker` via hooks WKS + Lumina sheets)
- Botão estrela ★ na barra de título do Recipe Note, Macro Editor, Macro List e Synthesis Helper para abrir/fechar o CosmicTracker rapidamente; muda de cor quando há Stellar Mission ativa
- Sync com upstream v2.11: correção de GC corruption em `NodeScoresBuffer`, fix de crash por RNG compartilhado entre threads no solver, solver não gera mais ações supérfluas após progress completo
- Configuração de **Quality Target %**: slider 0–100% para limitar o alvo de qualidade, liberando CP e steps para macros mais eficientes em crafts onde 100% não é necessário
- Novo algoritmo de solver **Next Action Forked**: avalia cada próxima ação em paralelo via MCTS, oferecendo respostas mais rápidas e melhor adaptação a condições em tempo real no Synthesis Helper
- CosmicTracker atualiza instantaneamente ao trocar de job (sem aguardar o ciclo de auto-refresh de 5 s)
- **Fundação da Lista de Coleta (P0):** camada de dados completa — `CraftingListRepository` (SQLite), `InventoryScanner` (bags + saddlebag + retainers via FFXIVClientStructs), `IngredientResolver` (resolução recursiva da árvore de ingredientes com consciência de inventário) e `CraftingListManager` (CRUD, sync, merge/split, auto-delete de listas concluídas); sem UI ainda (sub-backlog de UI em andamento)
- **Lista de Coleta P1 — helpers de dados:** `RecipeSearchHelper` (índice em memória para busca por nome/job), `RecipeRestrictionChecker` (detecta livros mestre, specialist, quest unlock e receitas Expert via Lumina), `GatheringLocator` (resolve zona, aetheryte e tipo de nó de coleta por item), `TeleportHelper` (IPC com plugin Teleporter, graceful fallback), `MarketboardHelper` (preços Universalis via IPC + REST com cache SQLite TTL-configurável) e `ExportHelper` (serialização para texto plano, `/echo` e clipboard)
- Empty states em janelas que ficavam em branco: CosmicTracker com filtro "ocultar concluídos" e todos os tipos já completos; `CraftingListMergeWindow` sem listas candidatas; `CraftingListDetailWindow` com lista vazia (sem receitas ainda)
- **Lista de Coleta P2 — UI completa:** `FeatureHubWindow` (ícone flutuante permanente com popup de features), `CraftingListWindow` (lista paginada com fuzzy search, ordenação, filtros e context menu), `CraftingListAddWindow` (modal de adição com `SearchableCombo`, avisos de restrição e preview de materiais), `CraftingListDetailWindow` (vista detalhada/simples com barras de progresso, teleporte, preços DC, export e modo de seleção para split), `CraftingListMergeWindow` (modal de mescla com preview) e painel de Settings integrado

## Instalação

### Via Dalamud Plugin Installer (recomendado)

1. Abra **Dalamud → Settings → Experimental**
2. Em **Custom Plugin Repositories**, adicione a URL:
   ```
   https://raw.githubusercontent.com/alejandrofelipe/afs-craftimizer/main/repo.json
   ```
3. Clique **Save and Close**
4. No **Plugin Installer**, procure por "Craftimizer" e instale

Atualizações futuras aparecem automaticamente no Plugin Installer.

### Versão oficial

Para a versão estável e sem customizações, instale o [Craftimizer original](https://github.com/WorkingRobot/Craftimizer) pelo repositório padrão do Dalamud.

## Build

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Testes (194 testes cobrindo Simulator e Solver):

```powershell
dotnet test
```

## Stack Técnico

- .NET 10.0 via Dalamud.NET.Sdk 15.0.0
- MathNet.Numerics 5.0.0 (KDE para violin plots)
- Microsoft.Data.Sqlite 9.0.5 (persistência de macros)
- Microsoft.Extensions.Caching.Memory 9.0.0 (cache de ícones)
- Raphael.Net 4.1.0 (solver A*)
- SIMD (Vector256) no MCTS para scoring de nodes

## Créditos

Todo o crédito pelo desenvolvimento original vai para **Asriel (WorkingRobot)**.
