# CraftingHelper Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the infinite lateral growth bug in the CraftingHelper window and simplify width management to a single stable constant (`colW = 160 × GlobalScale`).

**Architecture:** Two independent changes: (1) `DrawAlert` switches from `TextUnformatted`+`SetCursorScreenPos` to `DrawList.AddText`, making the Dummy the only layout item — no text can register width beyond it; (2) `CraftingHelper.Draw()` replaces the fragile `SameLine` width-capture trick with an explicit column constant and reads the GroupPanel's actual rendered width via `GetItemRectSize()` after it closes.

**Tech Stack:** C#, ImGui.NET (Dalamud cimgui wrapper), `ImGui.GetWindowDrawList().AddText`, `ImGui.Dummy`, `ImGui.GetItemRectSize`

---

## File Map

| File | Change |
|------|--------|
| `Artificer.UI/ImGuiUtils.Alert.cs` | Replace `TextUnformatted` + `SetCursorScreenPos` with `DrawList.AddText`; remove `PushTextWrapPos`; keep `Dummy` as sole layout item |
| `Artificer/Windows/CraftingHelper.cs` | Introduce `colW`/`tableW` constants; pass explicit `tableW` to GroupPanel; always setup table columns unconditionally; remove `SameLine` hack; compute `_anchoredMaxWidth` from `GetItemRectSize()` |
| `Artificer.UIStudio/Stories/AlertStory.cs` | Verify visual output unchanged — no code change expected, but must run UIStudio to confirm |

---

## Task 1: Refactor `DrawAlert` — DrawList text, single Dummy

**Files:**
- Modify: `Artificer.UI/ImGuiUtils.Alert.cs` (full file replacement)

**Context:**
Current `DrawAlert` renders title and message via `ImGui.TextUnformatted` after repositioning with `SetCursorScreenPos`. These text items register their width in ImGui's content size tracker, which can cause the window to expand. The fix: render all text through `ImGui.GetWindowDrawList().AddText()` (screen-space draw, no layout impact), and use `ImGui.Dummy(availW, totalH)` as the sole layout item.

The `AddText(Vector2, uint, string)` overload uses the currently active font and font size — which is exactly what's active in the normal draw context.

- [ ] **Step 1: Replace the full content of `ImGuiUtils.Alert.cs`**

Open `Artificer.UI/ImGuiUtils.Alert.cs` and replace everything with:

```csharp
namespace Artificer.Utils;

/// <summary>Visual variant for <see cref="ImGuiUtils.DrawAlert"/>.</summary>
public enum AlertVariant { Info, Success, Warning, Danger }

public static partial class ImGuiUtils
{
    /// <summary>
    /// Compact alert: 3px colored left bar + tinted background + uppercase title + body message.
    /// All text is rendered via DrawList — the sole layout item is <c>Dummy(width, height)</c>,
    /// so this component cannot cause horizontal window growth regardless of text length or placement.
    /// Does not reference Dalamud — pass <c>ImGuiHelpers.GlobalScale</c> as <paramref name="scale"/> from plugin context.
    /// </summary>
    public static void DrawAlert(AlertVariant variant, string title, string message, float scale = 1f, float width = -1f)
    {
        var barColor = variant switch
        {
            AlertVariant.Success => Colors.Good,
            AlertVariant.Warning => Colors.ActionBuff,
            AlertVariant.Danger  => Colors.Bad,
            _                    => new Vector4(0.23f, 0.51f, 0.96f, 1f),
        };

        var dl     = ImGui.GetWindowDrawList();
        var pos    = ImGui.GetCursorScreenPos();
        var availW = width > 0 ? width : ImGui.GetContentRegionAvail().X;
        var padX   = ImGui.GetStyle().WindowPadding.X;
        var padY   = ImGui.GetStyle().FramePadding.Y;
        var lineH  = ImGui.GetTextLineHeightWithSpacing();
        var totalH = lineH + ImGui.GetTextLineHeight() + padY * 2f;
        var barW   = 3f * scale;
        var textX  = pos.X + barW + padX;

        dl.AddRectFilled(pos, pos + new Vector2(availW, totalH),
            ImGui.ColorConvertFloat4ToU32(barColor with { W = 0.08f }));
        dl.AddRectFilled(pos, pos + new Vector2(barW, totalH),
            ImGui.ColorConvertFloat4ToU32(barColor));
        dl.AddText(new Vector2(textX, pos.Y + padY),
            ImGui.ColorConvertFloat4ToU32(barColor), title.ToUpperInvariant());
        dl.AddText(new Vector2(textX, pos.Y + padY + lineH),
            ImGui.ColorConvertFloat4ToU32(Colors.TextMuted), message);

        ImGui.Dummy(new Vector2(availW, totalH));
    }
}
```

Key differences from the old version:
- No `PushTextWrapPos` / `PopTextWrapPos`
- No `SetCursorScreenPos` calls inside the Dummy area
- No `ImGui.TextUnformatted` calls
- No explicit `SetCursorScreenPos` at the end (the Dummy already advances the cursor to `pos + (0, totalH + ItemSpacing.Y)`)
- `width` parameter kept — callers can still pass explicit width to prevent feedback with auto-sizing windows

- [ ] **Step 2: Build to confirm zero warnings**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
.\scripts\build.ps1 2>&1 | Select-String -Pattern "warning|error|Build succeeded|FAILED"
```

Expected output:
```
Building Artificer 2.22.0.0 (Debug)...
Building Artificer 2.22.0.0 (Release)...
Build succeeded.
```

If there are build errors, the most likely cause is that `dl.AddText(Vector2, uint, string)` is not exposed by the Dalamud cimgui version. In that case, fall back to the font overload:

```csharp
var font     = ImGui.GetFont();
var fontSize = ImGui.GetFontSize();
dl.AddText(font, fontSize, new Vector2(textX, pos.Y + padY),
    ImGui.ColorConvertFloat4ToU32(barColor), title.ToUpperInvariant());
dl.AddText(font, fontSize, new Vector2(textX, pos.Y + padY + lineH),
    ImGui.ColorConvertFloat4ToU32(Colors.TextMuted), message);
```

- [ ] **Step 3: Run UIStudio to verify DrawAlert renders correctly**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run --project Artificer.UIStudio
```

Navigate to **Molecules → Alert** story. Verify all four variants render correctly: colored bar, tinted background, uppercase title, muted message. The visual output should be identical to before.

- [ ] **Step 4: Commit Task 1**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
git add Artificer.UI/ImGuiUtils.Alert.cs
git commit -m "refactor(ui): DrawAlert — DrawList text, single Dummy layout item"
```

---

## Task 2: Stable width management in `CraftingHelper.Draw()`

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs` — `Draw()` method only (lines 410–581 approximately)

**Context:**
The current `Draw()` has three fragile width-management mechanisms that interact badly:
1. `var availWidth = ImGui.GetContentRegionAvail().X` — window content width on current frame (can be large on frame 1)
2. `SameLine` trick inside the table — captures cursor X to approximate table width
3. `availWidth += ItemSpacing*2; _anchoredMaxWidth = availWidth + WindowPadding*2` — derived from the SameLine capture

The SameLine capture can be slightly wider than the GroupPanel's inner content area due to CellPadding offsets, causing frame-to-frame drift when the DrawAlert (now outside the table) registers its Dummy width.

The fix introduces a **single width constant** `colW = 160f * GlobalScale` and **`tableW = colW * 2`**, which eliminates the circular dependency. `_anchoredMaxWidth` is computed from `ImGui.GetItemRectSize().X` immediately after the GroupPanel closes — this reads the GroupPanel's actual rendered width deterministically (stable from frame 1 because the GroupPanel now uses explicit width).

- [ ] **Step 5: Replace the `Draw()` method's opening block (width setup + GroupPanel)**

In `CraftingHelper.cs`, find the `Draw()` method starting at line ~410. Replace from `public override void Draw()` through the GroupPanel's closing brace and the `_anchoredMaxWidth` computation (through line ~460). The exact block to replace:

**OLD CODE (lines 410–460):**
```csharp
    public override void Draw()
    {
        IsCollapsed = false;

        var availWidth = ImGui.GetContentRegionAvail().X;
        using (var crPanel = ImRaii2.GroupPanel("Crafter / Recipe", -1, out _))
        {
            if (crPanel)
            {
                using var table = ImRaii.Table("stats", 2, ImGuiTableFlags.SizingFixedSame | ImGuiTableFlags.NoSavedSettings);
                if (table)
                {
                    if (StatsChanged)
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
                    }

                    ImGui.TableNextColumn();
                    DrawCharacterStats();
                    ImGui.TableNextColumn();
                    DrawRecipeStats();

                    // Ensure that we know the window should be the same size as this table. Any more and it'll grow slowly and won't shrink when it could
                    ImGui.SameLine(0, 0);
                    availWidth = ImGui.GetCursorPosX() - ImGui.GetStyle().WindowPadding.X + ImGui.GetStyle().CellPadding.X;
                }

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
                        ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale, availWidth);
                    }
                }
            }
        }
        availWidth += ImGui.GetStyle().ItemSpacing.X * 2;
        _anchoredMaxWidth = availWidth + ImGui.GetStyle().WindowPadding.X * 2;
```

**NEW CODE:**
```csharp
    public override void Draw()
    {
        IsCollapsed = false;

        var colW   = 160f * ImGuiHelpers.GlobalScale;
        var tableW = colW * 2;

        using (var crPanel = ImRaii2.GroupPanel("Crafter / Recipe", tableW, out _))
        {
            if (crPanel)
            {
                using var table = ImRaii.Table("stats", 2, ImGuiTableFlags.SizingFixedSame | ImGuiTableFlags.NoSavedSettings);
                if (table)
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, colW);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, colW);

                    ImGui.TableNextColumn();
                    DrawCharacterStats();
                    ImGui.TableNextColumn();
                    DrawRecipeStats();
                }

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
            }
        }
        var gpWidth = ImGui.GetItemRectSize().X;
        _anchoredMaxWidth = gpWidth + ImGui.GetStyle().WindowPadding.X * 2;
```

What changed and why:
- `var availWidth = ImGui.GetContentRegionAvail().X` → **removed** (was initial frame-dependent value, now unnecessary)
- `colW = 160f * GlobalScale` → **new** (single width constant, 10px wider than previous 150)
- `tableW = colW * 2` → **new** (total stats table width = 320px at 1x scale)
- `GroupPanel("Crafter / Recipe", -1, ...)` → `GroupPanel("Crafter / Recipe", tableW, ...)` → **explicit width eliminates the GroupPanel's frame-dependent Dummy**
- `if (StatsChanged) { TableSetupColumn... }` → **removed guard**, columns always set up per frame (correct ImGui usage — `TableSetupColumn` belongs every frame before row data)
- `150 * GlobalScale` → `colW` → column width updated
- `SameLine(0, 0); availWidth = GetCursorPosX() - ...` → **removed** (the whole trick is gone)
- DrawAlert: `availWidth` → `tableW` → **explicit, stable, from our constant**
- `availWidth += ...; _anchoredMaxWidth = availWidth + ...` → **replaced** by `GetItemRectSize().X` which reads the GroupPanel's actual rendered width after it closes

- [ ] **Step 6: Update the `panelWidth` and `availWidth` declarations that follow**

In `Draw()`, immediately after the `_anchoredMaxWidth` line, find:

**OLD CODE:**
```csharp
        if (CraftStatus != CraftableStatus.OK)
            return;

        ImGui.Spacing();

        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;
```

**NEW CODE:**
```csharp
        if (CraftStatus != CraftableStatus.OK)
            return;

        ImGui.Spacing();

        var availWidth = gpWidth;
        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;
```

Why: `availWidth` is now declared here (after the early-return guard) instead of at the top of `Draw()`. `gpWidth` is the GroupPanel's actual rendered width, which equals the window's inner content width after `_anchoredMaxWidth` constrains it. All button and panel widths that use `availWidth` will be stable.

- [ ] **Step 7: Build to confirm zero warnings**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
.\scripts\build.ps1 2>&1 | Select-String -Pattern "warning|error|Build succeeded|FAILED"
```

Expected:
```
Building Artificer 2.22.0.0 (Debug)...
Building Artificer 2.22.0.0 (Release)...
Build succeeded.
```

If you get CS0165 ("use of unassigned local variable `availWidth`"), it means there's a code path that reaches `availWidth` before the new declaration. Search for all uses of `availWidth` in `Draw()` and ensure they all come after the new `var availWidth = gpWidth;` line. Any that appear before the `if (CraftStatus != CraftableStatus.OK) return;` guard need to be replaced with `tableW` or `gpWidth` directly.

- [ ] **Step 8: Build + Deploy**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
.\scripts\build.ps1 -Deploy 2>&1 | Select-String -Pattern "warning|error|Build succeeded|FAILED|Deploy"
```

Expected:
```
Building Artificer 2.22.0.0 (Debug)...
Building Artificer 2.22.0.0 (Release)...
Build succeeded.
Deploying to C:\Users\aleja\AppData\Roaming\XIVLauncher\installedPlugins\Artificer\2.22.0.0 ...
Deploy complete.
```

- [ ] **Step 9: In-game verification**

Reload the Artificer plugin in FFXIV. Open the CraftingHelper window while crafting. Verify:

1. **Window width is stable** — watch the window for 10+ seconds; it must not grow horizontally
2. **Gear alert spans full width** — the GEAR CONDITION alert should span the full GroupPanel width (not just the left half)
3. **Alert text renders correctly** — title in accent color, message in muted color
4. **Column width** — Craftsmanship/Control/CP on left, Progress/Quality/Durability on right; each column visually ~160px wide (slightly wider than before)
5. **Buttons fill the window width** — Regenerate / Open in Macro Editor / View Saved Macros should span the full content width

If the window still grows: the most likely cause is that `GetItemRectSize().X` is reading a stale value (0 or wrong) on some frames. Add a guard:
```csharp
var gpWidth = ImGui.GetItemRectSize().X;
if (gpWidth > 0)
    _anchoredMaxWidth = gpWidth + ImGui.GetStyle().WindowPadding.X * 2;
```

- [ ] **Step 10: Commit Task 2**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
git add Artificer/Windows/CraftingHelper.cs
git commit -m "refactor(ui): CraftingHelper width — colW constant, remove SameLine hack, GetItemRectSize anchor"
```

---

## Task 3: UIStudio story update and final validation

**Files:**
- Read: `Artificer.UIStudio/Stories/Pages/CraftingHelperStory.cs` (no code change expected — verify it still builds)
- Read: `Artificer.UIStudio/Stories/AlertStory.cs` (no code change expected)

**Context:**
The UIStudio stories for CraftingHelper and Alert should continue to work unchanged. This task verifies that nothing broke in the stories and runs the full test suite.

- [ ] **Step 11: Run tests**

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test 2>&1 | Select-String -Pattern "passed|failed|error|Test run"
```

Expected:
```
Test run for ...
Passed! - Failed: 0, Passed: 211, Skipped: 0, Total: 211
```

If tests fail: read the full output without `Select-String` to see which test failed and why.

- [ ] **Step 12: Run UIStudio — CraftingHelper story**

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" run --project Artificer.UIStudio
```

Navigate to **Pages → CraftingHelper**. Check:
- The window renders without infinite growth
- The GEAR CONDITION alert is visible and spans the full panel width
- Column widths look proportional

- [ ] **Step 13: Final commit**

If the UIStudio and tests pass with no changes needed:

```powershell
cd "c:\Users\aleja\DEV\Craftimizer"
git status
```

If there are no uncommitted changes, the implementation is complete. If UIStudio stories needed minor adjustments (e.g., hardcoded widths in stories that now mismatch), commit those too:

```powershell
git add -A
git commit -m "chore(studio): update CraftingHelper story width after colW refactor"
```

---

## Troubleshooting Reference

### `GetItemRectSize()` returns zero or wrong value

`GetItemRectSize()` reads the last rendered item's bounding box. If the GroupPanel's `EndGroup()` isn't the last thing ImGui processed before this call, the value will be wrong. Ensure the `gpWidth = ImGui.GetItemRectSize().X;` line is the very first line after the closing `}` of the `using (var crPanel = ...)` block. No `ImGui.*` calls between the GroupPanel close and `GetItemRectSize()`.

### `availWidth` CS0165 compile error

If the compiler complains about unassigned `availWidth`: search for all uses of `availWidth` in `Draw()`. Any that appear before `var availWidth = gpWidth;` must be replaced. Candidates:
- Inside `if (crPanel)` block — replace with `tableW`
- In `DrawAlert` call — already replaced with `tableW`
- In `panelWidth` computation — that's in the right place (after the new declaration)

### `dl.AddText(Vector2, uint, string)` doesn't compile

Dalamud's cimgui version may not expose the short `AddText` overload. Use the font overload instead:
```csharp
var font     = ImGui.GetFont();
var fontSize = ImGui.GetFontSize();
dl.AddText(font, fontSize, new Vector2(textX, pos.Y + padY),
    ImGui.ColorConvertFloat4ToU32(barColor), title.ToUpperInvariant());
dl.AddText(font, fontSize, new Vector2(textX, pos.Y + padY + lineH),
    ImGui.ColorConvertFloat4ToU32(Colors.TextMuted), message);
```

### Window still grows despite `_anchoredMaxWidth` being set correctly

Check `PreDraw()` — it applies `SizeConstraints` from `_anchoredMaxWidth` with both `MinimumSize` and `MaximumSize` set to it. This pins the window width. If growth is still happening, add logging in `Draw()` to print `gpWidth` and `_anchoredMaxWidth` each frame — they must be identical across frames.

### StatsChanged guard removed — did this break recalculation?

`StatsChanged` is still used elsewhere in `CraftingHelper.cs` (e.g., triggering macro recalculation). The only change is removing the `if (StatsChanged)` guard around `TableSetupColumn` calls. `TableSetupColumn` should always be called every frame before row data — this is the correct ImGui usage regardless of whether stats changed. No behavioral change to the stats-change logic.
