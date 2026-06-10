using Craftimizer.Utils;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace Craftimizer.UIStudio.Stories;

internal sealed class ProgressBarStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "Progress Bar";

    private float _liveValue = 0.42f;

    public void Draw()
    {
        Section("Horizontal — estados");
        DrawRow("0 %",           new ProgressBarComponent.ProgressSnapshot("A", 0,   100, ProgressBarComponent.ProgressState.InProgress));
        DrawRow("42 %",          new ProgressBarComponent.ProgressSnapshot("B", 42,  100, ProgressBarComponent.ProgressState.InProgress));
        DrawRow("100 % (concluído)", new ProgressBarComponent.ProgressSnapshot("C", 100, 100, ProgressBarComponent.ProgressState.Completed));
        DrawRow("Falhou",        new ProgressBarComponent.ProgressSnapshot("D", 60,  100, ProgressBarComponent.ProgressState.Failed));
        DrawRow("Cancelado",     new ProgressBarComponent.ProgressSnapshot("E", 30,  100, ProgressBarComponent.ProgressState.Cancelled));
        DrawRow("Indeterminado", new ProgressBarComponent.ProgressSnapshot("F", 0,   100, ProgressBarComponent.ProgressState.Indeterminate));

        Section("Horizontal — temas de cor");
        ProgressBarComponent.DrawSimple(65, 100, config: new(ColorTheme: ProgressBarType.Colorful, Width: 300));
        ImGui.SameLine(0, 12);
        ImGui.TextDisabled("Colorful  ·  estágio 0");
        ProgressBarComponent.DrawSimple(65, 100, config: new(ColorTheme: ProgressBarType.Simple,   Width: 300));
        ImGui.SameLine(0, 12);
        ImGui.TextDisabled("Simple  ·  monocromático");

        Section("Arco circular");
        var arcCfg = new ProgressBarComponent.VisualConfig(Mode: ProgressBarComponent.DisplayMode.Arc, Height: 80);
        for (int pct = 0; pct <= 100; pct += 25)
        {
            if (pct > 0) ImGui.SameLine(0, 16);
            ProgressBarComponent.DrawSimple(pct, 100, config: arcCfg);
        }

        Section("Compacto");
        var compCfg = new ProgressBarComponent.VisualConfig(Mode: ProgressBarComponent.DisplayMode.Compact, Width: 60);
        for (int pct = 0; pct <= 100; pct += 25)
        {
            if (pct > 0) ImGui.SameLine(0, 4);
            ImGui.BeginChild($"##cmp{pct}", new Vector2(64, ImGui.GetFrameHeight()));
            ProgressBarComponent.DrawSimple(pct, 100, config: compCfg);
            ImGui.EndChild();
        }

        Section("Agregado — múltiplos processos");
        IReadOnlyList<ProgressBarComponent.ProgressSnapshot> snapshots =
        [
            new("Solver 1", 100, 100, ProgressBarComponent.ProgressState.Completed),
            new("Solver 2", 72,  100, ProgressBarComponent.ProgressState.InProgress, Stage: 3),
            new("Solver 3", 45,  100, ProgressBarComponent.ProgressState.InProgress, Stage: 1),
            new("Solver 4", 0,   100, ProgressBarComponent.ProgressState.Indeterminate),
        ];
        ProgressBarComponent.DrawAggregated(snapshots, new(Width: 400, ShowDetailedTooltip: true));
        ImGui.SameLine(0, 12);
        ImGui.TextDisabled("(passe o mouse para tooltip detalhado)");

        Section("Empilhado (stacked)");
        ProgressBarComponent.DrawAggregated(snapshots,
            new(Mode: ProgressBarComponent.DisplayMode.Stacked, Width: 400, ColorTheme: ProgressBarType.Colorful));

        Section("Interativo");
        ImGui.SetNextItemWidth(300);
        ImGui.SliderFloat("##live", ref _liveValue, 0f, 1f, "%.2f");
        ProgressBarComponent.DrawSimple((int)(_liveValue * 100), 100,
            config: new(Width: 300, ColorTheme: ProgressBarType.Colorful));
    }

    private static void DrawRow(string label, ProgressBarComponent.ProgressSnapshot snap)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"{label,-22}");
        ImGui.SameLine(0, 12);
        ProgressBarComponent.DrawSingle(snap, new(Width: 300));
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
