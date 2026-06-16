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
        var separatorPos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(
            separatorPos,
            separatorPos + new Vector2(availWidth, 0),
            ImGui.GetColorU32(ImGuiCol.Separator),
            1f);
        ImGui.Dummy(new Vector2(availWidth, 1f));
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
        ImGuiUtils.HoveredTooltip("Copy to Clipboard");
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
