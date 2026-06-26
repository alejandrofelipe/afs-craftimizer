using System.Numerics;

namespace Artificer.Utils;

public static partial class ImGuiUtils
{
    /// <summary>
    /// Trunca <paramref name="text"/> para caber em <paramref name="maxWidth"/> px (largura ImGui atual),
    /// anexando reticências "…". Retorna o texto original se já couber. Busca binária por eficiência.
    /// </summary>
    public static string TruncateToWidth(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || ImGui.CalcTextSize(text).X <= maxWidth)
            return text;

        const string ellipsis = "…";
        var ellipsisW = ImGui.CalcTextSize(ellipsis).X;
        if (maxWidth <= ellipsisW)
            return ellipsis;

        var lo = 0;
        var hi = text.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (ImGui.CalcTextSize(text[..mid]).X + ellipsisW <= maxWidth)
                lo = mid;
            else
                hi = mid - 1;
        }
        return text[..lo] + ellipsis;
    }
}
