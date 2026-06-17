# CraftingHelper Refactor — Design Spec

## Goal

Fix the infinite lateral growth bug in the CraftingHelper window and improve the layout structure so the window has a single, stable source of truth for its width.

## Problem Statement

The current window width management is fragile:
1. `BeginGroupPanel(-1, ...)` uses `GetContentRegionAvail().X` each frame to size a `Dummy`, creating a feedback loop if any child item registers a slightly wider content width.
2. The `SameLine` trick inside the stats table captures `availWidth` as an approximation that includes `CellPadding` offsets — these can be slightly larger than the GroupPanel's inner width, causing frame-to-frame drift.
3. `DrawAlert` was moved outside the table (fixing visual span) but its `Dummy(availWidth, totalH)` is now the drift source.
4. Column widths (150px) are implicit — encoded in `TableSetupColumn` calls scattered across `Draw()` and `DrawCharacterStats()`, with no single constant.

## Architecture

### Single Width Constant

Introduce one constant at the top of `Draw()`:

```csharp
var colW    = 160f * ImGuiHelpers.GlobalScale;  // per stats column
var tableW  = colW * 2;                          // total stats table width
```

All widths in the function derive from `colW` / `tableW`. No `GetContentRegionAvail()` for layout decisions. No SameLine measurement.

### GroupPanel receives explicit width

```csharp
using (var crPanel = ImRaii2.GroupPanel("Crafter / Recipe", tableW, out _))
```

This prevents the GroupPanel from creating a `Dummy` sized to the window's current content region.

### `_anchoredMaxWidth` computed from constant, not from frame measurement

```csharp
_anchoredMaxWidth = tableW
    + ImGui.GetStyle().ItemSpacing.X * 4   // GroupPanel left + right indent
    + ImGui.GetStyle().WindowPadding.X * 2; // window left + right padding
```

Set once in `Draw()`, not derived from a runtime SameLine capture. Remove the SameLine trick entirely.

### DrawAlert uses DrawList for text — no layout text items

Current `DrawAlert` renders text via `SetCursorScreenPos` + `TextUnformatted`, which registers layout items that can extend beyond the `Dummy`. The fix: render title and message via `DrawList.AddText()` (screen-space only), keeping `Dummy(width, height)` as the sole layout item.

```
Dummy(width, height)    ← only layout item; controls window auto-size
AddText(title)          ← DrawList only, no layout impact
AddText(message)        ← DrawList only, no layout impact  
SetCursorScreenPos(below dummy)  ← reset cursor
```

This makes `DrawAlert` impossible to cause width drift regardless of where it is called.

### Column width: 150 → 160px

Slightly wider per column gives labels like "Craftsmanship" and values like "5208" more breathing room. Total table: 320px instead of 300px.

## Files Changed

| File | Change |
|------|--------|
| `Artificer.UI/ImGuiUtils.Alert.cs` | Replace `TextUnformatted` calls with `DrawList.AddText()`; keep `Dummy` as sole layout item; remove `PushTextWrapPos` (no longer needed) |
| `Artificer/Windows/CraftingHelper.cs` | Introduce `colW`/`tableW` constants; pass explicit width to GroupPanel; remove SameLine trick; recompute `_anchoredMaxWidth` from constants; remove `availWidth` parameter from DrawAlert call (width already embedded) |

## Gear Alert Placement

Alert stays inside the GroupPanel, after the stats table. It receives `tableW` as explicit width. Because `DrawAlert` no longer registers layout text items, it cannot cause window expansion.

## What Is Not Changing

- Visual structure of sections (Crafter/Recipe, Best Saved Macro, Suggested Macro, buttons)
- `DrawCharacterStats()` and `DrawRecipeStats()` internal logic
- `BuildGearMessage()` logic
- `_anchoredMaxWidth` `PreDraw()` mechanism (kept, just computed differently)

## Success Criteria

- Window width is stable across all frames — no drift or growth
- Gear alert spans full GroupPanel width
- Column width = 160px per column
- Zero build warnings
