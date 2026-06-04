# Craftimizer

**Fork mantido por:** alejandrofelipe  
**Autor original:** Asriel (WorkingRobot)  
**Repositório original:** https://github.com/WorkingRobot/Craftimizer  
**Versão atual:** 2.10.3.0 · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0

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
- Progresso de research data da Cosmic Tool exibido em tempo real no Crafting Log e no Macro Editor durante Stellar Missions (`CosmicToolTracker` via hooks WKS)

## Instalação

Este é um fork de desenvolvimento pessoal. Para a versão oficial e estável, instale pelo repositório original no Dalamud Plugin Installer.

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
