# MacroClipboard Redesign — Design

**Date:** 2026-06-13  
**Status:** Approved  
**Scope:** `Artificer/Windows/MacroClipboard.cs`

---

## Problem

The MacroClipboard window has three issues:
1. GroupPanel label "Macro" renders in red instead of the expected `Colors.ActionBuff` (accent blue `#4AB8FF`)
2. The copy button icon is invisible (same `DrawCenteredIcon` bug as the RecipeNote buttons — fixed separately in the icon-buttons-fix spec)
3. The copy button uses a custom `InvisibleButton` + manual hover/active detection instead of the standard `ImGuiUtils.IconButtonSquare`, and floats in the top-right corner of the content area overlapping the text

## Root Cause — Red Label

`BeginGroupPanel` applies `Colors.ActionBuff` to the label text via `ImGui.PushStyleColor(ImGuiCol.Text, Colors.ActionBuff)`. The screenshot shows the label in red instead of the expected `#4AB8FF` (accent blue). The root cause is to be traced during implementation: the implementer should inspect the ImGui color stack at draw time for `MacroClipboard` and identify what overrides `ImGuiCol.Text` before the GroupPanel renders the label. The fix should ensure the label renders in `Colors.ActionBuff`.

## Fix

### 1. Investigate and fix the label color

During implementation: trace the color stack (e.g. add a temporary log of `ImGui.GetColorU32(ImGuiCol.Text)` at the start of `DrawMacro`) to find the unexpected push. Fix at the source — do not paper over it by re-pushing the color inside `BeginGroupPanel`.

### 2. Simplify the copy button

Replace the 15-line `InvisibleButton` + manual hover block with a single `ImGuiUtils.IconButtonSquare` call (which is the standard pattern used everywhere else in the project).

### 3. Move copy button to footer row

Add a footer separator line inside the GroupPanel, then render the copy button aligned to the right:

```
┌─ Macro ──────────────────────────────────────┐
│  /ac "Muscle Memory" <wait.3>                │
│  /ac "Waste Not II" <wait.2>                 │
│  ...                                         │
│  ─────────────────────────────────────────── │
│                                      [⧉]     │
└──────────────────────────────────────────────┘
```

Implementation:
```csharp
private void DrawMacro(int idx, string macro)
{
    using var id = ImRaii.PushId(idx);
    using var panel = ImRaii2.GroupPanel(Macros.Count == 1 ? "Macro" : $"Macro {idx + 1}", -1, out var availWidth);

    // Text area
    {
        using var font    = ImRaii.PushFont(UiBuilder.MonoFont);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
        using var bg      = ImRaii.PushColor(ImGuiCol.FrameBg, Vector4.Zero);
        var lineCount = macro.Count(c => c == '\n') + 1;
        ImGui.InputTextMultiline("", ref macro, macro.Length + 1,
            new(availWidth, ImGui.GetTextLineHeight() * Math.Max(15, lineCount) + ImGui.GetStyle().FramePadding.Y),
            ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
    }

    // Footer separator + copy button
    ImGui.Separator();
    ImGuiUtils.AlignRight(ImGui.GetFrameHeight(), availWidth);
    if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Paste))
    {
        ImGui.SetClipboardText(macro);
        if (_plugin.Configuration.MacroCopy.ShowCopiedMessage)
            Plugin.Plugin.DisplayNotification(new()
            {
                Content      = Macros.Count == 1 ? "Copied macro to clipboard." : $"Copied macro {idx + 1} to clipboard.",
                MinimizedText = Macros.Count == 1 ? "Copied macro" : $"Copied macro {idx + 1}",
                Title        = "Macro Copied",
                Type         = NotificationType.Success
            });
    }
    if (ImGui.IsItemHovered())
        ImGuiUtils.Tooltip("Copy to Clipboard");
}
```

## Files Changed

| File | Change |
|------|--------|
| `Artificer/Windows/MacroClipboard.cs` | Apply Theme, simplify copy button, add footer row |

## Dependencies

The copy button icon fix (`DrawCenteredIcon`) is handled in the separate **icon-buttons-fix** spec. Both specs can be implemented in any order; this spec's copy button will show the icon correctly once the icon fix is applied.

## Out of Scope

- Adding multiple macro tabs or any new content
- Changing the text area rendering (font, size, read-only behavior)
- Adding keyboard shortcuts for copy
