using System;
using System.Numerics;

namespace Artificer.Utils;

public static partial class ImGuiUtils
{
    public static void DrawSectionHeader(string label, Action? rightContent = null)
    {
        ImGui.Separator();

        if (rightContent is null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.ActionBuff))
                ImGui.TextUnformatted(label);
            return;
        }

        using (ImRaii.Group())
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.ActionBuff))
                ImGui.TextUnformatted(label);
            ImGui.SameLine();
            rightContent();
        }
    }

    public static void DrawStatRow(
        string label,
        string value,
        Vector4? valueColor = null,
        float availWidth = 0)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine();

        if (valueColor.HasValue)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, valueColor.Value))
                TextRight(value, availWidth);
        }
        else
        {
            TextRight(value, availWidth);
        }
    }
}
