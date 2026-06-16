using Artificer.Utils;
using ImGuiNET;

namespace Artificer.UIStudio.Stories;

internal sealed class AlertStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "Alert";

    public void Draw()
    {
        ImGui.TextDisabled("Todas as variantes");
        ImGui.Separator();
        ImGuiUtils.DrawAlert(AlertVariant.Info,    "Info",    "This is an informational message.");
        ImGuiUtils.DrawAlert(AlertVariant.Success, "Success", "Operation completed successfully.");
        ImGuiUtils.DrawAlert(AlertVariant.Warning, "Warning", "Gear condition is below 50%.");
        ImGuiUtils.DrawAlert(AlertVariant.Danger,  "Danger",  "Gear condition is critically low — repair now!");

        ImGui.Spacing();
        ImGui.TextDisabled("Contexto: Gear Condition");
        ImGui.Separator();
        ImGuiUtils.DrawAlert(AlertVariant.Warning, "Gear Condition", "38% · ~12–15 crafts left");
        ImGuiUtils.DrawAlert(AlertVariant.Danger,  "Gear Condition", "18% · ~3 crafts left");
    }
}
