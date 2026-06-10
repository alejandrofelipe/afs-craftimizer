# Consolidação de Componentes UI — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminar código duplicado e padrões inconsistentes em `Craftimizer.UI/` sem alterar nenhum comportamento visual.

**Architecture:** Cinco tasks independentes, do menor para o maior risco. Cada task termina com um build limpo — nenhuma task quebra outra. Sem novas APIs públicas, sem mudança de comportamento.

**Tech Stack:** C# 13, ImGui.NET 1.90.9.1, `Craftimizer.UI` (net10.0-windows)

> **Nota sobre F3 (AlignRight em Cosmic.cs):** O achado foi um falso positivo. `ImGui.SameLine(barWidth - x)` posiciona na linha atual com offset absoluto; `ImGuiUtils.AlignRight` usa `SetCursorPosX` que soma ao cursor atual — comportamentos distintos. **Não substituir.**

---

## Arquivos Modificados

| Task | Arquivo | Ação |
|------|---------|------|
| 1 | `Craftimizer/Utils/UI/IFontHandleExtensions.cs` | Deletar 2 métodos sem callers |
| 2 | `Craftimizer.UI/ImGuiUtils.Cosmic.cs` | Extrair helper `GetResearchTypeColors` |
| 3 | `Craftimizer.UI/ImGuiUtils.Charts.cs` | Extrair função local `DrawCaps` |
| 4 | `Craftimizer.UI/ImGuiUtils.cs` | Unificar `Tooltip`/`TooltipWrapped` |
| 5 | `Craftimizer.UI/ImGuiUtils.Cosmic.cs` | Extrair `GetResearchTypeFractions` + mesclar rows |

---

### Task 1: Deletar métodos mortos de `IFontHandleExtensions`

**Arquivo:** `Craftimizer/Utils/UI/IFontHandleExtensions.cs`

`CalcTextSize` e `Text` não têm nenhum caller no projeto. `GetFontSize` tem 2 callers em `Settings.About.cs:36`.

- [ ] **Step 1: Verificar que CalcTextSize e Text não têm callers**

```powershell
cd c:\Users\aleja\DEV\Craftimizer
Select-String -Path "Craftimizer/**/*.cs" -Pattern "\.CalcTextSize\(|\.Text\(" -Recurse | Where-Object { $_ -notmatch "IFontHandleExtensions" }
```

Esperado: nenhuma linha envolvendo `IFontHandle` (pode aparecer `ImGui.CalcTextSize` — isso é diferente, é OK).

- [ ] **Step 2: Deletar os dois métodos**

Conteúdo final de `Craftimizer/Utils/UI/IFontHandleExtensions.cs`:
```csharp
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Craftimizer.Utils;

/// <summary>
/// Extension methods for IFontHandle.
/// Originally defined in ImGuiUtils.cs before it was moved to Craftimizer.UI.
/// Kept in the plugin because IFontHandle is a Dalamud type.
/// </summary>
internal static class IFontHandleExtensions
{
    public static float GetFontSize(this IFontHandle font)
    {
        using (font.Push())
            return ImGui.GetFontSize();
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Esperado: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```powershell
git add Craftimizer/Utils/UI/IFontHandleExtensions.cs
git commit -m "refactor(ui): deletar CalcTextSize e Text sem callers de IFontHandleExtensions"
```

---

### Task 2: Extrair `GetResearchTypeColors` em `ImGuiUtils.Cosmic.cs`

**Arquivo:** `Craftimizer.UI/ImGuiUtils.Cosmic.cs`

O switch `ResearchTypeState → cores` aparece duas vezes no mesmo arquivo. A versão em `DrawResearchTypeRow` (linhas 27–33) retorna uma tupla de 3 cores; a versão em `DrawResearchTypeRowMinimized` (linhas 169–181) usa dois switches separados. Ambas podem ser substituídas por uma única chamada ao helper privado.

- [ ] **Step 1: Adicionar helper privado logo antes de `DrawResearchTypeRow`**

Inserir antes da linha 22 (`public static void DrawResearchTypeRow`), após o comentário `// ── Cosmic Exploration UI helpers`:

```csharp
private static (Vector4 Label, Vector4 Num, Vector4 Marker) GetResearchTypeColors(ResearchTypeState state) => state switch
{
    ResearchTypeState.Active   => (Colors.CosmicActive,   Colors.CosmicActive   with { W = 0.8f }, Colors.CosmicUpgrade),
    ResearchTypeState.Complete => (Colors.CosmicComplete, Colors.CosmicComplete with { W = 0.8f }, Colors.CosmicComplete),
    ResearchTypeState.Maxed    => (Colors.CosmicMaxed,    Colors.CosmicMaxed    with { W = 0.8f }, Colors.CosmicMaxed),
    _                          => (Colors.CosmicLocked,   Colors.CosmicLocked   with { W = 0.5f }, Colors.CosmicUpgrade),
};
```

> Nota: no default `_`, `Marker` é `CosmicUpgrade` (não `CosmicLocked`) porque quando Locked o `upgradeFraction = 0` e o marker nunca é desenhado — a cor não importa, mas `CosmicUpgrade` é mais neutro.

- [ ] **Step 2: Substituir switch em `DrawResearchTypeRow` (linhas 27–33)**

Substituir:
```csharp
var (labelColor, numColor, upgradeColor) = state switch
{
    ResearchTypeState.Active   => (Colors.CosmicActive,   Colors.CosmicActive   with { W = 0.8f }, Colors.CosmicUpgrade),
    ResearchTypeState.Complete => (Colors.CosmicComplete,  Colors.CosmicComplete  with { W = 0.8f }, Colors.CosmicComplete),
    ResearchTypeState.Maxed    => (Colors.CosmicMaxed,     Colors.CosmicMaxed     with { W = 0.8f }, Colors.CosmicMaxed),
    _                          => (Colors.CosmicLocked,    Colors.CosmicLocked    with { W = 0.5f }, Colors.CosmicLocked),
};
```

Por:
```csharp
var (labelColor, numColor, upgradeColor) = GetResearchTypeColors(state);
```

- [ ] **Step 3: Substituir os dois switches em `DrawResearchTypeRowMinimized` (linhas 169–181)**

Substituir:
```csharp
var labelColor = state switch
{
    ResearchTypeState.Active   => Colors.CosmicActive,
    ResearchTypeState.Complete => Colors.CosmicComplete,
    ResearchTypeState.Maxed    => Colors.CosmicMaxed,
    _                          => Colors.CosmicLocked,
};
var markerColor = state switch
{
    ResearchTypeState.Complete => Colors.CosmicComplete,
    ResearchTypeState.Maxed    => Colors.CosmicMaxed,
    _                          => Colors.CosmicUpgrade,
};
```

Por:
```csharp
var (labelColor, _, markerColor) = GetResearchTypeColors(state);
```

- [ ] **Step 4: Build**

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Esperado: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5: Commit**

```powershell
git add Craftimizer.UI/ImGuiUtils.Cosmic.cs
git commit -m "refactor(ui): extrair GetResearchTypeColors helper — elimina switch duplicado em Cosmic"
```

---

### Task 3: Extrair `DrawCaps` local function em `DrawStatArc`

**Arquivo:** `Craftimizer.UI/ImGuiUtils.Charts.cs`

O cálculo `center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius)` aparece 4 vezes seguidas em `DrawStatArc` (linhas 30–31 e 38–39). Uma função local elimina a repetição.

- [ ] **Step 1: Extrair função local dentro de `DrawStatArc`**

O método atual (linhas 15–41) deve ficar assim:

```csharp
public static void DrawStatArc(ImDrawListPtr drawList, Vector2 screenPos, float size, float frac, Vector4 color)
{
    const float StartAngle = 2.269f;
    const float SweepAngle = 4.887f;

    var center  = screenPos + new Vector2(size * 0.5f, size * 0.5f);
    var strokeW = MathF.Max(2f, size * 0.16f);
    var radius  = size * 0.5f - strokeW * 0.5f - 1f;
    var capR    = strokeW * 0.5f;

    var trackColor = ImGui.GetColorU32(color with { W = 0.20f });
    var fillColor  = ImGui.GetColorU32(color);

    static void DrawCaps(ImDrawListPtr dl, Vector2 c, float r, float a0, float a1, float cR, uint col)
    {
        dl.AddCircleFilled(c + new Vector2(MathF.Cos(a0) * r, MathF.Sin(a0) * r), cR, col);
        dl.AddCircleFilled(c + new Vector2(MathF.Cos(a1) * r, MathF.Sin(a1) * r), cR, col);
    }

    drawList.PathArcTo(center, radius, StartAngle, StartAngle + SweepAngle, 32);
    drawList.PathStroke(trackColor, ImDrawFlags.None, strokeW);
    DrawCaps(drawList, center, radius, StartAngle, StartAngle + SweepAngle, capR, trackColor);

    if (frac > 0.005f)
    {
        var fillEnd = StartAngle + SweepAngle * MathF.Min(frac, 1f);
        drawList.PathArcTo(center, radius, StartAngle, fillEnd, 32);
        drawList.PathStroke(fillColor, ImDrawFlags.None, strokeW);
        DrawCaps(drawList, center, radius, StartAngle, fillEnd, capR, fillColor);
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Esperado: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

```powershell
git add Craftimizer.UI/ImGuiUtils.Charts.cs
git commit -m "refactor(ui): extrair DrawCaps local function — elimina math trigonométrico duplicado em DrawStatArc"
```

---

### Task 4: Unificar `Tooltip` e `TooltipWrapped`

**Arquivo:** `Craftimizer.UI/ImGuiUtils.cs`

`Tooltip` e `TooltipWrapped` (linhas 217–230) diferem apenas pelo wrap. A solução segura: adicionar `float? wrapWidth = null` ao `Tooltip` e fazer `TooltipWrapped` delegar para ele. Nenhum caller precisa mudar.

- [ ] **Step 1: Substituir os dois métodos (linhas 217–230)**

```csharp
public static void Tooltip(string text, float? wrapWidth = null)
{
    using var _font    = ImRaii.PushFont(UiServices.Current.DefaultFont);
    using var _tooltip = ImRaii.Tooltip();
    if (wrapWidth.HasValue)
        using var _wrap = ImRaii2.TextWrapPos(wrapWidth.Value * UiServices.Current.GlobalScale);
    ImGui.TextUnformatted(text);
}

public static void TooltipWrapped(string text, float width = 300) => Tooltip(text, width);
```

> `TooltipWrapped` é mantido como delegador de uma linha — os 12 callers existentes não precisam mudar.

- [ ] **Step 2: Build**

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Esperado: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

```powershell
git add Craftimizer.UI/ImGuiUtils.cs
git commit -m "refactor(ui): unificar Tooltip/TooltipWrapped — TooltipWrapped vira delegador de uma linha"
```

---

### Task 5: Extrair `GetResearchTypeFractions` + mesclar `DrawResearchTypeRow` variants

**Arquivo:** `Craftimizer.UI/ImGuiUtils.Cosmic.cs`

O cálculo de `fillFraction` / `upgradeFraction` é idêntico em `DrawResearchTypeRow` (linhas 95–99) e `DrawResearchTypeRowMinimized` (linhas 191–194). Além disso, as duas funções públicas podem ser mescladas em uma com enum `ResearchTypeRowMode`.

> Esta é a task mais complexa. Se o build falhar, reverta com `git restore Craftimizer.UI/ImGuiUtils.Cosmic.cs` e isole o problema.

- [ ] **Step 1: Adicionar enum e helper de frações no topo de `ImGuiUtils.Cosmic.cs` (após `ResearchTypeState`)**

Após a linha `public enum ResearchTypeState { Locked, Active, Complete, Maxed }`, adicionar:

```csharp
public enum ResearchTypeRowMode { Full, Minimized }

private static (float Fill, float Upgrade) GetResearchTypeFractions(
    ResearchTypeState state, int current, int needed, int max)
{
    if (state == ResearchTypeState.Locked)
        return (0f, 0f);
    var fill    = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
    var upgrade = (state != ResearchTypeState.Maxed && max > 0)
        ? Math.Clamp((float)needed / max, 0f, 1f)
        : 0f;
    return (fill, upgrade);
}
```

- [ ] **Step 2: Mesclar os dois métodos públicos em um único**

Substituir tanto `DrawResearchTypeRow` quanto `DrawResearchTypeRowMinimized` pelos seguintes métodos:

```csharp
/// <summary>
/// Draws a research-type row. Use <paramref name="mode"/> to switch between
/// Full (label + numbers + bar + sub-limits) and Minimized (label + bar, numbers in tooltip).
/// </summary>
public static void DrawResearchTypeRow(
    string label, int current, int needed, int max,
    ResearchTypeState state, float barWidth,
    ResearchTypeRowMode mode = ResearchTypeRowMode.Full,
    int? delta = null)
{
    if (mode == ResearchTypeRowMode.Minimized)
    {
        DrawResearchTypeRowMinimized(label, current, needed, max, state, barWidth);
        return;
    }

    var (labelColor, numColor, upgradeColor) = GetResearchTypeColors(state);
    var (fillFraction, upgradeFraction) = GetResearchTypeFractions(state, current, needed, max);

    using var id       = ImRaii.PushId(label);
    var drawList       = ImGui.GetWindowDrawList();
    var topLeft        = ImGui.GetCursorScreenPos();
    var highlightColor = ImGui.GetColorU32(Colors.CosmicChanged);

    drawList.ChannelsSplit(2);
    drawList.ChannelsSetCurrent(1);

    // ── Header: label + current/max ──────────────────────────────────────
    using (ImRaii.Group())
    {
        var suffix = state switch
        {
            ResearchTypeState.Active   => " ◄",
            ResearchTypeState.Complete => " ✓",
            ResearchTypeState.Maxed    => " ★",
            _                          => "",
        };

        using (ImRaii.PushColor(ImGuiCol.Text, labelColor))
            ImGui.TextUnformatted($"{label}{suffix}");

        string numText;
        if (state == ResearchTypeState.Locked)
        {
            numText = "— / —";
        }
        else if (delta is { } d && d > 0)
        {
            var baseText   = $"{current:N0} / {max:N0}";
            var deltaText  = $" (+{d:N0})";
            var totalWidth = ImGui.CalcTextSize(baseText).X + ImGui.CalcTextSize(deltaText).X;
            ImGui.SameLine(barWidth - totalWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, numColor))
                ImGui.TextUnformatted($"{current:N0}");
            ImGui.SameLine(0, 0);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicMission))
                ImGui.TextUnformatted(deltaText);
            ImGui.SameLine(0, 0);
            using (ImRaii.PushColor(ImGuiCol.Text, numColor))
                ImGui.TextUnformatted($" / {max:N0}");
            numText = null!;
        }
        else
        {
            numText = $"{current:N0} / {max:N0}";
        }

        if (numText != null)
        {
            var numWidth = ImGui.CalcTextSize(numText).X;
            ImGui.SameLine(barWidth - numWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, numColor))
                ImGui.TextUnformatted(numText);
        }
    }

    // ── Bar ──────────────────────────────────────────────────────────────
    DrawResearchTypeBar(fillFraction, upgradeFraction, upgradeColor, state,
        new Vector2(barWidth, 6f * UiServices.Current.GlobalScale));

    // ── Sub-limits ───────────────────────────────────────────────────────
    if (state == ResearchTypeState.Locked)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicLocked))
        {
            ImGui.TextUnformatted("—");
            ImGui.SameLine(barWidth - ImGui.CalcTextSize("—").X);
            ImGui.TextUnformatted("—");
        }
    }
    else if (state == ResearchTypeState.Maxed)
    {
        var maxLabel = $"máx: {max:N0} ★";
        var maxWidth = ImGui.CalcTextSize(maxLabel).X;
        ImGui.TextUnformatted("  ");
        ImGui.SameLine(barWidth - maxWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicMaxed with { W = 0.8f }))
            ImGui.TextUnformatted(maxLabel);
    }
    else
    {
        var upgradeLabel = state == ResearchTypeState.Complete
            ? $"upgrade: {needed:N0} ✓"
            : $"upgrade: {needed:N0}";
        var maxLabel = $"máx: {max:N0}";

        using (ImRaii.PushColor(ImGuiCol.Text, upgradeColor with { W = 0.7f }))
            ImGui.TextUnformatted(upgradeLabel);

        var maxWidth = ImGui.CalcTextSize(maxLabel).X;
        ImGui.SameLine(barWidth - maxWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted with { W = 0.6f }))
            ImGui.TextUnformatted(maxLabel);
    }

    ImGui.Spacing();

    // ── Background highlight ──────────────────────────────────────────────
    if (delta != null)
    {
        var bottomRight = new Vector2(topLeft.X + barWidth, ImGui.GetCursorScreenPos().Y);
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(topLeft, bottomRight, highlightColor, 4f * UiServices.Current.GlobalScale);
    }

    drawList.ChannelsMerge();
}

private static void DrawResearchTypeRowMinimized(
    string label, int current, int needed, int max,
    ResearchTypeState state, float barWidth)
{
    var labelWidth   = 60f * UiServices.Current.GlobalScale;
    var barAreaWidth = barWidth - labelWidth - ImGui.GetStyle().ItemSpacing.X;
    var (labelColor, _, markerColor) = GetResearchTypeColors(state);
    var (fillFraction, upgradeFraction) = GetResearchTypeFractions(state, current, needed, max);

    using var id = ImRaii.PushId(label);
    using (ImRaii.Group())
    {
        using (ImRaii.PushColor(ImGuiCol.Text, labelColor))
            ImGui.TextUnformatted(label);

        ImGui.SameLine(labelWidth);

        DrawResearchTypeBar(fillFraction, upgradeFraction, markerColor, state,
            new Vector2(barAreaWidth, 8f * UiServices.Current.GlobalScale));
    }

    if (ImGui.IsItemHovered())
    {
        var tip = state == ResearchTypeState.Maxed
            ? $"{current:N0} / {max:N0} ★"
            : $"{current:N0} / {max:N0}  —  upgrade: {needed:N0}";
        Tooltip(tip);
    }
}
```

> **Nota de design:** `DrawResearchTypeRowMinimized` é rebaixado a `private` — a API pública expõe apenas `DrawResearchTypeRow(..., mode)`. Quem chamava `DrawResearchTypeRowMinimized` diretamente precisa trocar para `DrawResearchTypeRow(..., mode: ResearchTypeRowMode.Minimized)`.

- [ ] **Step 3: Encontrar e atualizar callers de `DrawResearchTypeRowMinimized`**

```powershell
Select-String -Path "Craftimizer/**/*.cs" -Pattern "DrawResearchTypeRowMinimized" -Recurse
```

Para cada ocorrência, substituir:
```csharp
// antes
ImGuiUtils.DrawResearchTypeRowMinimized(label, current, needed, max, state, barWidth);

// depois
ImGuiUtils.DrawResearchTypeRow(label, current, needed, max, state, barWidth, ResearchTypeRowMode.Minimized);
```

- [ ] **Step 4: Build**

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Esperado: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5: Commit**

```powershell
git add Craftimizer.UI/ImGuiUtils.Cosmic.cs
git add Craftimizer/**/*.cs
git commit -m "refactor(ui): mesclar DrawResearchTypeRow variants + extrair GetResearchTypeFractions"
```

---

## Verificação Final

Após todos os commits:

```powershell
dotnet build Craftimizer/Craftimizer.csproj -c Release
```

Esperado: `Build succeeded. 0 Warning(s). 0 Error(s).`

```powershell
git log --oneline -6
```

Esperado: 5 commits de refactor + o commit anterior.
