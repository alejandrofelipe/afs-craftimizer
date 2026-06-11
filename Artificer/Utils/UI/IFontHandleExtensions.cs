using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using System.Numerics;

namespace Artificer.Utils;

/// <summary>
/// Extension methods for IFontHandle.
/// Originally defined in ImGuiUtils.cs before it was moved to Artificer.UI.
/// Kept in the plugin because IFontHandle is a Dalamud type.
/// </summary>
internal static class IFontHandleExtensions
{
    public static float GetFontSize(this IFontHandle font)
    {
        using (font.Push())
            return ImGui.GetFontSize();
    }

    public static Vector2 CalcTextSize(this IFontHandle font, string text)
    {
        using (font.Push())
            return ImGui.CalcTextSize(text);
    }

    public static void Text(this IFontHandle font, string text)
    {
        using (font.Push())
            ImGui.TextUnformatted(text);
    }
}
