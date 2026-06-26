# Análise de updates relevantes do upstream — 2026-06-26

Comparação dos commits recentes do **Craftimizer** (`WorkingRobot/Craftimizer`) contra o fork
**Artificer**, focada no que importa (Simulator/Solver/core + correções funcionais). Mudanças de
**UI** do upstream em geral **não** se aplicam (o fork reescreveu a UI).

- **Upstream em:** `main`, último push 2026-06-10, versão **2.11.0.1** (scheme do upstream).
- **Fork em:** 2.26.1.0 (scheme próprio).
- **Método:** cada item foi verificado lendo o código do fork **e** o diff real do commit upstream,
  e depois revisado por um agente adversarial (que corrigiu 2 conclusões — ver scoring e cosmic).
- **De-para estrutural:** ver [`mapping.md`](mapping.md).

> **Atualização (2026-06-26):** entregues — **#1 Robust condition** (v2.26.2.0), **#2 Raphael.Net 5.0**
> (v2.26.3.0), **#3 scoring lexicográfico** e **#5 cosmic cap** (v2.26.4.0, no mesmo port literal).
> Resta só **#4 (pruning)**, adiado por YAGNI (ver `backlog/`).

## Resumo

| # | Update upstream | Subsistema | Status no fork | Prioridade | Esforço |
|---|---|---|---|---|---|
| 1 | **Robust condition (7.41)** | Simulator | ✅ **entregue (v2.26.2.0)** | 🔴 Alta (correctness) | Pequeno |
| 2 | **Raphael.Net 4.1 → 5.0** + internals | Solver | ✅ **entregue (v2.26.3.0)** | 🟠 Média | Pequeno |
| 3 | Rework de scoring (objetivo lexicográfico) | Solver | ✅ **entregue (v2.26.4.0)** | 🟠 Média | Grande |
| 4 | Pruning no synthesis helper (`c34dc59`) | Solver | 🟡 parcial | ⚪ Baixa | Grande |
| 5 | Fix cosmic exploration craft limiting | Solver/plugin | ✅ **entregue (v2.26.4.0)** | ⚪ Baixa | Médio |
| 6 | Attempted GC corruption fix | Solver | ✅ já coberto | ⚪ Baixa | — |
| — | Changelog window, ImRaii ref-struct, mudanças de UI | UI | N/A (fork reescreveu) | — | — |

**Único gap claro de correctness: #1 (Robust).** É a recomendação de port mais óbvia.

---

## 1. Robust condition (FFXIV 7.41) — 🔴 falta

**Upstream** (`99ca21e` / PR #61): adiciona a condição **Robust** (introduzida no patch 7.41):
- `Condition.cs`: novo valor no enum + `Robust = 1 << 10, // 0x0400` no `ConditionMask`.
- `Simulator.cs`: `GetConditionChance` → `Robust => 0.10f` (10% de chance); `StepCondition` →
  `Robust => Sturdy` (no passo seguinte vira Sturdy); **`CalculateDurabilityCost` passa de
  `Condition == Sturdy` para `Condition is Sturdy or Robust`** (Robust reduz durabilidade pela metade).
- `SimulatorUtils.cs`: addon IDs `Robust => (14218, 14219)` (cosmético — tooltip/label).

**Fork:** ausente em tudo. `Artificer.Simulator/Condition.cs:5-16` para em `GoodOmen` (`1<<9`);
`Simulator.cs:136-148` (GetConditionChance) cai no default `_ => 0.00f`; `Simulator.cs:161-171`
(StepCondition) não tem o mapeamento; `Simulator.cs:217` `if (Condition == Condition.Sturdy)` não
inclui Robust. `Artificer/Utils/SimulatorUtils.cs:213-222` (addon IDs) também não tem. Grep por
"Robust" em `Artificer.Simulator` = 0 ocorrências.

**Impacto:** em receitas expert 7.41+ que rolam Robust, o fork a trata como Normal — chance 0%,
durabilidade **não** reduzida pela metade, transição errada → macros/solver com cálculo incorreto.

**Port (alvos: `Artificer.Simulator/Condition.cs`, `Simulator.cs`):**
1. `Condition.cs`: `Robust,` após `GoodOmen` no enum + `Robust = 1 << 10, // 0x0400` no `ConditionMask`.
   `GetPossibleConditions` já varre `Enum.GetValues<Condition>()`, então funciona sem mais nada.
2. `Simulator.cs`: `Condition.Robust => 0.10f,` (antes do `_ =>`); `Condition.Robust => Condition.Sturdy,`;
   e `if (Condition == Condition.Sturdy || Condition == Condition.Robust)` em `CalculateDurabilityCost`.
3. (Opcional/cosmético) addon IDs `(14218, 14219)` em `SimulatorUtils.cs`.
4. Conferir que nenhum `switch` exaustivo sobre `Condition` (UI/tooltips) quebra com o novo valor.

---

## 2. Raphael.Net 4.1.0 → 5.0.0 + internals — 🟠 falta

**Upstream:** `fbf3ead` sobe `Raphael.Net` 4.1.0 → **5.0.0** (.csproj + lock). `78e18ac` adapta os
internals à nova API: em `RaphaelUtils.cs` adiciona o mapeamento bidirecional de **RapidSynthesis,
HastyTouch, DaringTouch** (Stellar/Cosmic; `(Stellar)SteadyHand` fica comentado, ainda não suportado);
em `Solver.cs` adiciona `StellarSteadyHandCharges = 0` ao inicializador de `Raphael.SolverInput`
(campo novo exigido pela struct do 5.0.0).

**Fork:** `Artificer.Solver.csproj:19` ainda em `4.1.0`. `RaphaelUtils.cs` (ConvertRawAction
linhas 29-63 / ConvertToRawAction 68-102) **não** tem essas 3 ações — `ConvertToRawAction` cai em
`_ => null` (descarta silenciosamente do pool) e `ConvertRawAction` em `_ => throw
ArgumentOutOfRangeException`. `Solver.cs:163-172` inicializa `SolverInput` sem `StellarSteadyHandCharges`.
As ações já existem como `ActionType` (`Artificer.Simulator/Actions/ActionType.cs:14,19,33`) e em
`RiskyActions` (`SolverConfig.cs:223-225`) — pré-requisito presente.

**Por que Média (não Alta):** hoje, em 4.1.0, o gap é **dormente** (o solver Rust nem conhece essas
ações, então não há bug em runtime); vira correctness real só após o upgrade.

**Port (alvos: `Artificer.Solver.csproj`, `RaphaelUtils.cs`, `Solver.cs`):**
1. **Bumpar o `.csproj` para `Raphael.Net 5.0.0` PRIMEIRO** — só então `StellarSteadyHandCharges`
   existe na struct (adicionar antes quebra o build no 4.1.0).
2. Adicionar RapidSynthesis/HastyTouch/DaringTouch nos dois switches de `RaphaelUtils.cs`.
3. Adicionar `StellarSteadyHandCharges = 0,` em `Solver.cs` após `JobLevel`.
4. Conferir `packages.lock.json` (se houver no fork) e atualizar deps relacionadas (DotNext 5.26.1→6.1.0
   se o 5.0.0 exigir).

---

## 3. Rework de scoring / quality targets — 🟡 parcial (correção da análise preliminar)

**Upstream** (`58c8706`, `67f9aa4`, `5544082`): trocou o scoring de **soma ponderada** (progress +
quality + durability + cp + steps, com lerp de dominance) por um **objetivo estritamente lexicográfico**
estilo Raphael: completion → quality (até um target) → menos steps. **Durability e CP foram removidos
do objetivo de propósito** (issues #6 e #44 — são "moeda de busca", não metas). As 5 variáveis `Score`
foram apagadas; o quality target virou `int QualityTarget` absoluto, resolvido via `ResolveQualityTarget`;
`MCTSConfig` passou a receber `RecipeInfo` no construtor.

**Fork:** **parcial.** Já tem `QualityTargetPercent` (`MCTSConfig.cs:25`, `SolverConfig.cs:51`,
`SimulationNode.cs:59-61`) e resolução de collectability (`WithResolvedQualityTarget`,
`SolverConfig.cs:103-123`) — **mas mantém o modelo antigo de soma ponderada** (`SimulationNode.cs:63-93`,
ainda pontuando durability/CP via `config.ScoreDurability`/`ScoreCP`), os 5 pesos `Score`
(`MCTSConfig.cs:18-22`, defaults 10/80/2/3/5) e o painel "Score Weights" na UI (`Settings.Solver.cs`).
O construtor `MCTSConfig(in SolverConfig)` **não** recebe `RecipeInfo`.

**Natureza:** mudança **comportamental** de scoring, não correctness — ambos produzem crafts válidos.
Os macros podem diferir (o upstream evita encher o fim do craft com durability/CP). **Esforço grande:**
mudar a assinatura de `MCTSConfig` obriga atualizar ~6 call sites (`CraftingHelper.cs:1395,1480`,
`Bench.cs:112`, `Program.cs:19`, `MCTSSolverTests`, `SolverNodes`) + reescrever `CalculateScoreForState`
+ remover/portar a UI de pesos. Portar só se quisermos a melhoria de qualidade dos macros; o fork já é
funcionalmente suficiente para atingir quality targets.

---

## 4. Pruning no synthesis helper (`c34dc59`, base `7ff4ce2`) — 🟡 parcial

**Upstream:** o `NextActionForked` maduro roda 1 MCTS por candidato (`Parallel.For`), faz `TrimRotation()`,
e em `c34dc59` adiciona **poda em 2 fases**: quando candidatos > `PruneActionCount` (default = nº de cores),
faz um "screen" raso em todos gastando `ScreenBudgetPercent` (33%) do budget, rankeia por
(Completed, Quality, menos Steps), mantém top-K e aprofunda só esses. `7ff4ce2` introduziu também o
construtor `MCTS(config, state, Random rng)` (seed por candidato, thread-safe/determinístico).

**Fork:** tem a **base** `NextActionForked` (`Solver.cs:496-611`), mas é uma reimplementação divergente:
`forksPerAction` via `Task.Run`, best por `MaxScore` global, **sem** `TrimRotation`, **sem** screening/poda,
e `MCTS` sem o overload com `Random rng` (`MCTS.cs:22`, usa `new Random()` interno). `SolverConfig.cs:56-62`
não tem `PruneActionCount`/`ScreenBudgetPercent`. Grep por Prune/Screen/TrimRotation = 0.

**Natureza:** otimização de qualidade/perf das sugestões mid-craft, **não** correctness → Baixa. Portar
não é só aplicar `c34dc59` (o fork não tem a base que ele assume): exigiria reescrever o `NextActionForked`
do fork para o modelo upstream + adicionar o overload de `MCTS` com seed, e só então a poda encaixa.
Risco de regressão no solver mid-craft que já funciona — só vale se houver reclamação de sugestões
ruins/lentas no Synthesis Helper.

---

## 5. Fix cosmic exploration craft limiting (`98498ab`) — ⚪ não-aplicável (com ressalva)

**Upstream:** corrige um bug onde o teto `RecipeInfo.CollectableTargetQuality = maiorThreshold ×
CollectabilityDivisor` fazia o solver **parar de otimizar quality** ao atingir o maior threshold de
collectability. Em Cosmic Exploration vale a pena exceder (XP/research), então o fix introduz
`limitMaxThreshold` e o desativa no ramo Cosmic.

**Fork:** o patch **literal** não se aplica — não existe `CollectableTargetQuality` nem
`CollectabilityDivisor` (grep global = 0; `Artificer.Simulator/Recipe.cs` só tem `MaxQuality`).

**Ressalva importante** (apontada pela verificação adversarial): o fork **tem** um mecanismo
equivalente de capping via `SolverConfig.WithResolvedQualityTarget` (`SolverConfig.cs:103-123`), que
limita `QualityTargetPercent` ao maior tier de collectability — **aplicado a Cosmic sem isenção**
(`CraftingSession.cs:291`, `MacroEditor.Solver.cs:59`). Porém é **opt-in e OFF por padrão**
(`QualityTargetToMaxCollectability` não inicializado → `false`; UI "Cap at Max Collectability Tier"). Logo
**não é correctness** (com a flag off, o fork otimiza até MaxQuality e não sofre o limiting) — é uma
**lacuna latente de paridade de baixa prioridade**: se alguém ligar o cap, Cosmic seria capada
subotimamente. Ideal seria isentar Cosmic do `WithResolvedQualityTarget`.

> ⚠️ Investigar também: o fork rotula `CollectableMetadataKey == 7` como `IsStudiumDelivery`
> (`RecipeData.cs:30`) e Cosmic como `key != 7` (`:25-27`) — **classificação possivelmente invertida**
> vs. o upstream (onde `key==7` + `WKSMissionToDoEvalutionRefin` é o ramo Cosmic). Vale conferir antes de
> qualquer mexida nessa área.

---

## 6. Attempted GC corruption fix (`3b07695`) — ✅ já coberto

**Upstream:** em `NodeScoresBuffer.cs`, troca `GC.AllocateUninitializedArray<ScoresBatch>(...)` (memória
**não** zerada — perigosa com os `Vector256` de `ScoresBatch`, fonte provável do crash/GC corruption) por
`new ScoresBatch[...]` (zerada), e comenta a reatribuição condicional `Data[...] = new();` (vira redundante).

**Fork:** a parte de **correctness já está presente** — `Artificer.Solver/NodeScoresBuffer.cs:22` já usa
`Data ??= new ScoresBatch[ArenaBuffer.BatchCount];` (grep por `AllocateUninitializedArray` = 0 no repo).
A única diferença é que as linhas 24-25 (`Data[...] = new();`) seguem **ativas** (upstream as comentou) —
**inócuo**: com o array já zerado, escrevem zeros sobre zeros. Port opcional (só limpeza/paridade).

---

## Recomendação

1. **Portar #1 (Robust condition)** — único gap de correctness, esforço pequeno, alvo claro
   (`Condition.cs` + `Simulator.cs`). Cobrir com teste no `Artificer.Test/Simulator`.
2. **Avaliar #2 (Raphael.Net 5.0)** — bump de engine; pequeno, mas faz sentido fazer **antes** de
   considerar #3/#4 (que dependem do solver atual). Atenção à ordem (csproj primeiro).
3. **#3 e #4** — só se quisermos alinhar o comportamento do solver ao upstream (melhoria de macros/perf);
   esforço grande, sem urgência.
4. **#5** — decisão de produto (isentar Cosmic do cap) + conferir a classificação `IsStudiumDelivery`.
5. **#6** — opcional (cosmético).

> Mudanças de **UI** do upstream (changelog window, ImRaii ref-struct — o fork tem `ImRaiiShim`,
> reescritas de janelas) **não se aplicam**: o fork tem sua própria camada de UI.
