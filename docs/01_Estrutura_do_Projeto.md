# Estrutura do Projeto Craftimizer

Este documento detalha onde as coisas estão na pasta base do projeto e suas funções de desenvolvimento.

* **`.claude/`**: Regras, agentes e instruções personalizadas locais do AI Claude para manutenção do projeto.
* **`.vscode/`**: Definições comuns de ambiente para quem usa Visual Studio Code.
* **`assets/`** e **`Images/`**: Ativos de imagem (pngs, svgs e ícones) usados primariamente na documentação (como o README) ou no gerenciador de pacotes do Dalamud.
* **`backlog/`**: Documentação e acompanhamento solto de bugs conhecidos e *feature requests* (ex. "calcular-ping-wait-macro.md").
* **`Benchmark/`**: Contém o CSPROJ `Craftimizer.Benchmark` para testar performance em micro-nível (BenchmarkDotNet).
* **`Craftimizer/`**: C# Project do plugin de Interface do usuário.
  * `Windows/`: Controllers das janelas do ImGui (`MacroEditor.cs`, `RecipeNote.cs`, `Settings.cs`).
  * `Utils/`: Integração de Hooks (como ActionUsed), leitura de memória in-game, e gerência de Ipc/Comunicações.
  * `Application/`: Regras de negócio restritas ao plugin (ex. Copy/Paste e conversões das Macros).
* **`Simulator/`**: C# Project que modela as mecânicas matemáticas de crafting puramente.
  * `Actions/`: Scripts detalhados de cada habilidade, derivadas de `BaseAction.cs`.
* **`Solver/`**: C# Project onde mora a IA de recomendação. Arquivos como `MCTS.cs` ou `RaphaelUtils.cs` estão aqui.
* **`Test/`**: C# Project de testes unitários para o Simulador e Solver usando MSTest e Coverlet para métricas de Code Coverage.
* **`scripts/`**: Arquivos em PowerShell para compilar releases locais, fazer "bump version" (como `bump-version.ps1`) ou gerar artefatos zip.
* **`dist/`**: Gerado automaticamente após compilar. Onde o arquivo ZIP final pra importação no XIVLauncher reside.

### Configurações na Raiz:
- **`Craftimizer.sln`**: Solução Visual Studio ligando tudo.
- **`.editorconfig`**: Mantém consistência de tabs, indentações, e lint rules (ex. regras Meziantou).
- **`UPDATE-7.5.md`**: Log longo e histórico da compatibilidade recente.
