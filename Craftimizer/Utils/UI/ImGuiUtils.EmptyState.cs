using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Craftimizer.Utils;

internal static partial class ImGuiUtils
{
    public static void DrawEmptyState(
        FontAwesomeIcon icon,
        string title,
        string? subtitle = null,
        (string Label, Action Clicked)? primaryButton = null,
        (string Label, Action Clicked)? secondaryButton = null,
        float buttonWidth = 200f)
    {
        var availW   = ImGui.GetContentRegionAvail().X;
        var availH   = ImGui.GetContentRegionAvail().Y;
        var spacing  = ImGui.GetStyle().ItemSpacing.Y;
        var iconH    = ImGui.GetTextLineHeight() * 1.8f;
        var btnW     = buttonWidth * ImGuiHelpers.GlobalScale;
        var btnCount = (primaryButton != null ? 1 : 0) + (secondaryButton != null ? 1 : 0);

        var totalH = iconH + spacing + ImGui.GetTextLineHeightWithSpacing();
        if (subtitle != null)
            totalH += ImGui.GetTextLineHeight();
        if (btnCount > 0)
        {
            totalH += subtitle != null ? spacing * 2f : spacing;
            totalH += ImGui.GetFrameHeight();
            if (btnCount > 1)
                totalH += spacing + ImGui.GetFrameHeight();
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (availH - totalH) / 2f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            TextCentered(icon.ToIconString(), availW);

        TextCentered(title, availW);

        if (subtitle != null)
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                TextCentered(subtitle, availW);

        if (btnCount == 0) return;

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + spacing);

        if (secondaryButton is { } sec)
        {
            AlignCentered(btnW, availW);
            if (ImGui.Button(sec.Label, new(btnW, 0)))
                sec.Clicked();
        }

        if (primaryButton is { } pri)
        {
            AlignCentered(btnW, availW);
            if (btnCount > 1)
            {
                Theme.PushPrimaryButton();
                if (ImGui.Button(pri.Label, new(btnW, 0)))
                    pri.Clicked();
                Theme.PopPrimaryButton();
            }
            else
            {
                if (ImGui.Button(pri.Label, new(btnW, 0)))
                    pri.Clicked();
            }
        }
    }
}
