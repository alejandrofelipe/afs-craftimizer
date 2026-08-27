# Componentes — Design System

Catálogo de todos os componentes reutilizáveis, com assinatura C#, comportamento e exemplo de uso.

Fontes de verdade:
- **Reutilizáveis (sem Dalamud):** `Artificer.UI/ImGuiUtils*.cs` (`.cs`, `.Progress.cs`, `.Cosmic.cs`, `.Layout.cs`, `.EmptyState.cs`, `.Alert.cs`, `.Charts.cs`, `.SearchableCombo.cs`), `Artificer.UI/ProgressBarComponent.cs`, `Artificer.UI/ImRaii2.cs`.
- **Plugin-side (usam Dalamud):** `Artificer/Utils/UI/PluginImGuiUtils.*.cs`.

---

## GroupPanel

**Arquivo:** `ImGuiUtils.cs` → `BeginGroupPanel` / `EndGroupPanel`

Container com borda arredondada e label flutuante na borda superior.
A label usa `Colors.ActionBuff` (`#4AB8FF`) quando `accentLabel = true` (default).

### Assinatura

```csharp
float width = ImGuiUtils.BeginGroupPanel(string name, float width, bool accentLabel = true, Action? titleSuffix = null);
// ... conteúdo ...
ImGuiUtils.EndGroupPanel();

// Variante RAII (preferida) — fecha o painel automaticamente:
using (ImRaii2.GroupPanel(string name, float width, out float internalWidth, bool accentLabel = true, Action? titleSuffix = null))
{
    // ... conteúdo ...
}
```

Parâmetro `width`:
- `-1` → preenche o parent (mais comum)
- `0` → tamanho do conteúdo
- `> 0` → largura fixa em pixels (antes do scale)

Retorna a largura disponível para o conteúdo (útil com `width = -1`).

### Exemplo

```csharp
ImGuiUtils.BeginGroupPanel("Craft Parameters", -1);
ImGui.Text($"Difficulty: {recipe.Difficulty}");
ImGui.Text($"Durability: {recipe.Durability}");
ImGuiUtils.EndGroupPanel();
```

### Comportamento visual

- Borda: `ImGuiCol.Border` com rounding igual a `style.ItemSpacing.X`
- Label: desenhada sobre a borda superior (clip para "cortar" a linha)
- Sem label (string vazia): painel sem label, borda completa

---

## DrawStateChip

**Arquivo:** `ImGuiUtils.Progress.cs` → `DrawStateChip`

Dot animado + label de texto indicando estado do solver.
O dot pulsa quando o estado é `Solving`.

### Enum

```csharp
public enum SolverState { Solving, Complete, Suboptimal, Failed }
```

### Assinatura

```csharp
ImGuiUtils.DrawStateChip(SolverState state, string? label = null);
```

Omitir `label` usa o texto padrão: "Solving…", "Complete", "Suboptimal", "Failed".

### Mapeamento de cores

| Estado | Cor | Token |
|---|---|---|
| Solving | `Colors.ActionBuff` | `#4AB8FF` (pulsa) |
| Complete | `Colors.ConditionSturdy` | `#52E5A0` |
| Suboptimal | `Colors.ConditionGood` | `#FFB84A` |
| Failed | `new Vector4(1,0.36,0.43,1)` | `#FF5C6E` |

### Animação do dot

```csharp
// Calculado internamente:
var dotAlpha = state == SolverState.Solving
    ? MathF.Abs(MathF.Sin((float)(ImGui.GetTime() * MathF.PI / 0.6f))) * 0.6f + 0.4f
    : 1f;
// Período: ~1.2s, amplitude: 0.4–1.0
```

---

## DrawConditionIndicator

**Arquivo:** `Artificer/Utils/UI/PluginImGuiUtils.*.cs` → `PluginImGuiUtils.DrawConditionIndicator` (plugin-side — usa `Artificer.Simulator.Condition` e o extension `GetColor` de `Artificer/Utils/SimulatorUtils.cs`, por isso não fica em `Artificer.UI/`)

Círculo colorido animado seguido do nome da condição. A cor anima ao longo do tempo
para simular a transição de cor in-game.

### Assinatura

```csharp
PluginImGuiUtils.DrawConditionIndicator(Condition condition, float spacing);
```

- `condition`: valor do enum `Artificer.Simulator.Condition`
- `spacing`: gap em pixels entre o círculo e o texto (antes do scale)

### Comportamento visual

- Círculo: raio = `frameHeight / 2`, posicionado no cursor atual
- Cor: `new Vector4(.35f,.35f,.35f,0) + condition.GetColor(DateTime.UtcNow.TimeOfDay)`
- O texto usa a cor padrão do ImGui (`ImGuiCol.Text`)

### Mapeamento Condition → Cor base (Colors.cs)

```
Normal    → Colors.ConditionNormal    (#C7C7C7)
Good      → Colors.ConditionGood      (#FFB84A)
Excellent → Colors.ConditionExcellent (#FF6C8A)
Poor      → Colors.ConditionPoor      (#8A99BB)
Pliant    → Colors.ConditionPliant    (#4AB8FF)
Malleable → Colors.ConditionMalleable (#B07BFF)
Sturdy    → Colors.ConditionSturdy    (#52E5A0)
Primed    → Colors.ConditionPrimed    (#FF8C40)
```

---

## DrawBadgePill

**Arquivo:** `ImGuiUtils.Progress.cs` → `DrawBadgePill` / `CalcBadgePillSize`

Pill (badge com bordas totalmente arredondadas) com fill translúcido e borda na cor fornecida.

### Assinatura

```csharp
ImGuiUtils.DrawBadgePill(string text, Vector4 foreColor);
Vector2 size = ImGuiUtils.CalcBadgePillSize(string text);
```

### Especificações visuais

| Propriedade | Valor |
|---|---|
| Padding | `new Vector2(7f, 2f) * GlobalScale` |
| Rounding | `totalSize.Y / 2f` (pill perfeito) |
| Fill alpha | `foreColor.W = 0.14f` |
| Border alpha | `foreColor.W = 0.30f` |
| Texto | `foreColor` (alpha original) |

### Exemplos comuns

```csharp
// Badge de categoria de ação
ImGuiUtils.DrawBadgePill("Synthesis", Colors.ActionSynth);
ImGuiUtils.DrawBadgePill("Touch",     Colors.ActionTouch);

// Badge de stage do solver
ImGuiUtils.DrawBadgePill("Stage 3", Colors.ActionBuff);

// Badge de completude
ImGuiUtils.DrawBadgePill("HQ ✓",  Colors.HQ);
ImGuiUtils.DrawBadgePill("Failed", Colors.Bad);
```

---

## DrawBadge (ícone com imagem)

**Arquivo:** `ImGuiUtils.Progress.cs` → `DrawBadge`

Badge de imagem (ícone/texture) com tooltip no hover.

### Assinatura

```csharp
ImGuiUtils.DrawBadge(nint handle,  Vector2 size, string tooltip, Vector4? tint = null);
ImGuiUtils.DrawBadge(ulong handle, Vector2 size, string tooltip, Vector4? tint = null);
```

Usar para badges de job, ícones de recipe, e ícones de status.

---

## ProgressBar (customizado)

**Arquivo:** `ImGuiUtils.Progress.cs` → `ProgressBar`

Barra de progresso compatível com indeterminate state e overlay de texto.
Substitui `ImGui.ProgressBar` quando é necessário texto centralizado ou estado indeterminate.

### Assinatura

```csharp
ImGuiUtils.ProgressBar(float value, Vector2 size, string? overlay = null);
```

- `value` em `[0, 1]` → progresso normal
- `value < 0` → estado indeterminate (animação de "scanning")
- `size.X <= 0` → preenche a largura disponível (igual ao ImGui padrão)

### Como usar com cores de stat

```csharp
using var _ = ImRaii.PushColor(ImGuiCol.PlotHistogram, Colors.Quality);
ImGuiUtils.ProgressBar(qualityPct, new Vector2(-1, 14 * scale), $"{quality} / {maxQuality}");
```

---

## DrawResearchTypeRow

**Arquivo:** `ImGuiUtils.Cosmic.cs` → `DrawResearchTypeRow`

Row completa do Cosmic Tracker com header (label + valores), barra de progresso e sub-labels.
Em modo minimizado, renderiza internamente uma variante compacta (helper privado `DrawResearchTypeRowMinimized`: `label 60px + barra`, valores no tooltip).

### Assinatura

```csharp
ImGuiUtils.DrawResearchTypeRow(
    string label,
    int current, int needed, int max,
    ResearchTypeState state,
    float barWidth,
    int? delta = null   // se não-null: mostra "+delta" em Colors.CosmicMission e ativa o highlight
);
```

### Enum ResearchTypeState

```csharp
public enum ResearchTypeState { Locked, Active, Complete, Maxed }
```

### Comportamento por estado

| Estado | Label suffix | Cor da label | Cor da barra |
|---|---|---|---|
| Active | ` ◄` | `CosmicActive` | `CosmicActive` |
| Complete | ` ✓` | `CosmicComplete` | `CosmicComplete` |
| Maxed | ` ★` | `CosmicMaxed` | `CosmicMaxed` |
| Locked | — | `CosmicLocked` | fundo com `CosmicLocked @30%` |

### Highlight de atualização

Se `delta != null`, desenha `Colors.CosmicChanged` (amber 12%) como background da row.
O chamador deve controlar a janela de tempo de 10 segundos e passar `delta = null` após expirar.

---

## DrawResearchTypeBar

**Arquivo:** `ImGuiUtils.Cosmic.cs` → `DrawResearchTypeBar`

Barra primitiva do Cosmic Tracker. Usada internamente por `DrawResearchTypeRow`.
Usar diretamente apenas se precisar de uma barra Cosmic sem os textos.

### Assinatura

```csharp
ImGuiUtils.DrawResearchTypeBar(
    float fillFraction,      // 0.0–1.0
    float upgradeFraction,   // posição do marcador de upgrade (0 = não mostrar)
    Vector4 markerColor,
    ResearchTypeState state,
    Vector2 size             // altura recomendada: 6px*scale (normal) ou 8px*scale (minimized)
);
```

---

## DrawCosmicStageBadge

**Arquivo:** `ImGuiUtils.Cosmic.cs` → `DrawCosmicStageBadge`

Pill de estágio do Cosmic Tracker com estado de completude.

### Assinatura

```csharp
ImGuiUtils.DrawCosmicStageBadge(int stage, bool complete, int maxStage = 0);
```

Exemplos de output:
- `stage=3, complete=false, maxStage=5` → pill "Stage 3/5" em `CosmicActive`
- `stage=5, complete=true, maxStage=5` → pill "Stage 5/5 ✓" em `CosmicComplete`
- `stage=2, complete=false, maxStage=0` → pill "Stage 2 → 3" em `CosmicActive`

---

## IconButtonSquare

**Arquivo:** `ImGuiUtils.cs` → `IconButtonSquare`

Botão quadrado com ícone FontAwesome centralizado. Tamanho padrão: `frameHeight × frameHeight`.

### Assinatura

```csharp
bool clicked = ImGuiUtils.IconButtonSquare(FontAwesomeIcon icon, float size = -1);
```

- `size = -1` → usa `ImGui.GetFrameHeight()` (recomendado)
- Ícone é dimensionado para preencher o botão, mantendo aspect ratio

---

## Hyperlink

**Arquivo:** `ImGuiUtils.cs` → `Hyperlink`

Texto clicável que abre URL no browser. Sublinhado por padrão.
Cor: não aplica nenhuma cor de texto — usa `ImGuiCol.Text` atual (caller deve fazer push de cor se quiser `Colors.Link`).

### Assinatura

```csharp
ImGuiUtils.Hyperlink(string text, string url, bool underline = true);
```

### Exemplo (com cor de link)

```csharp
using (ImRaii.PushColor(ImGuiCol.Text, Colors.Link))
    ImGuiUtils.Hyperlink("Support on Ko-fi ↗", "https://ko-fi.com/...");
```

---

## Tooltip / TooltipWrapped

**Arquivo:** `ImGuiUtils.cs`

Tooltip padrão com `DefaultFont`. Sempre usar estas wrappers em vez de `ImGui.BeginTooltip()` diretamente.

```csharp
ImGuiUtils.Tooltip(string text);
ImGuiUtils.TooltipWrapped(string text, float width = 300);
```

`width` em pixels lógicos (será multiplicado por `GlobalScale`).

---

## Helpers de alinhamento

**Arquivo:** `ImGuiUtils.cs`

```csharp
ImGuiUtils.AlignCentered(float width, float availWidth = default);
ImGuiUtils.AlignRight(float width, float availWidth = default);
ImGuiUtils.AlignMiddle(Vector2 size, Vector2 availSize = default);

ImGuiUtils.TextCentered(string text, float availWidth = default);
ImGuiUtils.TextRight(string text, float availWidth = default);
ImGuiUtils.TextMiddleNewLine(string text, Vector2 availSize);

bool clicked = ImGuiUtils.ButtonCentered(string text, Vector2 buttonSize = default);
```

Todos calculam a posição usando `GetContentRegionAvail()` quando o parâmetro de tamanho não é fornecido.

---

## TextWrappedTo

**Arquivo:** `ImGuiUtils.cs`

Texto com quebra de linha em posição X específica, re-alinhando a partir de `basePosX`.

```csharp
ImGuiUtils.TextWrappedTo(string text, float wrapPosX = default, float basePosX = default);
```

Usar quando o texto precisa quebrar em coluna específica (ex: valor alinhado à direita de uma label).

---

## DrawSectionHeader

**Arquivo:** `ImGuiUtils.Layout.cs` → `DrawSectionHeader`

`Separator` + label em `Colors.ActionBuff`, com conteúdo opcional à direita (mesma linha).

```csharp
ImGuiUtils.DrawSectionHeader(string label, Action? rightContent = null);
```

---

## DrawStatRow

**Arquivo:** `ImGuiUtils.Layout.cs` → `DrawStatRow`

Label à esquerda + valor alinhado à direita, com cor opcional no valor.

```csharp
ImGuiUtils.DrawStatRow(string label, string value, Vector4? valueColor = null, float availWidth = 0);
```

---

## DrawEmptyState

**Arquivo:** `ImGuiUtils.EmptyState.cs` → `DrawEmptyState`

Estado vazio centralizado na vertical: ícone grande (muted) + título + subtítulo opcional + até 2 botões
(com dois botões, o primário usa `Theme.PushPrimaryButton()`). Há overload `int icon` para uso cross-assembly.

```csharp
ImGuiUtils.DrawEmptyState(
    FontAwesomeIcon icon,
    string title,
    string? subtitle = null,
    (string Label, Action Clicked)? primaryButton = null,
    (string Label, Action Clicked)? secondaryButton = null,
    float buttonWidth = 200f);
```

---

## DrawAlert

**Arquivo:** `ImGuiUtils.Alert.cs` → `DrawAlert`

Alerta compacto: barra colorida de 3px à esquerda + fundo tingido + título em maiúsculas + mensagem.
Todo o texto é desenhado via DrawList (o único item de layout é um `Dummy`), então **nunca** causa
crescimento horizontal da janela. Não referencia Dalamud — passar `ImGuiHelpers.GlobalScale` como `scale`.

```csharp
public enum AlertVariant { Info, Success, Warning, Danger }

ImGuiUtils.DrawAlert(AlertVariant variant, string title, string message, float scale = 1f, float width = -1f);
```

| Variante | Cor da barra |
|---|---|
| Info | azul `#3B82F5` |
| Success | `Colors.Good` (`#52E5A0`) |
| Warning | `Colors.ActionBuff` (`#4AB8FF`) |
| Danger | `Colors.Bad` (`#FF5C6E`) |

---

## IconButtonWithTooltip

**Arquivo:** `ImGuiUtils.cs` → `IconButtonWithTooltip`

`IconButtonSquare` + `HoveredTooltip` numa chamada. Overload `int icon` para uso cross-assembly.

```csharp
bool clicked = ImGuiUtils.IconButtonWithTooltip(FontAwesomeIcon icon, string tooltip, float size = -1, int flags = 0);
```

---

## HoveredTooltip

**Arquivo:** `ImGuiUtils.cs` → `HoveredTooltip`

`IsItemHovered(flags)` + `Tooltip` numa linha — o jeito preferido de anexar tooltip ao último item.

```csharp
ImGuiUtils.HoveredTooltip(string text, int flags = 0, float? wrapWidth = null);
```

---

## ProgressBarComponent

**Arquivo:** `Artificer.UI/ProgressBarComponent.cs`

Componente standalone da barra de progresso do solver, separado de `ImGuiUtils`. Consome
`ProgressSnapshot` + `VisualConfig` (`DisplayMode`, `ColorTheme`, `Width`, `ShowPercentage`).

```csharp
// Uma barra a partir de um snapshot:
ProgressBarComponent.DrawSingle(ProgressSnapshot snapshot, VisualConfig? config = null);

// Barra agregada: cada snapshot completo contribui 1/N ao total; tooltip lista todos:
ProgressBarComponent.DrawAggregated(IReadOnlyList<ProgressSnapshot> snapshots, VisualConfig? config = null);
```

---

## SearchableCombo

**Arquivo:** `ImGuiUtils.SearchableCombo.cs` → `SearchableCombo<T>`

Combo com campo de busca embutido (filtra os itens por `getString`).

```csharp
bool changed = ImGuiUtils.SearchableCombo<T>(
    string id, ref T selectedItem, IEnumerable<T> items,
    ImFontPtr selectableFont, float width,
    Func<T,string> getString, Func<T,string> getId, Action<T> draw) where T : IEquatable<T>;
```

---

## Charts — DrawStatArc / DrawBarRow

**Arquivo:** `ImGuiUtils.Charts.cs`

Primitivas de gráfico desenhadas via DrawList:

```csharp
// Arco de stat (gauge parcial) num DrawList:
ImGuiUtils.DrawStatArc(ImDrawListPtr drawList, Vector2 screenPos, float size, float frac, Vector4 color);

// Linha de barras horizontais a partir de uma lista de BarData:
ImGuiUtils.DrawBarRow(IReadOnlyList<BarData> bars, float? totalWidth = null);
```

---

## Extensões de IFontHandle

**Arquivo:** `ImGuiUtils.cs`

```csharp
float size   = font.GetFontSize();
Vector2 dim  = font.CalcTextSize(string text);
font.Text(string text);
```

Usar para renderizar texto com `Service.AxisFont`, `Service.HeaderFont` ou `Service.SubheaderFont`
sem precisar de `PushFont`/`PopFont` manual.
