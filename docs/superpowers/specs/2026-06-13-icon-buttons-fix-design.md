# Icon Buttons Fix — Design

**Date:** 2026-06-13  
**Status:** Approved  
**Scope:** `Artificer.UI/ImGuiUtils.cs` — `DrawCenteredIcon` only

---

## Problem

The Edit and Copy buttons in `RecipeNote.DrawMacro()` (and any other `IconButtonSquare` call) render as gray rectangles with no icon inside. The button widget appears correctly; only the FontAwesome glyph is invisible.

## Root Cause

`DrawCenteredIcon` draws the icon via:
```csharp
ImGui.GetWindowDrawList().AddText(
    UiServices.Current.IconFont,   // ← explicit ImFontPtr
    fontSize, pos, color, text);
```

In Dalamud SDK 15, the `ImFontPtr` obtained from `(nint)UiBuilder.IconFont.Handle` does not work correctly when passed as the explicit font argument to `AddText`. The overload `AddText(ImFontPtr, float, Vector2, uint, string)` renders nothing (font size resolves to 0 or the glyph is not found).

The `PushFont` → active-font path works correctly everywhere else in the project (e.g. the empty-state icon in `RecipeNote`, `GetIconSize` internally).

## Fix

In `DrawCenteredIcon` (`Artificer.UI/ImGuiUtils.cs`):

1. Push the icon font onto the ImGui font stack before `AddText`.
2. Use the overload `AddText(float size, Vector2 pos, uint col, string text)` — which draws using the **currently active font** (the one just pushed).
3. `GetIconSize` already pushes the same font for `CalcTextSize`, so the `scale` computation is unaffected.

```csharp
// BEFORE (broken in Dalamud SDK 15):
ImGui.GetWindowDrawList().AddText(
    UiServices.Current.IconFont,
    UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale,
    offset + iconOffset,
    ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
    icon.ToIconString());

// AFTER:
using (ImRaii.PushFont(UiServices.Current.IconFont))
    ImGui.GetWindowDrawList().AddText(
        UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale,
        offset + iconOffset,
        ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
        icon.ToIconString());
```

## Scope

| File | Change |
|------|--------|
| `Artificer.UI/ImGuiUtils.cs` | `DrawCenteredIcon`: wrap `AddText` with `PushFont` |

No callers change. All `IconButtonSquare` usages throughout the project are fixed automatically.

## Out of Scope

- Investigating why the explicit-font `AddText` overload fails in Dalamud SDK 15 (the fix avoids it; understanding the SDK internals is not required)
- Other `AddText` calls in the project (none use the explicit-font overload)
