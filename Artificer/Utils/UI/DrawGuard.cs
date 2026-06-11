using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Artificer.Utils.UI;

/// <summary>
/// Wraps ImGui draw calls in a try/catch. Catches managed exceptions only —
/// native crashes (C0000005 / access violations in cimgui) are NOT catchable here.
/// On error: logs to /xllog (Service.Log) and renders an inline error card;
/// hover the card to see the full stack trace.
/// </summary>
internal static class DrawGuard
{
    public static void Try(
        Action draw,
        [CallerMemberName] string caller = "",
        [CallerFilePath]   string file   = "")
    {
        try
        {
            draw();
        }
        catch (Exception ex)
        {
            var ctx = $"{System.IO.Path.GetFileNameWithoutExtension(file)}.{caller}";
            Log.Error(ex, $"[DrawGuard] {ctx}");
            RenderError(ctx, ex);
        }
    }

    private static void RenderError(string ctx, Exception ex)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.5f, 0.05f, 0.05f, 0.85f));
        ImGui.PushStyleColor(ImGuiCol.Text,    new Vector4(1f,   0.85f, 0.85f, 1f));
        using (var child = ImRaii.Child("##draw_error", new Vector2(-1, 52), true))
        {
            if (child)
            {
                ImGui.TextUnformatted($"  Draw error — {ctx}");
                ImGui.TextUnformatted($"  {ex.GetType().Name}: {ex.Message}");
                if (ImGui.IsWindowHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(600);
                    ImGui.TextUnformatted(ex.ToString());
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }
            }
        }
        ImGui.PopStyleColor(2);
    }
}
