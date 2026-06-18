# Gear Condition Alert — Reposition + Text Fix + Build Default

## Goal

1. Move the Gear Condition alert from inside the "Crafter / Recipe" GroupPanel to between that GroupPanel and "Best Saved Macro".
2. Fix the message text being cut off by removing the `(no data)` suffix from `BuildGearMessage()`.
3. Change the build script default from `Debug` to `Release` so `.\scripts\build.ps1` deploys directly to the folder the dev game server reads.

## Problem Statement

**Alert position**: The alert is currently inside the `GroupPanel("Crafter / Recipe", tableW)`. Its Dummy is bounded by the GroupPanel's clip rect. The alert logically belongs outside that container — it is a status message for the craft session, not a recipe stat.

**Text cutoff**: `BuildGearMessage()` produces `"27% · ~27 crafts left (no data)"` when the confidence estimate is 0. The text exceeds the available width at typical font scales and gets clipped. The `(no data)` suffix is redundant — the alert color already communicates urgency.

**Build default**: `.\scripts\build.ps1` defaults to Debug, writing to `bin\Debug`. The dev game server watches `bin\Release`. Developers must remember `-Configuration Release` on every build, which is error-prone.

## Architecture

### 1. Alert repositioning (`Artificer/Windows/CraftingHelper.cs`)

Remove the gear condition block from inside the GroupPanel. Add it after the GroupPanel closes, after the `CraftStatus != OK` early return, using `availWidth` (= `gpWidth`, the measured GroupPanel outer width).

**Why this is safe (no infinite growth):**
- `_anchoredMaxWidth` is computed from `gpWidth` immediately after the GroupPanel closes (line 455), before the alert renders.
- The alert's `Dummy(availWidth, totalH)` where `availWidth = gpWidth` fits exactly within the constrained window content area (`_anchoredMaxWidth − 2×WindowPadding = gpWidth`).
- No feedback loop: the alert Dummy cannot request more space than the window already provides.

New render order in `Draw()`:
```
1. GroupPanel "Crafter / Recipe" (stats table only, no alert)
2. GetItemRectSize() → _anchoredMaxWidth
3. if CraftStatus != OK → return
4. Spacing
5. availWidth = gpWidth; panelWidth = availWidth − itemSpacing×2
6. [NEW] if ShowGearCondition → DrawAlert(variant, "Gear Condition", msg, scale, availWidth) + Spacing
7. DrawMacro(bestMacro, panelWidth)
8. Spacing
9. DrawMacro(suggestedMacro, panelWidth)
10. ... buttons
```

**Code — remove from GroupPanel block (lines 433–451):**
```csharp
// DELETE:
if (CraftStatus == CraftableStatus.OK && _plugin.Configuration.ShowGearCondition)
{
    var gearCondition = Gearsets.GetMinimumGearCondition();
    if (gearCondition.HasValue)
    {
        var pct     = gearCondition.Value;
        var variant = pct < 25f ? AlertVariant.Danger
                    : pct < 50f ? AlertVariant.Warning
                    :             AlertVariant.Info;
        var message = PluginImGuiUtils.BuildGearMessage(
            pct,
            _plugin.Configuration.EnableGearWearTracking,
            RecipeData,
            _plugin.GearWearTracker);

        ImGuiHelpers.ScaledDummy(2);
        ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale, tableW);
    }
}
```

**Code — add after `var panelWidth = ...` (line 463):**
```csharp
if (_plugin.Configuration.ShowGearCondition)
{
    var gearCondition = Gearsets.GetMinimumGearCondition();
    if (gearCondition.HasValue)
    {
        var pct     = gearCondition.Value;
        var variant = pct < 25f ? AlertVariant.Danger
                    : pct < 50f ? AlertVariant.Warning
                    :             AlertVariant.Info;
        var message = PluginImGuiUtils.BuildGearMessage(
            pct,
            _plugin.Configuration.EnableGearWearTracking,
            RecipeData,
            _plugin.GearWearTracker);
        ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale, availWidth);
        ImGui.Spacing();
    }
}
```

### 2. Text fix (`Artificer/Utils/UI/PluginImGuiUtils.GearCondition.cs`)

Remove `(no data)` from the zero-confidence case. The estimate is still shown; the alert color communicates data quality.

```csharp
// BEFORE:
{ } e => $"{pct:0}% · ~{e.MinCrafts} crafts left (no data)",

// AFTER:
{ } e => $"{pct:0}% · ~{e.MinCrafts} crafts left",
```

### 3. Build script default (`scripts/build.ps1`)

Change line 49:

```powershell
# BEFORE:
[string]$Configuration = "Debug"

# AFTER:
[string]$Configuration = "Release"
```

`.\scripts\build.ps1` → Release (dev game server reads this)
`.\scripts\build.ps1 -Configuration Debug` → Debug (for attaching debugger)
`.\scripts\build.ps1 -Deploy` → Release + copy to XIVLauncher (unchanged)

## Files Changed

| File | Change |
|------|--------|
| `Artificer/Windows/CraftingHelper.cs` | Remove alert from GroupPanel; add after early return with `availWidth` |
| `Artificer/Utils/UI/PluginImGuiUtils.GearCondition.cs` | Drop `(no data)` from zero-confidence message |
| `scripts/build.ps1` | Change default Configuration from `Debug` to `Release` |

## Success Criteria

- Alert appears between "Crafter / Recipe" and "Best Saved Macro" panels
- Alert spans full window content width (`availWidth`)
- Gear condition message fits within the alert without clipping
- Window does not grow laterally after repositioning
- `.\scripts\build.ps1` builds Release by default; `-Deploy` still works
- Zero build warnings
