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

    public static void DrawRow(IEnumerable<BarData> bars, float? totalWidth = null)
    {
        var barList = bars as IReadOnlyList<BarData> ?? bars.ToList();
        if (barList.Count == 0) return;

        var spacing = ImGui.GetStyle().ItemSpacing;
        var lineH   = ImGui.GetTextLineHeight();
        var arcSize = ImGui.GetFrameHeight() * 3f;
        var totalW  = totalWidth ?? ImGui.GetContentRegionAvail().X;
        var n       = barList.Count;
        var colW    = (totalW - spacing.X * (n - 1)) / n;

        var colH = arcSize + spacing.Y + lineH;

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(totalW, colH));
        var dl = ImGui.GetWindowDrawList();

        for (var i = 0; i < n; i++)
        {
            var bar  = barList[i];
            var colX = origin.X + i * (colW + spacing.X);
            var frac = bar.Max > 0 ? Math.Clamp(bar.Value / bar.Max, 0f, 1f) : 0f;

            var arcPos = new Vector2(colX + (colW - arcSize) * 0.5f, origin.Y);
            PluginImGuiUtils.DrawStatArc(dl, arcPos, arcSize, frac, bar.Color);

            if (bar.Reliability is { } reliability &&
                ImGui.IsMouseHoveringRect(arcPos, arcPos + new Vector2(arcSize, arcSize)))
            {
                using var _font    = ImRaii.PushFont(UiBuilder.DefaultFont);
                using var _tooltip = ImRaii.Tooltip();
                using var _        = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

                ImGui.TextUnformatted("Min: ");
                ImGui.SameLine(0, 0);
                using (bar.PushCollectableColor(reliability.Min))
                    ImGui.TextUnformatted(reliability.Min.ToString());

                ImGui.TextUnformatted("Med: ");
                ImGui.SameLine(0, 0);
                using (bar.PushCollectableColor(reliability.Median))
                    ImGui.TextUnformatted(reliability.Median.ToString());

                ImGui.TextUnformatted("Avg: ");
                ImGui.SameLine(0, 0);
                using (bar.PushCollectableColor(reliability.Average))
                    ImGui.TextUnformatted(reliability.Average.ToString());

                ImGui.TextUnformatted("Max: ");
                ImGui.SameLine(0, 0);
                using (bar.PushCollectableColor(reliability.Max))
                    ImGui.TextUnformatted(reliability.Max.ToString());
            }

            var mutedCol = ImGui.GetColorU32(Colors.TextMuted);
            var statCol  = ImGui.GetColorU32(bar.Color);
            var center   = arcPos + new Vector2(arcSize * 0.5f, arcSize * 0.5f);

            var font        = ImGui.GetFont();
            var baseFontSz  = ImGui.GetFontSize();
            var valFontSz   = arcSize * 0.22f;
            var maxFontSz   = arcSize * 0.16f;
            var valScale    = valFontSz / baseFontSz;
            var maxScale    = maxFontSz / baseFontSz;

            var valStr = bar.Caption ?? $"{bar.Value:0}";
            var valSz  = ImGui.CalcTextSize(valStr) * valScale;

            if (bar.Caption is null)
            {
                var maxStr  = $"/{bar.Max:0}";
                var maxSz   = ImGui.CalcTextSize(maxStr) * maxScale;
                const float innerGap = 1f;
                var blockH  = valSz.Y + innerGap + maxSz.Y;
                var startY  = center.Y - blockH * 0.5f;
                dl.AddText(font, valFontSz, new Vector2(center.X - valSz.X * 0.5f, startY),                     statCol, valStr);
                dl.AddText(font, maxFontSz, new Vector2(center.X - maxSz.X * 0.5f, startY + valSz.Y + innerGap), mutedCol, maxStr);
            }
            else
            {
                dl.AddText(font, valFontSz, new Vector2(center.X - valSz.X * 0.5f, center.Y - valSz.Y * 0.5f), statCol, valStr);
            }

            var nameSz = ImGui.CalcTextSize(bar.Name);
            dl.AddText(new Vector2(colX + (colW - nameSz.X) * 0.5f, origin.Y + arcSize + spacing.Y), mutedCol, bar.Name);
        }
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
