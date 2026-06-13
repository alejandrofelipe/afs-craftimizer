# Icon Buttons Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `DrawCenteredIcon` so icon glyphs render correctly inside `IconButtonSquare` buttons throughout the plugin.

**Architecture:** Single function edit — replace the broken `AddText(ImFontPtr, float, ...)` overload with `PushFont` + `AddText(float, ...)` (no explicit font parameter). This is the documented workaround for Dalamud SDK 15 where passing an `ImFontPtr` to `AddText` renders nothing.

**Tech Stack:** C#, Dalamud SDK 15, ImGui.NET (ImRaii.PushFont), FontAwesome icons.

---

### Task 1: Fix DrawCenteredIcon

**Files:**
- Modify: `Artificer.UI/ImGuiUtils.cs:167`

> **Note:** This is a rendering-only change. There are no automated tests for ImGui drawing calls. Verification is build success + visual check in-game after deploying the plugin.

- [ ] **Step 1: Apply the fix**

In `Artificer.UI/ImGuiUtils.cs`, replace line 167 (the single `AddText` call inside `DrawCenteredIcon`):

```csharp
// BEFORE (broken in Dalamud SDK 15 — explicit ImFontPtr causes glyph to not render):
ImGui.GetWindowDrawList().AddText(UiServices.Current.IconFont, UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale, offset + iconOffset, ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled), icon.ToIconString());
```

Replace with:

```csharp
// AFTER (push font onto stack first, then use overload that draws with current active font):
using (ImRaii.PushFont(UiServices.Current.IconFont))
    ImGui.GetWindowDrawList().AddText(
        UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale,
        offset + iconOffset,
        ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
        icon.ToIconString());
```

The resulting method body should look like:

```csharp
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
            UiServices.Current.IconFont.FontSize * UiServices.Current.GlobalScale * scale,
            offset + iconOffset,
            ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
            icon.ToIconString());
}
```

- [ ] **Step 2: Build the plugin**

```powershell
.\scripts\build.ps1 -Deploy
```

Expected: build succeeds with no errors. If it fails, check that `ImRaii` is already imported (it is — used in `GetIconSize` just above).

- [ ] **Step 3: Commit**

```powershell
git add Artificer.UI/ImGuiUtils.cs
git commit -m "fix(ui): corrigir DrawCenteredIcon usando PushFont em vez de AddText com ImFontPtr"
```

- [ ] **Step 4: Visual verification (in-game)**

After deploying, open the Crafting Helper window and start a craft. Open the macro result panel. The Edit and Copy buttons inside the macro card should now display their FontAwesome icons (pencil and clipboard) instead of rendering as empty gray rectangles.
