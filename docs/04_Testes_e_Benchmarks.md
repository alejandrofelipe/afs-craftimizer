# Qualidade de Código (Testes & Benchmarks)

A estabilidade do Artificer recai sobre seus dois projetos paralelos focados em QA estrutural. Testar localmente é mandatório antes de qualquer "Push".

## 1. O Projeto `Test` (MSTest)
Usamos MSTest V3 injetado via framework `Microsoft.Testing.Platform`.
Para executar todo o pacote base, vá ao root ou navegue na pasta:
```sh
dotnet test
```

A estrutura lógica aborda os dois pontos soltos da aplicação:
- **Simulator Tests**: Mocka um `SimulationState` inicial (incluindo buffs passados como *Great Strides*), joga uma Action e valida asserts específicos das mecânicas in-game, como se multiplicou perfeitamente ou se esgotou a durabilidade no ciclo finalizado. Evita regressões da Square Enix em updates menores.
- **Solver Tests**: Focado no `MCTSSolverTests.cs`, atesta a integridade do retorno nativo do Rust (via bibliotecas precompiladas para múltiplos runtimes que estão em `Test/bin/.../runtimes`). A validação inclui atestar que o Monte Carlo gerará a árvore e passará com sucesso sem estourar referências de ponteiro do heap.

## 2. O Projeto `Benchmark`
Testes de tempo real de simulação, especialmente criados pra manter os limites sob controle do Solver MCTS. Utiliza *BenchmarkDotNet*.

Para rodar (Obrigatoriamente no modo `Release` para inibir JIT overhead):
```sh
dotnet run -c Release --project Benchmark/Artificer.Benchmark.csproj
```

**Por que focar nisso?**
Se um desenvolvedor altera como uma habilidade processa matemática da Qualidade e isso adicina um `if/else` caro no *hotpath*, o tempo do simulador passa de 300 nanosegundos para 2 microsegundos. O Benchmark sinalizará isso instantaneamente e barrará a implementação se o decréscimo travar a UI (Main Thread) do jogo.
