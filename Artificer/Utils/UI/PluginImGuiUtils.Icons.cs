using Artificer.Plugin;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using Service = Artificer.Plugin.Service;

namespace Artificer.Utils;

internal static partial class PluginImGuiUtils
{
    /// <summary>Desenha o ícone do item (size×size). Dummy do mesmo tamanho se não houver ícone.</summary>
    public static void DrawItemIcon(uint itemId, float size)
    {
        var iconId = LuminaSheets.ItemSheet.GetRowOrDefault(itemId)?.Icon ?? 0;
        if (iconId == 0)
        {
            ImGui.Dummy(new Vector2(size));
            return;
        }
        ImGui.Image(Service.IconManager.GetIconCached(iconId).Handle, new Vector2(size));
    }
}
