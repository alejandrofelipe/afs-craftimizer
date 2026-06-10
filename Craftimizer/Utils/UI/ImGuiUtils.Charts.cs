using Craftimizer.Simulator;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Craftimizer.Utils;

internal static partial class PluginImGuiUtils
{
    public static void DrawStatArc(ImDrawListPtr drawList, Vector2 screenPos, float size, float frac, Vector4 color)
    {
        const float StartAngle = 2.269f;
        const float SweepAngle = 4.887f;

        var center  = screenPos + new Vector2(size * 0.5f, size * 0.5f);
        var strokeW = MathF.Max(2f, size * 0.16f);
        var radius  = size * 0.5f - strokeW * 0.5f - 1f;
        var capR    = strokeW * 0.5f;

        var trackColor = ImGui.GetColorU32(color with { W = 0.20f });
        var fillColor  = ImGui.GetColorU32(color);

        drawList.PathArcTo(center, radius, StartAngle, StartAngle + SweepAngle, 32);
        drawList.PathStroke(trackColor, ImDrawFlags.None, strokeW);
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(StartAngle) * radius, MathF.Sin(StartAngle) * radius), capR, trackColor);
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(StartAngle + SweepAngle) * radius, MathF.Sin(StartAngle + SweepAngle) * radius), capR, trackColor);

        if (frac > 0.005f)
        {
            var fillEnd = StartAngle + SweepAngle * MathF.Min(frac, 1f);
            drawList.PathArcTo(center, radius, StartAngle, fillEnd, 32);
            drawList.PathStroke(fillColor, ImDrawFlags.None, strokeW);
            drawList.AddCircleFilled(center + new Vector2(MathF.Cos(StartAngle) * radius, MathF.Sin(StartAngle) * radius), capR, fillColor);
            drawList.AddCircleFilled(center + new Vector2(MathF.Cos(fillEnd) * radius, MathF.Sin(fillEnd) * radius), capR, fillColor);
        }
    }

    public static void DrawMacroStatArcs(in SimulationState state, float windowHeight, bool asGrid = false)
    {
        var style    = ImGui.GetStyle();
        var spacingX = style.ItemSpacing.X;
        var spacingY = style.ItemSpacing.Y;
        var origin   = ImGui.GetCursorScreenPos();
        var dl       = ImGui.GetWindowDrawList();

        float arcSize;
        if (asGrid)
        {
            arcSize = (windowHeight - spacingY) / 2f;
            ImGui.Dummy(new Vector2(arcSize * 2 + spacingX, arcSize * 2 + spacingY));
        }
        else
        {
            arcSize = (windowHeight - spacingX) / 2f;
            ImGui.Dummy(new Vector2(arcSize * 4 + spacingX * 3, arcSize));
        }

        void Arc(int col, int row, float frac, Vector4 color, string tip)
        {
            var pos = new Vector2(origin.X + col * (arcSize + spacingX), origin.Y + row * (arcSize + spacingY));
            DrawStatArc(dl, pos, arcSize, Math.Clamp(frac, 0f, 1f), color);
            if (ImGui.IsMouseHoveringRect(pos, pos + new Vector2(arcSize)))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tip);
                ImGui.EndTooltip();
            }
        }

        if (asGrid)
        {
            Arc(0, 0, state.Input.Recipe.MaxProgress   > 0 ? (float)state.Progress   / state.Input.Recipe.MaxProgress   : 0f, Colors.Progress,   $"Progress: {state.Progress} / {state.Input.Recipe.MaxProgress}");
            Arc(1, 0, state.Input.Recipe.MaxQuality    > 0 ? (float)state.Quality    / state.Input.Recipe.MaxQuality    : 0f, Colors.Quality,    $"Quality: {state.Quality} / {state.Input.Recipe.MaxQuality}");
            Arc(0, 1, state.Input.Recipe.MaxDurability > 0 ? (float)state.Durability / state.Input.Recipe.MaxDurability : 0f, Colors.Durability, $"Durability: {state.Durability} / {state.Input.Recipe.MaxDurability}");
            Arc(1, 1, state.Input.Stats.CP             > 0 ? (float)state.CP         / state.Input.Stats.CP             : 0f, Colors.CP,         $"CP: {state.CP} / {state.Input.Stats.CP}");
        }
        else
        {
            Arc(0, 0, state.Input.Recipe.MaxProgress   > 0 ? (float)state.Progress   / state.Input.Recipe.MaxProgress   : 0f, Colors.Progress,   $"Progress: {state.Progress} / {state.Input.Recipe.MaxProgress}");
            Arc(1, 0, state.Input.Recipe.MaxQuality    > 0 ? (float)state.Quality    / state.Input.Recipe.MaxQuality    : 0f, Colors.Quality,    $"Quality: {state.Quality} / {state.Input.Recipe.MaxQuality}");
            Arc(2, 0, state.Input.Recipe.MaxDurability > 0 ? (float)state.Durability / state.Input.Recipe.MaxDurability : 0f, Colors.Durability, $"Durability: {state.Durability} / {state.Input.Recipe.MaxDurability}");
            Arc(3, 0, state.Input.Stats.CP             > 0 ? (float)state.CP         / state.Input.Stats.CP             : 0f, Colors.CP,         $"CP: {state.CP} / {state.Input.Stats.CP}");
        }
    }

    public sealed class ViolinData
    {
        public struct Point(float x, float y, float y2)
        {
            public float X = x, Y = y, Y2 = y2;
        }

        public ReadOnlySpan<Point> Data => (DataArray ?? []).AsSpan();
        private Point[]? DataArray { get; set; }
        public readonly float Min;
        public readonly float Max;

        public ViolinData(IEnumerable<int> samples, float min, float max, int resolution, double bandwidth)
        {
            Min = min;
            Max = max;
            bandwidth *= Max - Min;
            var samplesList = samples.AsParallel().Select(s => (double)s).ToArray();
            var plotTask = Task.Run(() =>
            {
                var s = Stopwatch.StartNew();
                var data = ParallelEnumerable.Range(0, resolution + 1)
                    .Select(n => MathF.FusedMultiplyAdd(max - min, n / (float)resolution, min))
                    .Select(n => (n, (float)KernelDensity.EstimateGaussian(n, bandwidth, samplesList)))
                    .Select(n => new Point(n.n, n.Item2, -n.Item2));
                // ParallelQuery doesn't support [.. data] correctly. The plots look very wrong.
#pragma warning disable IDE0305 // Simplify collection initialization
                DataArray = data.ToArray();
#pragma warning restore IDE0305 // Simplify collection initialization
                s.Stop();
                Log.Debug($"Violin plot processing took {s.Elapsed.TotalMilliseconds:0.00}ms");
            });
            _ = plotTask.ContinueWith(t => Log.Error(t.Exception!, "Violin plot computation failed"), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public static void ViolinPlot(in ViolinData data, Vector2 size)
    {
        using var padding = ImRaiiPlot.PushStyle(ImPlotStyleVar.Padding, Vector2.Zero);
        using var plotBg = ImRaiiPlot.PushColor(ImPlotCol.Bg, Vector4.Zero);
        using var fill = ImRaiiPlot.PushColor(ImPlotCol.Fill, new Vector4(1f, 1f, 1f, .5f));

        using var plot = ImRaiiPlot.Plot("##violin", size, ImPlotFlags.CanvasOnly | ImPlotFlags.NoInputs | ImPlotFlags.NoChild | ImPlotFlags.NoFrame);
        if (plot)
        {
            ImPlot.SetupAxes([], [], ImPlotAxisFlags.NoDecorations, ImPlotAxisFlags.NoDecorations | ImPlotAxisFlags.AutoFit);
            ImPlot.SetupAxisLimits(ImAxis.X1, data.Min, data.Max, ImPlotCond.Always);
            ImPlot.SetupFinish();

            if (data.Data is { } points && !points.IsEmpty)
            {
                unsafe
                {
                    var label_id = stackalloc byte[] { (byte)'\0' };
                    fixed (ViolinData.Point* p = points)
                    {
                        ImPlot.PlotShaded(label_id, &p->X, &p->Y, &p->Y2, points.Length, ImPlotShadedFlags.None, 0, sizeof(ViolinData.Point));
                    }
                }
            }
        }
    }
}
