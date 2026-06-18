# Cores — Design System

Fonte de verdade: `Artificer.UI/Colors.cs`

Todos os tokens são `public static readonly Vector4` (RGBA, valores 0.0–1.0).
Para converter para hex: `Math.Round(component * 255)` → hex de 2 dígitos.

---

## Stats de Crafting

Usados em progress bars, labels, bordas de badges e ícones de stat.
Cada stat tem **exatamente uma cor** — nunca usar uma no lugar da outra.

| Token C# | CSS Token | Hex | Uso |
|---|---|---|---|
| `Colors.Progress` | `--progress` | `#52E5A0` | Barra e label de Progress |
| `Colors.Quality` | `--quality` | `#B07BFF` | Barra e label de Quality |
| `Colors.Durability` | `--durability` | `#FFB84A` | Barra e label de Durability |
| `Colors.CP` | `--cp` | `#FF6C8A` | Barra e label de CP |
| `Colors.Collectability` | `--collectability` | `#42C4E8` | Barra e label de Collectability |
| `Colors.HQ` | `--hq` | `#97DC60` | Badge de item HQ |

### Como usar em ImGui

```csharp
// Cor de texto para uma stat
ImGui.TextColored(Colors.Progress, $"{progress} / {maxProgress}");

// Barra de progresso colorida
ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Colors.Quality);
ImGui.ProgressBar(qualityPct, barSize);
ImGui.PopStyleColor();
```

---

## Categorias de Ação

Usados em hotbars de macro, tooltips de ação e badges de categoria.
São aliases dos stats — **não criar novas cores**, usar os aliases.

| Token C# | Alias de | Hex | Categoria |
|---|---|---|---|
| `Colors.ActionSynth` | `Colors.Progress` | `#52E5A0` | Synthesis actions |
| `Colors.ActionTouch` | `Colors.Quality` | `#B07BFF` | Touch actions |
| `Colors.ActionBuff` | — | `#4AB8FF` | Buff actions |
| `Colors.ActionSpecial` | `Colors.Durability` | `#FFB84A` | Special actions |

### Como usar

```csharp
// Tint de ícone de ação
var tint = action.Category switch {
    ActionCategory.Synthesis => Colors.ActionSynth,
    ActionCategory.Touch     => Colors.ActionTouch,
    ActionCategory.Buff      => Colors.ActionBuff,
    ActionCategory.Special   => Colors.ActionSpecial,
    _                        => Vector4.One
};
ImGui.Image(actionIcon, iconSize, Vector2.Zero, Vector2.One, tint);
```

---

## Condições de Crafting

Usados no `SynthHelper` para o `ConditionIndicator` e em tooltips de step.

| Token C# | Hex | Condição FFXIV |
|---|---|---|
| `Colors.ConditionNormal` | `#C7C7C7` | Normal |
| `Colors.ConditionGood` | `#FFB84A` | Good |
| `Colors.ConditionExcellent` | `#FF6C8A` | Excellent |
| `Colors.ConditionPoor` | `#8A99BB` | Poor |
| `Colors.ConditionPliant` | `#4AB8FF` | Pliant |
| `Colors.ConditionMalleable` | `#B07BFF` | Malleable |
| `Colors.ConditionSturdy` | `#52E5A0` | Sturdy |
| `Colors.ConditionPrimed` | `#FF8C40` | Primed |

### Como usar

```csharp
var condColor = condition switch {
    Condition.Good      => Colors.ConditionGood,
    Condition.Excellent => Colors.ConditionExcellent,
    Condition.Poor      => Colors.ConditionPoor,
    Condition.Pliant    => Colors.ConditionPliant,
    Condition.Malleable => Colors.ConditionMalleable,
    Condition.Sturdy    => Colors.ConditionSturdy,
    Condition.Primed    => Colors.ConditionPrimed,
    _                   => Colors.ConditionNormal,
};
// Dot com glow
ImGuiUtils.DrawConditionDot(condColor);
```

---

## Cosmic Exploration

Usados exclusivamente no `CosmicTracker` e `CosmicToolTracker`. Não usar em outras janelas.

| Token C# | Hex | Significado |
|---|---|---|
| `Colors.CosmicActive` | `#B07AFF` | Research type em coleta ativa |
| `Colors.CosmicComplete` | `#55B855` | Threshold de upgrade atingido |
| `Colors.CosmicLocked` | `#383847` | Ainda não desbloqueado |
| `Colors.CosmicMission` | `#F0B830` | Stellar Mission ativa |
| `Colors.CosmicUpgrade` | `#9973E6` | Marcador de threshold de upgrade na barra |
| `Colors.CosmicMaxed` | `#FFD966` | XP máximo absoluto atingido |
| `Colors.CosmicChanged` | `rgba(240,184,48, 0.12)` | Highlight temporário de 10s após update |

### Lógica de estado da barra Cosmic

```csharp
Vector4 barColor = (current, max, needed) switch {
    _ when current >= max    => Colors.CosmicMaxed,
    _ when current >= needed => Colors.CosmicComplete,
    _ when isActive          => Colors.CosmicActive,
    _                        => Colors.CosmicLocked,
};
```

### Highlight temporário

```csharp
// Mostrar background amber por 10 segundos após mudança de valor
if (DateTime.Now - lastChanged < TimeSpan.FromSeconds(10)) {
    var dl = ImGui.GetWindowDrawList();
    dl.AddRectFilled(rowMin, rowMax, ImGui.ColorConvertFloat4ToU32(Colors.CosmicChanged));
}
```

---

## Estados Semânticos

Estados genéricos de UI. Usados em qualquer janela para comunicar resultado/status.

| Token C# | CSS Token | Hex | Uso |
|---|---|---|---|
| `Colors.Good` | `--good` | `#52E5A0` | Valor suficiente, ação válida |
| `Colors.Bad` | `--bad` | `#FF5C6E` | Valor insuficiente, ação inválida |
| `Colors.Link` | `--link` | `#4AB8FF` | Hyperlinks, suporte, label ativa |
| `Colors.Disabled` | `--disabled` | `rgba(128,128,128, 0.75)` | Elemento desativado |
| `Colors.TextMuted` | `--text-muted` | `#50607A` | Labels secundários, valores idle |

### Regra de uso: Good vs Bad

```csharp
// Stat atingiu o mínimo necessário → verde
// Stat abaixo do mínimo → vermelho
var statColor = craftsmanship >= minCraftsmanship ? Colors.Good : Colors.Bad;
ImGui.TextColored(statColor, craftsmanship.ToString());
```

---

## Solver Progress

Cores dos segmentos de progresso do solver MCTS/Raphael.
Usadas **apenas** em `GetSolverProgressColors()` — não acessar os arrays diretamente.

### Modo Colorful (7 cores, loop)

| Index | Hex |
|---|---|
| 0 | `#DE313D` (vermelho) |
| 1 | `#F59E1E` (laranja) |
| 2 | `#F7D600` (amarelo) |
| 3 | `#5EB05A` (verde) |
| 4 | `#364DFA` (azul) |
| 5 | `#429EF0` (azul claro) |
| 6 | `#B37CE0` (violeta) |

### Modo Simple (6 tons de cinza)

`#545454` → `#707070` → `#8F8F8F` → `#ADADAD` → `#CECECE` → `#EDEDED`

### Como usar

```csharp
var (bg, fg) = Colors.GetSolverProgressColors(stageValue, config.ProgressBarStyle);
ImGui.PushStyleColor(ImGuiCol.PlotHistogram, fg);
ImGui.PushStyleColor(ImGuiCol.FrameBg, bg);
ImGui.ProgressBar(pct, size);
ImGui.PopStyleColor(2);
```

---

## Collectability Thresholds

Cores dos 3 thresholds de Collectability. Usadas no overlay de collectability.

| Token C# | Hex |
|---|---|
| `Colors.CollectabilityThreshold[0]` | `#78C7EE` (azul) |
| `Colors.CollectabilityThreshold[1]` | `#FDCA00` (amarelo) |
| `Colors.CollectabilityThreshold[2]` | `#BFFFBF` (verde claro) |

---

## Badge de Especialista

| Token C# | Hex | Uso |
|---|---|---|
| `Colors.SpecialistGold` | `#FCFA9E` | Badge "Specialist" em receitas que exigem especialista |

---

## Feature Hub

Cores do ícone de âncora e do launcher do FeatureHub.

| Token C# | Hex | Uso |
|---|---|---|
| `Colors.FeatureHubAnchored` | `#3B9EE8` | Ícone âncora quando o hub está fixado ao NaviMap |
| `Colors.FeatureHubFree`     | `#595959` | Ícone âncora quando o hub está flutuante (livre) |
| `Colors.FeatureHubGold`     | `#C7A86E` | Ícone hammer (minimizar) do hub |

---

## Regras gerais

1. **Nunca hardcode hex** — sempre usar `Colors.*`
2. **Nunca misturar contextos** — cor de stat não vira cor de estado, cor de Cosmic não vira cor de buff
3. **Alpha via multiplicação** — `new Vector4(color.X, color.Y, color.Z, 0.14f)` para fill de badge
4. **Glow via `box-shadow`** (HTML) ou `AddCircleFilled` com alpha baixo (ImGui) para condition dots
5. **Disabled = qualquer cor × 0.75 alpha** — usar `Colors.Disabled` como multiplicador ou sobrepor o alpha
