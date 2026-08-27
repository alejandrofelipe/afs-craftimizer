# Qualidade de Código (Testes & Benchmarks)

A estabilidade do Artificer recai sobre dois projetos de QA. Rodar os testes localmente é obrigatório antes de qualquer push.

## 1. Artificer.Test (MSTest)

Usa MSTest V3 via `Microsoft.Testing.Platform`. 385 testes no total.

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test
```

Esperado: `Passed! - Failed: 0, Passed: 385, Skipped: 0`

### Estrutura

- **`Artificer.Test/Simulator/`**: Instancia um `SimulationState` com buffs iniciais (ex: Great Strides ativo), executa uma Action e valida o resultado matemático. Previne regressões quando a Square Enix altera fórmulas em patches menores.
- **`Artificer.Test/Solver/`**: `MCTSSolverTests.cs` valida a integridade do retorno nativo do Rust via `Raphael.Net`. Confirma que o MCTS gera a árvore e devolve um resultado sem estourar ponteiros de heap.
- **`Artificer.Test/UI/`**: Testes de componentes de UI como `ImRaiiShim`, `GearMessage` e serviços de UI.
- **`Artificer.Test/CraftingLists/`**: planner de move (`CraftingListMovePlannerTests`), reconciliador de progresso (`MaterialProgressReconcilerTests`), repositório transacional (`CraftingListMoveRepositoryTests`, SQLite temporário) e `MarketboardHelper`/cache de preços — sem cliente FFXIV.
- **`Artificer.Test/Application/`**: `SolverRunTests` (isolamento por geração do solver) e decisões puras de macro (`MacroSelectionTests`, `MacroScoringTests`).

### Quando adicionar testes

Sempre que modificar um arquivo em `Artificer.Simulator/Actions/`, adicionar ou atualizar o teste correspondente em `Artificer.Test/Simulator/`.

---

## 2. Artificer.Benchmark (BenchmarkDotNet)

Benchmarks de tempo real para manter o Simulator e Solver dentro dos limites de performance aceitáveis.

**Obrigatoriamente em modo Release** para eliminar overhead do JIT:

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run -c Release --project Artificer.Benchmark
```

### Por que importa

Se uma alteração em uma skill adiciona um `if/else` no *hot path* e o tempo do Simulator sobe de ~300ns para ~2µs, o Benchmark sinaliza isso imediatamente. Em 300ms de craft com 40 ações, cada microssegundo extra importa para não travar a main thread do Dalamud.
