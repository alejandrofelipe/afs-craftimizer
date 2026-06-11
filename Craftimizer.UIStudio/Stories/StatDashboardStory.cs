using Craftimizer.Utils;
using ImGuiNET;
using System.Collections.Generic;

namespace Craftimizer.UIStudio.Stories;

internal sealed class StatDashboardStory : IStory
{
    public string Category => "Templates";
    public string Name     => "Stat Dashboard";

    private float _fraction = 0.65f;

    public void Draw()
    {
        Section("DrawBarRow — arcos (ex: SynthHelper, MacroEditor)");
        ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
        {
            new("Progress",   Colors.Progress,   _fraction * 4000f, 4000f),
            new("Quality",    Colors.Quality,    _fraction * 3500f, 3500f),
            new("Durability", Colors.Durability, _fraction *   80f,   80f),
            new("CP",         Colors.CP,         _fraction *  400f,  400f),
        });

        Section("ProgressBar — horizontal");
        var cfg = new ProgressBarComponent.VisualConfig(Width: 400);
        ProgressBarComponent.DrawSimple((int)(_fraction * 100),           100, config: cfg);
        ImGui.SameLine(0, 12); ImGui.TextDisabled("Progress");
        ProgressBarComponent.DrawSimple((int)(_fraction * 80),            100, config: cfg);
        ImGui.SameLine(0, 12); ImGui.TextDisabled("Quality");
        ProgressBarComponent.DrawSimple((int)(_fraction * 120),           100, config: cfg);
        ImGui.SameLine(0, 12); ImGui.TextDisabled("Durability");
        ProgressBarComponent.DrawSimple((int)(_fraction * 60),            100, config: cfg);
        ImGui.SameLine(0, 12); ImGui.TextDisabled("CP");

        Section("Interativo");
        ImGui.SetNextItemWidth(300);
        ImGui.SliderFloat("##frac", ref _fraction, 0f, 1f, "%.2f");
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled("atualiza arcos e barras acima");
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
