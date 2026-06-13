# Icon Buttons Fix — Design

**Date:** 2026-06-13 (revised)
**Status:** Approved  
**Scope:** Fix `DrawCenteredIcon` so icon glyphs render inside `IconButtonSquare`; add centering unit tests and UIStudio visual story.

---

## Problem

`IconButtonSquare` renders a clickable button area but no icon glyph appears. The button is interactive (clicks register) but visually blank.

**Affected surfaces (all via `IconButtonSquare` → `DrawCenteredIcon`):**

| File | Line | Button |
|------|------|--------|
| `Artificer/Windows/CraftingHelper.cs` | ~1113 | Edit macro (FontAwesomeIcon.Edit) |
| `Artificer/Windows/CraftingHelper.cs` | ~1118 | Copy macro to clipboard (FontAwesomeIcon.Paste) |
| `Artificer/Windows/MacroClipboard.cs` | ~75 | Copy in clipboard window (FontAwesomeIcon.Paste) |

---

## Root Cause

The current `DrawCenteredIcon` uses:
```csharp
using (ImRaii.PushFont(UiServices.Current.IconFont))
    ImGui.GetWindowDrawList().AddText(
        ImGui.GetFont(),   // ← ImFontPtr obtained inside PushFont scope
        UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale,
        offset + iconOffset,
        col,
        icon.ToIconString());
```

`ImDrawList.AddText(ImFontPtr, float, Vector2, uint, string)` (5-arg overload) does **not render** when `ImFontPtr` is obtained via `ImGui.GetFont()` **inside** a `PushFont()` scope in Dalamud SDK 15. Confirmed: Charts and ProgressBar use the same 5-arg overload with `ImGui.GetFont()` captured **outside** PushFont and they render correctly. The issue is specific to fonts obtained from inside a PushFont scope.

**History:** A previous spec described a non-existent 4-arg `AddText(float, Vector2, uint, string)` overload. The implementer tried `ImGui.GetFont()` as a workaround for the missing overload — but this still calls the broken 5-arg path.

---

## Fix

Use `ImDrawList.AddText(Vector2, uint, string)` — the **3-arg overload** — inside the PushFont scope. This overload uses the currently active font from the ImGui font stack without requiring an explicit `ImFontPtr`, sidestepping the broken pattern entirely.

### `Artificer.UI/ImGuiUtils.cs` — `DrawCenteredIcon`

```csharp
// BEFORE (does not render):
private static void DrawCenteredIcon(FontAwesomeIcon icon, Vector2 offset, Vector2 size, bool isDisabled = false)
{
    var iconSize = GetIconSize(icon);

    float scale;
    Vector2 iconOffset;
    if (iconSize.X > iconSize.Y)
    {
        scale = size.X / iconSize.X;
        iconOffset = new(0, (size.Y - (iconSize.Y * scale)) / 2f);
    }
    else if (iconSize.Y > iconSize.X)
    {
        scale = size.Y / iconSize.Y;
        iconOffset = new((size.X - (iconSize.X * scale)) / 2f, 0);
    }
    else
    {
        scale = size.X / iconSize.X;
        iconOffset = Vector2.Zero;
    }

    using (ImRaii.PushFont(UiServices.Current.IconFont))
        ImGui.GetWindowDrawList().AddText(
            ImGui.GetFont(),
            UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale,
            offset + iconOffset,
            ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
            icon.ToIconString());
}

// AFTER (works):
private static void DrawCenteredIcon(FontAwesomeIcon icon, Vector2 offset, Vector2 size, bool isDisabled = false)
{
    var iconSize = GetIconSize(icon);
    var iconOffset = (size - iconSize) * 0.5f;

    using (ImRaii.PushFont(UiServices.Current.IconFont))
        ImGui.GetWindowDrawList().AddText(
            offset + iconOffset,
            ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
            icon.ToIconString());
}
```

**Why the centering simplifies:** The 3-arg overload renders at the font's natural size — the same size `GetIconSize` measures via `CalcTextSize` (both inside PushFont). Centering with `(size - iconSize) * 0.5f` is correct. The `scale` factor existed only to specify a custom size to the (now dropped) size argument.

**Assumption:** FontAwesome glyphs at icon font's native pixel size fit within a frame-height button. If `iconSize > size` the offset is negative (glyph clips). This does not occur for standard FontAwesome icons at normal DPI.

Also extract the offset calculation as an `internal static` method for testability:

```csharp
internal static Vector2 CenteredOffset(Vector2 iconSize, Vector2 area) =>
    (area - iconSize) * 0.5f;
```

And update `DrawCenteredIcon` to call `CenteredOffset(iconSize, size)`.

---

## Tests

### 1. Unit tests — centering math

New file: `Test/Artificer.Test/UI/DrawCenteredIconTests.cs`

```csharp
using Artificer.UI;
using NUnit.Framework;
using System.Numerics;

namespace Artificer.Test.UI;

[TestFixture]
public class DrawCenteredIconTests
{
    [TestCase(20f, 20f, 12f, 12f,  4f,  4f)]  // square icon in square area
    [TestCase(20f, 20f, 12f,  8f,  4f,  6f)]  // wider icon
    [TestCase(20f, 20f,  8f, 12f,  6f,  4f)]  // taller icon
    [TestCase(20f, 10f, 12f,  8f,  4f,  1f)]  // non-square area
    [TestCase(16f, 16f, 16f, 16f,  0f,  0f)]  // icon fills area exactly
    public void CenteredOffset_CentersWithinArea(
        float areaW, float areaH, float iconW, float iconH,
        float expectedX, float expectedY)
    {
        var result = ImGuiUtils.CenteredOffset(new(iconW, iconH), new(areaW, areaH));
        Assert.That(result.X, Is.EqualTo(expectedX).Within(0.01f));
        Assert.That(result.Y, Is.EqualTo(expectedY).Within(0.01f));
    }
}
```

The test project already uses NUnit and references `Artificer.UI`. No new dependencies needed.

### 2. UIStudio story — visual smoke test

New file: `Artificer.UIStudio/Stories/IconButtonsStory.cs`

A story page titled **"Icon Buttons"** that renders two rows:
- **Row 1 (Enabled):** `IconButtonSquare` with FontAwesomeIcon.Edit, FontAwesomeIcon.Paste, FontAwesomeIcon.Save, FontAwesomeIcon.Trash — labeled below each
- **Row 2 (Disabled):** Same icons wrapped in `ImRaii.Disabled()`, showing the grayed-out state

Each icon should display a visible glyph. If `DrawCenteredIcon` breaks, icons disappear — the story fails visually.

Follow the existing story pattern in `Artificer.UIStudio/Stories/`. Register the story in the story registry.

---

## Knowledge Base Update

Add to `ref_dalamud-gotchas.md`:

```
## AddText com fonte obtida dentro de PushFont

`ImDrawList.AddText(ImFontPtr, float, ...)` (5-arg) não renderiza quando `ImFontPtr`
é obtido via `ImGui.GetFont()` dentro de um `PushFont()` no Dalamud SDK 15.
Funciona com a fonte default (GetFont() sem PushFont ativo).

Padrão correto para render com fonte não-default via draw list:

    using (ImRaii.PushFont(someFont))
        drawList.AddText(pos, col, text);  // 3-arg: usa a fonte ativa no stack

Nunca:  drawList.AddText(ImGui.GetFont(), size, pos, col, text)  dentro de PushFont.
```

---

## Files Changed

| File | Change |
|------|--------|
| `Artificer.UI/ImGuiUtils.cs` | Rewrite `DrawCenteredIcon`; extract `CenteredOffset` as `internal static` |
| `Test/Artificer.Test/UI/DrawCenteredIconTests.cs` | New: unit tests for `CenteredOffset` |
| `Artificer.UIStudio/Stories/IconButtonsStory.cs` | New: visual story showing icon button variants |
| `C:\Users\aleja\.claude\projects\c--Users-aleja-DEV-Craftimizer\memory\ref_dalamud-gotchas.md` | Add AddText+PushFont gotcha |
