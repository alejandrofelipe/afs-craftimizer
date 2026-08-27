# Padrões de UI — Design System

Padrões recorrentes no plugin. Cada padrão descreve quando usar, estrutura de código e o que evitar.

---

## Padrão: Stat Row com barra de progresso

Usado em: `MacroEditor`, `SynthHelper`, `RecipeNote`

Linha de stat com: cor da stat → nome → valor numérico → mini barra.

```csharp
void DrawStatRow(string label, int current, int max, Vector4 color)
{
    var pct = max > 0 ? (float)current / max : 0f;
    var scale = ImGuiHelpers.GlobalScale;

    using (ImRaii.PushColor(ImGuiCol.Text, color))
        ImGui.Bullet();
    ImGui.SameLine(0, 4 * scale);
    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
        ImGui.TextUnformatted(label);
    ImGui.SameLine();
    ImGui.SetNextItemWidth(60 * scale);
    using (ImRaii.PushColor(ImGuiCol.PlotHistogram, color))
        ImGuiUtils.ProgressBar(pct, new Vector2(-1, 4 * scale));
    ImGui.SameLine();
    using (ImRaii.PushColor(ImGuiCol.Text, pct >= 1f ? Colors.Good : color))
        ImGui.TextUnformatted(current.ToString());
}

// Uso:
DrawStatRow("Progress",   progress,     maxProgress,  Colors.Progress);
DrawStatRow("Quality",    quality,      maxQuality,   Colors.Quality);
DrawStatRow("Durability", durability,   maxDurability, Colors.Durability);
DrawStatRow("CP",         cp,           maxCp,        cp < minCp ? Colors.Bad : Colors.CP);
```

---

## Padrão: Group Panel com tabela de parâmetros

Usado em: `MacroEditor` (Craft Parameters, Character Stats)

```csharp
ImGuiUtils.BeginGroupPanel("Craft Parameters", -1);
{
    using var table = ImRaii.Table("craftparams", 2,
        ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp);

    void Row(string label, string value, Vector4? valueColor = null)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        using (valueColor.HasValue ? ImRaii.PushColor(ImGuiCol.Text, valueColor.Value) : null)
            ImGui.TextUnformatted(value);
    }

    Row("Recipe Level", recipe.Level.ToString());
    Row("Difficulty",   recipe.Difficulty.ToString());
    Row("Quality",      recipe.MaxQuality.ToString());
    Row("Durability",   recipe.Durability.ToString());
}
ImGuiUtils.EndGroupPanel();
```

---

## Padrão: Badge de categoria de ação inline

Usado em: `MacroEditor` (hotbar de ações), tooltips de ação

```csharp
void DrawActionBadge(CraftingAction action)
{
    var (label, color) = action.Category switch
    {
        ActionCategory.Synthesis => ("Synthesis", Colors.ActionSynth),
        ActionCategory.Touch     => ("Touch",     Colors.ActionTouch),
        ActionCategory.Buff      => ("Buff",      Colors.ActionBuff),
        ActionCategory.Special   => ("Special",   Colors.ActionSpecial),
        _                        => ("Other",      Vector4.One)
    };

    // Ícone da ação
    ImGui.Image(action.Icon, new Vector2(32 * scale));
    ImGui.SameLine(0, 4 * scale);

    // Badge inline abaixo do ícone
    using (ImRaii.Group())
    {
        ImGui.TextUnformatted(action.Name);
        ImGuiUtils.DrawBadgePill(label, color);
    }
}
```

---

## Padrão: Condition Indicator no SynthHelper

Usado em: `SynthHelper` — mostra a condição do step atual

```csharp
// No Draw() do SynthHelper, após calcular a condição do step
PluginImGuiUtils.DrawConditionIndicator(currentCondition, spacing: 8f * scale);

// Se quiser mostrar condição com tooltip de probabilidade:
PluginImGuiUtils.DrawConditionIndicator(currentCondition, 8f * scale);
if (ImGui.IsItemHovered())
    ImGuiUtils.TooltipWrapped($"Chance: {conditionChance:P0}");
```

---

## Padrão: Solver State Chip com botão de ação

Usado em: `MacroEditor` — chip de estado do solver ao lado do botão de cancelar/reiniciar

```csharp
// Mostra estado + botão de cancelar quando resolvendo
if (solverRunning)
{
    ImGuiUtils.DrawStateChip(SolverState.Solving);
    ImGui.SameLine(0, 8 * scale);

    Theme.PushDangerButton();
    if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Stop))
        CancelSolver();
    Theme.PopDangerButton();
}
else if (solverComplete)
{
    ImGuiUtils.DrawStateChip(SolverState.Complete);
}
else if (solverFailed)
{
    ImGuiUtils.DrawStateChip(SolverState.Failed);
}
```

---

## Padrão: Progress Bar de Solver com cores por stage

Usado em: `MacroEditor` — barra de progresso multi-cor do MCTS

```csharp
// Para cada stage da barra
for (int i = 0; i < stageCount; i++)
{
    var (bg, fg) = Colors.GetSolverProgressColors(i, config.ProgressBarStyle);
    float pct = GetStagePct(i);

    using (ImRaii.PushColor(ImGuiCol.PlotHistogram, fg))
    using (ImRaii.PushColor(ImGuiCol.FrameBg, bg))
    {
        ImGuiUtils.ProgressBar(pct, new Vector2(stageWidth, 8 * scale));
    }

    if (i < stageCount - 1)
        ImGui.SameLine(0, 2 * scale);
}
```

---

## Padrão: Row do Cosmic Tracker com highlight de mudança

Usado em: `CosmicTracker` — exibe research types com atualização em tempo real

```csharp
// Estado persistido por type
private readonly Dictionary<ResearchType, (int Value, DateTime LastChanged)> _lastValues = new();

void DrawResearchRow(ResearchType type, ResearchTypeData data)
{
    // Calcular delta desde o último frame
    int? delta = null;
    if (_lastValues.TryGetValue(type, out var prev) && data.Current != prev.Value)
    {
        delta = data.Current - prev.Value;
        _lastValues[type] = (data.Current, DateTime.Now);
    }
    else if (!_lastValues.ContainsKey(type))
    {
        _lastValues[type] = (data.Current, DateTime.MinValue);
    }

    // Só passar delta se dentro da janela de 10s
    var activeDelta = (_lastValues[type].LastChanged > DateTime.Now - TimeSpan.FromSeconds(10))
        ? delta
        : null;

    var state = DetermineState(data);
    ImGuiUtils.DrawResearchTypeRow(
        type.Label, data.Current, data.Needed, data.Max,
        state, availableWidth, activeDelta);
}

ResearchTypeState DetermineState(ResearchTypeData d) => d switch
{
    { IsLocked: true }          => ResearchTypeState.Locked,
    { Current: var c, Max: var m } when c >= m => ResearchTypeState.Maxed,
    { Current: var c, Needed: var n } when c >= n => ResearchTypeState.Complete,
    _                           => ResearchTypeState.Active,
};
```

---

## Padrão: Janela minimizável com estado de minimizar

Usado em: `CosmicTracker`, `SynthHelper`

```csharp
private bool _isMinimized = false;

protected override void Draw()
{
    Theme.Push();

    // Title bar com botão de minimize
    DrawTitleBar();

    if (!_isMinimized)
    {
        DrawFullContent();
    }
    else
    {
        DrawMinimizedContent();
    }

    Theme.Pop();
}

void DrawTitleBar()
{
    // Botão de minimize com cor ativa quando minimizado
    if (_isMinimized)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.Text, Colors.ActionBuff);
        if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Minus))
            _isMinimized = false;
    }
    else
    {
        if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Minus))
            _isMinimized = true;
    }
}
```

---

## Padrão: Hyperlink com cor de link

Usado em: settings, título de janela, rodapé do plugin

```csharp
using (ImRaii.PushColor(ImGuiCol.Text, Colors.Link))
    ImGuiUtils.Hyperlink("Support ↗", "https://ko-fi.com/...");

// Com texto antes
ImGui.TextUnformatted("For issues, visit ");
ImGui.SameLine(0, 0);
using (ImRaii.PushColor(ImGuiCol.Text, Colors.Link))
    ImGuiUtils.Hyperlink("GitHub Issues ↗", "https://github.com/...");
```

---

## Padrão: Valor com indicador Good/Bad

Usado em: qualquer lugar que exiba um valor com threshold

```csharp
// Stat abaixo do mínimo → vermelho; acima → verde
var meetsMin = craftsmanship >= recipe.MinCraftsmanship;
using (ImRaii.PushColor(ImGuiCol.Text, meetsMin ? Colors.Good : Colors.Bad))
    ImGui.TextUnformatted(craftsmanship.ToString());

// Com ícone de status
ImGui.SameLine(0, 4 * scale);
using (ImRaii.PushColor(ImGuiCol.Text, meetsMin ? Colors.Good : Colors.Bad))
    ImGui.TextUnformatted(meetsMin ? "✓" : "✗");
```

---

## Anti-padrões — O que evitar

| Anti-padrão | Por quê é ruim | Alternativa |
|---|---|---|
| Hardcode de hex como `new Vector4(0.32f, 0.89f, ...)` | Quebra quando Colors.cs muda | Usar `Colors.*` |
| `ImGui.PushStyleColor` sem `PopStyleColor` correspondente | Corrompe estilo de frames futuros | Usar `ImRaii.PushColor` (descarta automaticamente) |
| `Theme.Push()` sem `Theme.Pop()` no fim | Idem | Envolver em try/finally ou garantir retorno único |
| Espaçamento sem `* GlobalScale` | Quebra com scale ≠ 1.0 | `n * ImGuiHelpers.GlobalScale` |
| Usar `Colors.Progress` para "sucesso genérico" | Confunde com a stat de crafting | Usar `Colors.Good` para estados semânticos |
| Criar novo tom de azul para hover | Inconsistente com o theme | Usar `BgHover` via `ImGuiCol.ButtonHovered` |
| Desenhar texto com `ImGui.Text()` com `\n` embutido | Não quebra corretamente | `ImGuiUtils.TextWrappedTo()` ou `ImGui.TextWrapped()` |
| `ImGui.BeginChild` sem `EndChild` | Crasheia o ImGui | Usar `ImRaii.Child` (descarta automaticamente) |
