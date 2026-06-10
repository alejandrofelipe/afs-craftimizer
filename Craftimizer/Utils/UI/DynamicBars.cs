using Craftimizer.Plugin;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System;
using System.Numerics;
using System.Linq;
using Dalamud.Interface;

namespace Craftimizer.Utils;

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
    /// Legacy method for drawing progress bars. Use ProgressBarComponent instead for new code.
    /// This method is kept for backward compatibility during migration.
    /// </summary>
    [Obsolete("Use ProgressBarComponent.DrawProgressBarCompat() or ProgressBarComponent.DrawSingle() instead")]
    public static void DrawProgressBar(Solver.Solver solver, ProgressBarType progressType, float? availSpace = null)
    {
        // Delegate to ProgressBarComponent for consistency
        SolverProgressBar.DrawProgressBarCompat(solver, progressType, availSpace);
    }

    /// <summary>
    /// Legacy method for drawing progress bar tooltips. Use ProgressBarComponent tooltips instead.
    /// This method is kept for backward compatibility during migration.
    /// </summary>
    [Obsolete("Tooltip is now handled automatically by ProgressBarComponent")]
    public static void DrawProgressBarTooltip(Solver.Solver solver)
    {
        string tooltip;
        if (solver.IsIndeterminate)
            tooltip = "Initializing";
        else
        {
            tooltip = $"Solver Progress: {solver.ProgressValue:N0} / {solver.ProgressMax:N0}";
            if (solver.ProgressValue > solver.ProgressMax)
                tooltip += $"\n\nThis is taking longer than expected. Check to see if your gear stats are good and the solver settings are adequate.";
        }
        ImGuiUtils.TooltipWrapped(tooltip);
    }
}
