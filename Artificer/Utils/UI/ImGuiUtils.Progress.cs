using Artificer.Simulator;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Artificer.Utils;

internal static partial class PluginImGuiUtils
{
    // ── Condition indicator ───────────────────────────────────────────────────

    /// <summary>
    /// Renders an animated colored circle followed by the condition name.
    /// The circle color matches the in-game condition animation.
    /// </summary>
    public static void DrawConditionIndicator(Condition condition, float spacing)
    {
        var frameHeight = ImGui.GetFrameHeight();
        ImGui.GetWindowDrawList().AddCircleFilled(
            ImGui.GetCursorScreenPos() + new Vector2(frameHeight / 2),
            frameHeight / 2,
            ImGui.ColorConvertFloat4ToU32(new Vector4(.35f, .35f, .35f, 0) + condition.GetColor(DateTime.UtcNow.TimeOfDay)));
        ImGui.Dummy(new(frameHeight));
        ImGui.SameLine(0, spacing);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(condition.Name());
    }
}
