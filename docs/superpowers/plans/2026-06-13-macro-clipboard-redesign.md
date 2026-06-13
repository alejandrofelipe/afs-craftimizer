# MacroClipboard Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the MacroClipboard window: correct the red GroupPanel label (add Theme.Push/Pop), simplify the copy button using the standard `IconButtonSquare`, and move it to a footer row below a separator.

**Architecture:** Single file edit — `MacroClipboard.cs`. Add `PreDraw`/`PostDraw` overrides with `Theme.Push/Pop` (fixes the label color by initializing the plugin theme color stack). Rewrite `DrawMacro` to render the text area first, then a `Separator`, then an `IconButtonSquare` aligned right. Removes the 15-line custom `InvisibleButton` + manual hover/click block.

**Tech Stack:** C#, Dalamud SDK 15, ImGui.NET, `Theme` (from `Artificer.Utils` namespace — already imported), `ImGuiUtils.IconButtonSquare`, `ImGuiUtils.AlignRight`.

> **Dependency:** The copy button icon (FontAwesome Paste glyph) will only render correctly after the **Icon Buttons Fix** plan is also applied. Both plans are independent and can be executed in any order — when both are done, the button will show the icon.

---

### Task 1: Add Theme.Push/Pop and rewrite DrawMacro

**Files:**
- Modify: `Artificer/Windows/MacroClipboard.cs`

> **Note:** No automated tests exist for ImGui rendering. Verification is build success + visual check in-game.

- [ ] **Step 1: Add PreDraw and PostDraw overrides**

After the `Draw()` override (around line 35), add:

```csharp
public override void PreDraw() => Theme.Push();

public override void PostDraw()
{
    Theme.Pop();
    base.PostDraw();
}
```

`Theme` is in the `Artificer.Utils` namespace which is already imported via `using Artificer.Utils;` at line 2.

- [ ] **Step 2: Rewrite DrawMacro**

Replace the entire `DrawMacro` method (lines 42–88 in the original file) with:

```csharp
private void DrawMacro(int idx, string macro)
{
    using var id    = ImRaii.PushId(idx);
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

    // Footer: separator + copy button aligned right
    ImGui.Separator();
    ImGuiUtils.AlignRight(ImGui.GetFrameHeight(), availWidth);
    if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Paste))
    {
        ImGui.SetClipboardText(macro);
        if (_plugin.Configuration.MacroCopy.ShowCopiedMessage)
            Plugin.Plugin.DisplayNotification(new()
            {
                Content       = Macros.Count == 1 ? "Copied macro to clipboard." : $"Copied macro {idx + 1} to clipboard.",
                MinimizedText = Macros.Count == 1 ? "Copied macro" : $"Copied macro {idx + 1}",
                Title         = "Macro Copied",
                Type          = NotificationType.Success
            });
    }
    if (ImGui.IsItemHovered())
        ImGuiUtils.Tooltip("Copy to Clipboard");
}
```

- [ ] **Step 3: Verify the full file compiles cleanly**

After editing, the complete `MacroClipboard.cs` file should look like this:

```csharp
using Artificer.Plugin;
using Artificer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.ImGuiNotification;

namespace Artificer.Windows;

public sealed class MacroClipboard : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoCollapse;

    private readonly global::Artificer.Plugin.Plugin _plugin;
    private List<string> Macros { get; }

    public MacroClipboard(global::Artificer.Plugin.Plugin plugin, IEnumerable<string> macros) : base("Macro Clipboard", WindowFlags)
    {
        _plugin = plugin;
        Macros = [.. macros];

        IsOpen = true;
        AllowPinning = false;
        AllowClickthrough = false;
        BringToFront();

        _plugin.WindowSystem.AddWindow(this);
    }

    public override void PreDraw() => Theme.Push();

    public override void PostDraw()
    {
        Theme.Pop();
        base.PostDraw();
    }

    public override void Draw()
    {
        var idx = 0;
        foreach (var macro in Macros)
            DrawMacro(idx++, macro);
    }

    private void DrawMacro(int idx, string macro)
    {
        using var id    = ImRaii.PushId(idx);
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

        // Footer: separator + copy button aligned right
        ImGui.Separator();
        ImGuiUtils.AlignRight(ImGui.GetFrameHeight(), availWidth);
        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Paste))
        {
            ImGui.SetClipboardText(macro);
            if (_plugin.Configuration.MacroCopy.ShowCopiedMessage)
                Plugin.Plugin.DisplayNotification(new()
                {
                    Content       = Macros.Count == 1 ? "Copied macro to clipboard." : $"Copied macro {idx + 1} to clipboard.",
                    MinimizedText = Macros.Count == 1 ? "Copied macro" : $"Copied macro {idx + 1}",
                    Title         = "Macro Copied",
                    Type          = NotificationType.Success
                });
        }
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Copy to Clipboard");
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
```

- [ ] **Step 4: Build the plugin**

```powershell
.\scripts\build.ps1 -Deploy
```

Expected: build succeeds with no errors.

- [ ] **Step 5: Commit**

```powershell
git add Artificer/Windows/MacroClipboard.cs
git commit -m "fix(ui): corrigir label vermelha e simplificar botão copiar no MacroClipboard"
```

- [ ] **Step 6: Visual verification (in-game)**

After deploying, trigger the Macro Clipboard window (e.g., via a macro result with the copy option). Verify:
1. The GroupPanel label "Macro" renders in accent blue (`#4AB8FF`), not red.
2. The copy button appears in the footer row (below a separator line), aligned to the right.
3. Clicking the copy button copies the macro text to clipboard and shows the notification.
4. Hovering the copy button shows the "Copy to Clipboard" tooltip.
5. The text area is read-only and fills the available width.
