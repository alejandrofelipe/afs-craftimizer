# Task 2 — parser e combinação de preços por escopo

## Resultado

- `MarketboardHelper.ParseScope` agora produz um `MarketScopePrice` puro e testável offline.
- Listings fora de ordem usam `MinBy(PricePerUnit)`; `WorldName` vem do listing mínimo e todas as quantidades são somadas.
- Sem listings, `minPrice` é preservado, o servidor fica vazio e a quantidade é zero.
- JSON inválido retorna `null`.
- `MarketboardHelper.Combine` mantém o preço/estoque do mundo e usa preço/servidor do DC quando disponível.
- Sem DC, o snapshot do mundo é reutilizado; sem mundo, o resultado é `null`; `CachedAt` é o timestamp mais antigo.
- O fluxo de produção continua fazendo somente uma busca e foi migrado apenas para a API transitória `GetCachedScopePrice`/`SaveScopePrice` da Task 1. Transporte duplo, scheduler e cancelamento permanecem para a Task 3.

## TDD — RED

Comando:

```powershell
& 'C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe' test Artificer.Test --filter 'FullyQualifiedName~MarketboardHelperTests' --no-restore -p:NuGetAudit=false
```

Resultado: falha de compilação esperada, com seis ocorrências de `CS0117`, porque `MarketboardHelper` ainda não possuía `ParseScope` nem `Combine`.

## TDD — GREEN e regressão

Comando focado:

```powershell
& 'C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe' test Artificer.Test --filter 'FullyQualifiedName~MarketboardHelperTests' --no-restore -p:NuGetAudit=false
```

Resultado: 6/6 aprovados, 0 falhas, 0 ignorados.

Comando focado com o cache da Task 1:

```powershell
& 'C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe' test Artificer.Test --filter 'FullyQualifiedName~MarketboardHelperTests|FullyQualifiedName~MarketPriceCacheTests' --no-restore -p:NuGetAudit=false
```

Resultado: 8/8 aprovados, 0 falhas, 0 ignorados.

Suíte completa:

```powershell
& 'C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe' test Artificer.sln --no-restore -p:NuGetAudit=false
```

Resultado: 315/315 aprovados, 0 falhas, 0 ignorados.

Build completo:

```powershell
& 'C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe' build Artificer.sln --no-restore -p:NuGetAudit=false
```

Resultado: sucesso, 0 avisos e 0 erros.

## Arquivos

- `Artificer/Application/CraftingLists/MarketboardHelper.cs`
- `Artificer.Test/CraftingLists/MarketboardHelperTests.cs`
- `.superpowers/sdd/2026-08-18-concorrencia-precos-mercado/task-2-report.md`

## Self-review

- Todos os comportamentos pedidos possuem teste offline com valores esperados derivados manualmente.
- O teste de listings fora de ordem falharia se o parser voltasse a usar o primeiro listing, ignorasse quantidades ou inventasse o servidor.
- Os testes de combinação falhariam se os preços fossem duplicados, se a disponibilidade viesse do DC, se o timestamp mais novo fosse usado ou se um resultado fosse criado sem mundo.
- Nenhuma API de transporte, scheduler, geração ou cancelamento da Task 3 foi introduzida.
- `git diff --check`: sem erros.
- Revisão externa por subagente não foi executada porque a task proíbe explicitamente criar subagentes; o diff fica disponível ao agente coordenador.

## Commit, HEAD e estado final

- Base/HEAD inicial: `0f06d5185ffd3a9706997440538e266e0f4da60f`.
- Commit da Task 2: `fix: distinguish world and data center prices` (o commit que contém este relatório e passa a ser o `HEAD`).
- O SHA final é informado no handoff após a criação do próprio commit, evitando uma referência circular no conteúdo versionado.
- Worktree final: `git status --short` sem saída, confirmado após o commit e registrado no handoff.

## Riscos remanescentes

- Até a Task 3 buscar mundo e DC separadamente, o fluxo público de uma única busca reutiliza o snapshot consultado nos dois campos por meio de `Combine(scope, null)`; isso preserva o comportamento de produção sem antecipar a integração dupla.
- O IPC existente continua sendo consultado por `worldId` mesmo quando a chave selecionada é um nome de DC; essa limitação preexistente pertence à separação de transporte/escopos da Task 3.
