# Análise dos Módulos Principais

Aqui se aprofunda a função técnica de cada projeto na Solution e as pontes entre eles.

## 1. Artificer (Dalamud Plugin)

O projeto principal expõe a inicialização via `IDalamudPlugin` (`Plugin.cs`).

Responsabilidades técnicas:
- **Hooking & Memória:** `Hooks.cs` intercepta chamadas nativas do FFXIV (`UseAction`, `IsActionHighlighted`) via delegates `Hook<T>`, permitindo que o plugin sugira ações brilhando botões na hotbar.
- **Render de Interface:** Todas as janelas em `Windows/*.cs` usam ImGui via Dalamud WindowSystem. A inicialização de tema ocorre via `Theme.ConfigureForDalamud()` em `PreDraw()`.
- **DB e Configuração:** Persistência via API Dalamud com SQLite (`MacroRepository`) e JSON de configuração. Migrações gerenciadas manualmente.
- **CosmicTracker:** Janela flutuante que sobrepõe a tela do jogo durante Stellar Missions.

## 2. Artificer.UI (Biblioteca de UI Compartilhada)

Biblioteca "sem Dalamud" — usa apenas `ImGuiNET` (NuGet). Pode ser compilada e testada sem o SDK do Dalamud.

Responsabilidades técnicas:
- **Componentes reutilizáveis:** `ImGuiUtils*.cs` (tooltips, badges, progress bars, empty states, searchable combo, hyperlinks) e `ImRaii2.cs` (GroupPanel RAII).
- **Tokens de cor e tema:** `Colors.cs` define todos os tokens semânticos. `Theme.cs` configura o tema global do ImGui — `Theme.ConfigureForDalamud()` remapeia os enums de `ImGuiStyleVar` divergentes entre ImGuiNET e Dalamud.
- **Contrato de serviços:** `IUiServices.cs` abstrai dependências de runtime (fonte padrão, scale, abertura de links). `DalamudUiServices` implementa para o jogo; `StubUiServices` implementa para UIStudio.
- **ImRaiiShim:** Wrappers RAII para operações ImGui que não existem no `ImGuiNET` padrão (`Disabled`, `GroupPanel`, etc.).

## 3. Artificer.Simulator (Engine de Regras)

Módulo isolado — sem Dalamud SDK, sem FFXIV structs, sem I/O.

Responsabilidades técnicas:
- A engine evita alocações no *hot path*: usa structs passadas por valor/referência, classes estáticas, sem `new` em caminhos críticos.
- Cada skill do jogo é uma classe em `Artificer.Simulator/Actions/` que sobrepõe métodos definindo custo de CP, efeito em progresso/qualidade/durabilidade e condições de uso.
- Toda entrada (`CharacterStats`, `Recipe`, `SimulationState`) é injetada pelo plugin — o Simulator não lê nada do jogo diretamente.

## 4. Artificer.Solver (IA de Busca)

Responsabilidades técnicas:
- O núcleo reside no **MCTS** (Monte Carlo Tree Search) em `MCTS.cs`, com extensivo uso de `unsafe` e arenas de memória (`ArenaBuffer`, `ArenaNode`) para evitar GC pressure.
- **RaphaelUtils / Raphael.Net:** O Solver delega rotações completas ao `Raphael.Net` (bindings Rust via FFI). `RaphaelUtils.cs` mapeia enums do `Artificer.Simulator` para os tipos nativos de Raphael.
- O Solver consome o Simulator para avaliar nós da árvore MCTS sem sair do C# gerenciado quando Raphael não é necessário.

## 5. Artificer.UIStudio (Visualizador de UI)

App desktop standalone (Silk.NET + GLFW) para testar componentes de `Artificer.UI` sem abrir o FFXIV.

Responsabilidades técnicas:
- Cada `IStory` em `Stories/` demonstra um componente ou página. Stories são registradas manualmente em `Program.cs`.
- `StubUiServices` simula `IUiServices` em ambiente desktop (fontes via ImGui, links são no-op).
- Não referencia `Dalamud.*` — usa somente `Artificer.UI` + Silk.NET.

## 6. Artificer.Test e Artificer.Benchmark

- **Test (MSTest):** 215 testes cobrindo Simulator (mecânicas de cada habilidade), Solver (integridade do MCTS e Raphael), e componentes de UI (`ImRaiiShim`, `GearMessage`, `UIServices`).
- **Benchmark (BenchmarkDotNet):** Mede latência do Simulator e Solver no *hot path*. Rodar em modo `Release` é obrigatório para resultados realistas.
