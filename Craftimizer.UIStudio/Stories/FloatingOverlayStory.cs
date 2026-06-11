using Craftimizer.Utils;
using ImGuiNET;

namespace Craftimizer.UIStudio.Stories;

internal sealed class FloatingOverlayStory : IStory
{
    public string Category => "Templates";
    public string Name     => "Floating Overlay";

    public void Draw()
    {
        const float BarW = 300f;

        Section("Full — 4 estados (ex: CosmicTracker)");
        ImGuiUtils.DrawResearchTypeRow("Type I",   4200, 5000, 7500, ImGuiUtils.ResearchTypeState.Active,   BarW);
        ImGuiUtils.DrawResearchTypeRow("Type II",  7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Complete, BarW);
        ImGuiUtils.DrawResearchTypeRow("Type III",    0,    0,    0, ImGuiUtils.ResearchTypeState.Locked,   BarW);
        ImGuiUtils.DrawResearchTypeRow("Type IV",  7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Maxed,    BarW);

        Section("Minimizado — modo compacto");
        ImGuiUtils.DrawResearchTypeRow("Type I",   4200, 5000, 7500, ImGuiUtils.ResearchTypeState.Active,   BarW, ImGuiUtils.ResearchTypeRowMode.Minimized);
        ImGuiUtils.DrawResearchTypeRow("Type II",  7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Complete, BarW, ImGuiUtils.ResearchTypeRowMode.Minimized);
        ImGuiUtils.DrawResearchTypeRow("Type III",    0,    0,    0, ImGuiUtils.ResearchTypeState.Locked,   BarW, ImGuiUtils.ResearchTypeRowMode.Minimized);
        ImGuiUtils.DrawResearchTypeRow("Type IV",  7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Maxed,    BarW, ImGuiUtils.ResearchTypeRowMode.Minimized);
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
