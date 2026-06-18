# Estrutura do Projeto Artificer

Este documento detalha onde as coisas estão na pasta base do projeto e suas funções de desenvolvimento.

* **`.claude/`**: Regras, agentes e instruções personalizadas locais do AI Claude para manutenção do projeto.
* **`.vscode/`**: Definições comuns de ambiente para quem usa Visual Studio Code.
* **`assets/`** e **`Images/`**: Ativos de imagem (pngs, svgs e ícones) usados na documentação (como o README) ou no gerenciador de pacotes do Dalamud.
* **`backlog/`**: Documentação e acompanhamento de bugs conhecidos e *feature requests*. Pasta gitignored — local only.
* **`scripts/`**: Scripts PowerShell para build local, bump de versão e deploy. Pasta gitignored — local only.
* **`Artificer/`**: C# Project do plugin Dalamud (entry point, janelas, hooks, serviços).
  * `Windows/`: Controllers das janelas ImGui (`MacroEditor.cs`, `RecipeNote.cs`, `Settings.cs`, `FeatureHub.cs`, `CosmicTracker.cs`, etc.).
  * `Utils/`: Hooks de jogo (`ActionUsed`), leitura de memória, IPC, e utilitários de UI específicos do plugin (`PluginImGuiUtils.*.cs`).
  * `Application/`: Regras de negócio restritas ao plugin (copy/paste de macros, `MacroRepository`, `CosmicToolTracker`).
* **`Artificer.UI/`**: Biblioteca de UI compartilhada, sem dependência do Dalamud SDK. Usa somente ImGui.NET.
  * `Colors.cs`, `Theme.cs`: tokens de cor e temas.
  * `ImGuiUtils*.cs`, `ImRaiiShim.cs`, `ImRaii2.cs`: componentes reutilizáveis e helpers RAII.
  * `IUiServices.cs`, `ProgressBarComponent.cs`, `UIConstants.cs`: contratos e constantes de UI.
* **`Artificer.UIStudio/`**: App desktop standalone para visualizar e testar componentes de UI sem rodar o FFXIV.
  * `Stories/`: Uma story por componente/página, organizadas por Category (`Atoms`, `Molecules`, `Pages`).
* **`Artificer.Simulator/`**: C# Project que modela as mecânicas matemáticas de crafting puramente, sem dependências externas.
  * `Actions/`: Cada habilidade do jogo como uma classe, derivada de `BaseAction.cs`.
* **`Artificer.Solver/`**: C# Project com a IA de recomendação. `MCTS.cs`, `RaphaelUtils.cs`, integração com `Raphael.Net`.
* **`Artificer.Test/`**: C# Project de testes unitários (MSTest) para Simulator, Solver e componentes de UI. 215 testes.
* **`Artificer.Benchmark/`**: C# Project de benchmarks de performance (BenchmarkDotNet) para o Simulator e Solver.
* **`dist/`**: Gerado automaticamente após compilar em Release. Contém o ZIP final para importação no XIVLauncher.

### Configurações na Raiz:
- **`Artificer.sln`**: Solução Visual Studio ligando todos os projetos.
- **`.editorconfig`**: Mantém consistência de tabs, indentações e lint rules (Meziantou analyzer).
- **`.gitignore`**: Inclui `scripts/`, `backlog/`, `dist/`, `docs/superpowers/` e artefatos de build.
