using Artificer.Plugin;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System;
using System.Numerics;
using System.Linq;
using Dalamud.Interface;

namespace Artificer.Utils;

internal static class DynamicBars
{
    public readonly record struct BarData(string Name, Vector4 Color, SimulatedMacro.Reliablity.Param? Reliability, float Value, float Max, IReadOnlyList<int?>? Collectability = null, string? Caption = null)
    {
        public BarData(string name, Vector4 color, float value, float max) : this(name, color, null, value, max)
        {
        }
    }


    private static ImRaii.ColorDisposable? PushCollectableColor(this in BarData bar, float collectability, bool colorUnmetThreshold = true)
    {
        if (bar.Collectability is not { } collectabilities)
            return null;

        var ret = collectabilities.Count;
        for (var i = 0; i < collectabilities.Count; ++i)
        {
            if (collectability < collectabilities[i])
            {
                ret = i;
                break;
            }
        }

        if (ret == 0)
        {
            if (colorUnmetThreshold)
                return ImRaii.PushColor(ImGuiCol.Text, Colors.Collectability);
            return null;
        }

        return ImRaii.PushColor(ImGuiCol.Text, Colors.CollectabilityThreshold[ret - 1]);
    }

    private static void DrawReliabilityTooltip(BarData bar, SimulatedMacro.Reliablity.Param reliability)
    {
        using var _ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        void Line(string label, float value)
        {
            ImGui.TextUnformatted(label);
            ImGui.SameLine(0, 0);
            using var color = bar.PushCollectableColor(value);
            ImGui.TextUnformatted(value.ToString());
        }

        Line("Min: ", reliability.Min);
        Line("Med: ", reliability.Median);
        Line("Avg: ", reliability.Average);
        Line("Max: ", reliability.Max);
    }

    public static void DrawRow(IEnumerable<BarData> bars, float? totalWidth = null)
    {
        var barList = bars as IReadOnlyList<BarData> ?? bars.ToList();
        if (barList.Count == 0) return;

        var mapped = barList.Select(b => new ImGuiUtils.BarData(
            Name:           b.Name,
            Color:          b.Color,
            Value:          b.Value,
            Max:            b.Max,
            Caption:        b.Caption,
            TooltipContent: b.Reliability is { } r
                ? () => DrawReliabilityTooltip(b, r)
                : null
        )).ToList();

        ImGuiUtils.DrawBarRow(mapped, totalWidth);
    }

    /// <summary>
    /// Desenha as barras como barras horizontais numa grade de <paramref name="columns"/> colunas
    /// (1 barra por célula, label+valor no overlay). Preserva o tooltip de reliability.
    /// Alternativa compacta ao <see cref="DrawRow"/> (que desenha arcos).
    /// </summary>
    public static void DrawColumns(IReadOnlyList<BarData> bars, int columns = 2, float? totalWidth = null)
    {
        if (bars.Count == 0) return;
        columns = Math.Max(1, columns);

        var totalW = totalWidth ?? ImGui.GetContentRegionAvail().X;
        var barH   = ImGui.GetFrameHeight();

        using var table = ImRaii.Table("dynbars2col", columns,
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoClip,
            new Vector2(totalW, 0));
        if (!table) return;

        for (var i = 0; i < bars.Count; i++)
        {
            if (i % columns == 0)
                ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(i % columns);

            var bar   = bars[i];
            var cellW = ImGui.GetContentRegionAvail().X;
            var frac  = bar.Max > 0 ? Math.Clamp(bar.Value / bar.Max, 0f, 1f) : 0f;
            var value = bar.Caption ?? bar.Value.ToString("0");
            var overlay = $"{bar.Name} {value}";

            var pos  = ImGui.GetCursorScreenPos();
            var size = new Vector2(cellW, barH);
            using (ImRaii.PushColor(ImGuiCol.PlotHistogram, bar.Color))
                ImGuiUtils.ProgressBar(frac, size, overlay);

            if (bar.Reliability is { } r && ImGui.IsMouseHoveringRect(pos, pos + size))
            {
                ImGui.BeginTooltip();
                DrawReliabilityTooltip(bar, r);
                ImGui.EndTooltip();
            }
        }
    }

}
