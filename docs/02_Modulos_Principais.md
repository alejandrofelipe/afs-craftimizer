# Análise dos Módulos Principais

Aqui se aprofunda a função técnica de cada biblioteca na Solution e as pontes entre elas.

## 1. Craftimizer (Dalamud Plugin API)
O projeto principal expõe a inicialização via interface `IDalamudPlugin` do framework (dentro de `Plugin.cs`).
Responsabilidades Técnicas:
- **Hooking & Memória:** Usando `Hooks.cs`, o plugin intercepta chamadas nativas do FFXIV (como `UseAction` e `IsActionHighlighted`) usando delegates `Hook<T>`. Permite alterar a UI do jogo para forçar botões "brilharem" sugerindo o próximo passo via formigas ao redor do botão.
- **Render de Interface:** Todo `Windows/*.cs` utiliza extensivamente o namespace do `ImGui` para prover overlay e janelas. 
- **DB e Configuração:** Persistência usando a API Dalamud junto a migrações legadas para gerenciar um `MacroRepository`.

## 2. Craftimizer.Simulator (Simulação Determinística)
Um módulo isolado (sem Dalamud SDK ou FFXIV Structs envolvido).
Responsabilidades Técnicas:
- A engine foi modelada visando evitar alocação de objetos (Allocation-Free) no *Hot Path*. Usa majoritariamente classes estáticas com cálculos e modificação mutável via Passagem de Valor/Referência.
- Cada skill no jogo é uma classe em `Simulator/Actions/` que sobrepõe métodos predefinindo sucesso, falha, incremento em Durabilidade/Qualidade.
- Não há I/O na simulação. Toda entrada (`CharacterStats`, `Recipe`) é entregue injetada pelo plugin.

## 3. Craftimizer.Solver (Inteligência & Busca)
Responsabilidades Técnicas:
- O cérebro reside no framework **MCTS** (Monte Carlo Tree Search), desenhado em C# com extensivo uso de `unsafe` blocks.
- **RaphaelUtils / Raphael.Net:** O Solver no C# interage com o `Raphael.Net` (implementado em Rust) para processar os nós de MCTS na simulação em velocidade nativa. Usa interoperabilidade direta com o C. O arquivo `RaphaelUtils.cs` mapeia Enums da API `Craftimizer.Simulator` para o lado nativo (`Raphael.Action`).
- Performance é monitorada localmente via matrizes de arrays instanciadas na simulação inicial pra não engasgar o Garbage Collector.
