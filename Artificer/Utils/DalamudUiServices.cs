using Artificer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using ImGuiNET;

namespace Artificer.Plugin;

internal sealed class DalamudUiServices : IUiServices
{
    private readonly IDalamudPluginInterface _pluginInterface;

    public DalamudUiServices(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public float GlobalScale => ImGuiHelpers.GlobalScale;

    // UiBuilder.IconFont returns Dalamud.Bindings.ImGui.ImFontPtr.
    // Convert via the underlying native pointer since both wrappers share the same ImFont*.
    public unsafe ImFontPtr IconFont => new ImFontPtr((nint)UiBuilder.IconFont.Handle);

    public unsafe ImFontPtr DefaultFont => new ImFontPtr((nint)UiBuilder.DefaultFont.Handle);

    public void OpenLink(string url) =>
        Dalamud.Utility.Util.OpenLink(url);
}
