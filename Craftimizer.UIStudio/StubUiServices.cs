using Craftimizer.Utils;
using ImGuiNET;

namespace Craftimizer.UIStudio;

internal sealed class StubUiServices : IUiServices
{
    public float GlobalScale => 1f;
    public ImFontPtr IconFont => ImGui.GetFont();
    public ImFontPtr DefaultFont => ImGui.GetFont();
    public void OpenLink(string url) { }
}
