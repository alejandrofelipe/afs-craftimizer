# Macro Card Table Borders — Design

**Date:** 2026-06-13  
**Status:** Approved  
**Scope:** `Artificer/Windows/RecipeNote.cs` — `DrawMacro` table flags only

---

## Problem

The macro card inside each GroupPanel ("Best Saved Macro", "Suggested Macro") uses an ImGui table with:

```csharp
ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH
```

This causes:
- **`BordersInnerH`** — horizontal lines between the action-icons row and the HQ%/name row, making the card look like a spreadsheet
- **`BordersOuter`** — an outer border rectangle that duplicates the GroupPanel's own border (double-border)
- **`BordersInnerV`** — a vertical line between the arcs column and the actions column (also redundant/visual noise)

## Other Occurrences

All other ImGui tables in the project use `BordersInnerV` only (intentional column separators for stats/data layouts in `MacroEditor` and `RecipeNote` stats panel). None have `BordersOuter` or `BordersInnerH`. The macro card table is the sole offender.

## Fix

Remove all three border flags. The table becomes a pure layout tool — invisible borders, two fixed-width columns, same row heights.

```csharp
// BEFORE:
using var table = ImRaii.Table("macroCard", 2,
    ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH,
    new Vector2(innerW, 0));

// AFTER:
using var table = ImRaii.Table("macroCard", 2,
    ImGuiTableFlags.None,
    new Vector2(innerW, 0));
```

Column widths and row heights are defined via `TableSetupColumn` + `TableNextRow(height)`, which are unaffected by border flags. The GroupPanel already provides the visual container.

## Files Changed

| File | Change |
|------|--------|
| `Artificer/Windows/RecipeNote.cs` | Remove border flags from `macroCard` table (line ~1044) |

## Rule Added to Knowledge Base

`ref_dalamud-gotchas.md` — new section: ImGui tables used for layout inside GroupPanels must not use `BordersOuter` (causes double-border) or `BordersInnerH` (causes spreadsheet-look row separators). Use `ImGuiTableFlags.None` + `WidthFixed` columns.
