# Separator & Divider Cleanup — Design

**Date:** 2026-06-13  
**Status:** Approved  
**Scope:** Remove visible vertical dividers from all tables, fix horizontal separator overflow in GroupPanel, replace one section separator with spacing.

---

## Problem

Three distinct visual issues across the codebase:

1. **Vertical dividers (`BordersInnerV`)** — All data/layout tables use `ImGuiTableFlags.BordersInnerV`, rendering a visible vertical line between columns. Per design intent, vertical dividers should not be visible (tables are layout tools; column content provides its own separation).

2. **Horizontal separator overflow** — `ImGui.Separator()` inside a `GroupPanel` draws from the window's left edge to its right edge, ignoring the GroupPanel's cliprect. Results in a line that visually bleeds outside the container. Affected: `MacroClipboard.cs:67`.

3. **Unnecessary horizontal separator** — `CraftingHelper.cs:437` places a `ImGui.Separator()` between the stats GroupPanel and the macro GroupPanels below. The GroupPanel borders already provide visual separation; the line is redundant and adds visual noise.

## Other Separators (Not Changed)

All other `ImGui.Separator()` calls in the project are at window level (not inside GroupPanels) and serve intentional purposes:
- Context menu item groupings (`CraftingListWindow.cs`)
- Modal confirmation dialogs (`DrawDeleteModal`, `DrawRenameModal`)
- Settings section titles (`Settings.cs:96 DrawSectionTitle`)
- CraftingList structural dividers (`CraftingListAddWindow`, `CraftingListDetailWindow`, `CraftingListMergeWindow`)
- CosmicTracker section break
- Tooltip separator in `ProgressBarComponent.cs`

These are kept unchanged.

---

## Fix

### 1. Remove `BordersInnerV` from all tables

Remove the flag from every table declaration. Column layout is unaffected — widths are controlled by `TableSetupColumn` + `SizingFixedSame` / `SizingStretchSame`, which are independent of border flags.

| File | Line | Before | After |
|------|------|--------|-------|
| `Artificer/Windows/CraftingHelper.cs` | ~410 | `BordersInnerV \| SizingFixedSame \| NoSavedSettings` | `SizingFixedSame \| NoSavedSettings` |
| `Artificer/Windows/MacroLibrary.cs` | ~232 | `BordersInnerV` | `ImGuiTableFlags.None` |
| `Artificer/Windows/MacroEditor.cs` | ~248 | `BordersInnerV \| SizingStretchSame` | `SizingStretchSame` |
| `Artificer/Windows/MacroEditor.cs` | ~264 | `BordersInnerV \| SizingStretchSame` | `SizingStretchSame` |
| `Artificer/Windows/MacroEditor.Recipe.cs` | ~170 | `BordersInnerV` | `ImGuiTableFlags.None` |
| `Artificer/Windows/MacroEditor.Recipe.cs` | ~196 | `BordersInnerV \| SizingStretchSame` | `SizingStretchSame` |
| `Artificer/Windows/MacroEditor.Character.cs` | ~42 | `BordersInnerV \| SizingStretchSame` | `SizingStretchSame` |
| `Artificer/Windows/MacroEditor.Character.cs` | ~80 | `BordersInnerV \| SizingStretchSame` | `SizingStretchSame` |

### 2. `CraftingHelper.cs:437` — Replace separator with spacing

```csharp
// BEFORE:
ImGui.Separator();

// AFTER:
ImGui.Spacing();
```

`ImGui.Spacing()` adds one line of `ItemSpacing.Y` vertical gap — enough to separate sections visually without a visible rule line.

### 3. `MacroClipboard.cs:67` — Replace overflowing separator with manual AddLine

```csharp
// BEFORE:
ImGui.Separator();

// AFTER:
var p = ImGui.GetCursorScreenPos();
ImGui.GetWindowDrawList().AddLine(
    p,
    p + new Vector2(availWidth, 0),
    ImGui.GetColorU32(ImGuiCol.Separator),
    1f);
ImGui.Dummy(new Vector2(availWidth, 1f));
```

`availWidth` is already in scope (declared in the `GroupPanel` call above). The `Dummy` advances the cursor past the drawn line so the next widget renders below it, not on top of it.

---

## Files Changed

| File | Change |
|------|--------|
| `Artificer/Windows/CraftingHelper.cs` | Remove `BordersInnerV` from stats table; `Spacing()` instead of `Separator()` at line ~437 |
| `Artificer/Windows/MacroLibrary.cs` | Remove `BordersInnerV` |
| `Artificer/Windows/MacroEditor.cs` | Remove `BordersInnerV` from 2 tables |
| `Artificer/Windows/MacroEditor.Recipe.cs` | Remove `BordersInnerV` from 2 tables |
| `Artificer/Windows/MacroEditor.Character.cs` | Remove `BordersInnerV` from 2 tables |
| `Artificer/Windows/MacroClipboard.cs` | Replace `Separator()` with `AddLine` + `Dummy` |

---

## Knowledge Base Update

Add to `ref_dalamud-gotchas.md`:
- `ImGui.Separator()` inside a GroupPanel draws to window width, not GroupPanel width — causes overflow. Fix: use `AddLine` on `GetWindowDrawList()` with `availWidth` + `Dummy` to advance cursor.
- `ImGuiTableFlags.BordersInnerV` should not be used in this project — visible column dividers are unwanted in all current tables. Use `ImGuiTableFlags.None` or omit from flag combination.
