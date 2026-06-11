using Artificer.Utils;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class BarsStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "Bar Row";

    private float _liveValue = 0.65f;

    public void Draw()
    {
        Section("Linha básica — 4 barras");
        ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
        {
            new("Progress",   Colors.Progress,   3200f, 4000f),
            new("Quality",    Colors.Quality,    2800f, 3500f),
            new("Durability", Colors.Durability,   55f,   80f),
            new("CP",         Colors.CP,           240f,  400f),
        });

        Section("Com Caption customizado");
        ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
        {
            new("HQ %",    Colors.HQ,             82f, 100f, Caption: "82%"),
            new("Collect.", Colors.Collectability, 410f, 500f, Caption: "410"),
        });

        Section("Com TooltipContent");
        ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
        {
            new("Progress", Colors.Progress, 3200f, 4000f, TooltipContent: () =>
            {
                ImGui.TextUnformatted("Min: 2900");
                ImGui.TextUnformatted("Med: 3150");
                ImGui.TextUnformatted("Avg: 3180");
                ImGui.TextUnformatted("Max: 3600");
            }),
            new("Quality", Colors.Quality, 2800f, 3500f, TooltipContent: () =>
            {
                ImGui.TextUnformatted("Min: 2200");
                ImGui.TextUnformatted("Med: 2750");
                ImGui.TextUnformatted("Avg: 2780");
                ImGui.TextUnformatted("Max: 3100");
            }),
        });
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled("(passe o mouse sobre um arco)");

        Section("Interativo");
        ImGui.SetNextItemWidth(300);
        ImGui.SliderFloat("##val", ref _liveValue, 0f, 1f, "%.2f");
        ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
        {
            new("Progress",   Colors.Progress,   _liveValue * 4000f, 4000f),
            new("Quality",    Colors.Quality,    _liveValue * 3500f, 3500f),
            new("Durability", Colors.Durability, _liveValue *   80f,   80f),
            new("CP",         Colors.CP,         _liveValue *  400f,  400f),
        });
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
