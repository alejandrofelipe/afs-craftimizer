# Visão Geral do Craftimizer

## 1. Propósito

O Craftimizer é um plugin robusto para FFXIV (framework Dalamud) destinado a otimizar o crafting. Suas funções incluem:

1. **Simulação Avançada:** Testa rotinas de craft (macros) considerando CP, durabilidade, progresso e chance de sucesso com precisão, replicando as mecânicas internas do jogo.
2. **Solver (MCTS + Rust Raphael):** Um solucionador que usa busca de Monte Carlo (MCTS) acelerada por código nativo via `Raphael.Net` (baseado em Rust) para encontrar rapidamente a rotação mais eficiente para craftar.
3. **Assistência In-Game:** Otimiza o processo de crafting oferecendo sugestões visuais ativas na tela do usuário e integrações como um Macro Editor avançado.

## 2. Arquitetura

O projeto divide-se em componentes fortemente desacoplados para focar em testes e reusabilidade:

### Módulos Essenciais
* **`Craftimizer.Plugin` (O Client Dalamud):** A "cola" entre o jogador, FFXIV (Dalaud) e as bibliotecas lógicas. Lida com `ImGui`, ler memória do jogo e orquestrar as simulações e UI.
* **`Craftimizer.Simulator` (A Engine de Regras):** Biblioteca "pura" (C# padrão, independente de jogo ou UI) implementando cada habilidade do FFXIV e seu impacto matemático no craft.
* **`Craftimizer.Solver` (A IA):** Toma decisões a partir das regras do `Simulator`. Depende do pacote nativo `Raphael.Net` para lidar com cálculos extensos e recorrentes. Usa algoritmos como MCTS.

### Módulos de Suporte
* **`Craftimizer.Test` & `Craftimizer.Benchmark`:** Testes unitários (MSTest) e testes de stress de perfomance (BenchmarkDotNet) fundamentais na aprovação de refatorações das engines.
* **`scripts`:** Scripts de PowerShell focados em automatizar CI local, bump versionings e gerar pacotes zip.

## 3. Tech Stack

- **Linguagem Principal:** C# (.NET 10.0, com `AllowUnsafeBlocks` habilitado).
- **Injeção e UI:** Dalamud API + ImGui.Net.
- **Solver Backend:** Integrado ao pacote nuget `Raphael.Net` que expõe *bindings* Rust via FFI de alto desempenho.
- **Performance:** Amplo uso de ponteiros (`unsafe`), structs alocados e controle restrito de heap para MCTS.
- **Config & DB:** SQLite e arquivos locais json para gerenciar o repositório de macros do usuário.
