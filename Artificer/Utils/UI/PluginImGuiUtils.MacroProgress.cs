using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace Artificer.Utils;

internal static partial class PluginImGuiUtils
{
    /// <summary>
    /// Two-line progress area: chip + stage dots + algo name (line 1), progress bar (line 2).
    /// Mirrors the MacroEditor progress component.
    /// </summary>
    public static void DrawSolverProgressArea(
        float availWidth,
        ProgressBarComponent.ProgressSnapshot[] snapshots,
        ProgressBarType progressType)
    {
        var snapshot = snapshots[0];

        var chipState = snapshot.State switch
        {
            ProgressBarComponent.ProgressState.Completed => ImGuiUtils.SolverState.Complete,
            ProgressBarComponent.ProgressState.Cancelled or ProgressBarComponent.ProgressState.Failed
                => ImGuiUtils.SolverState.Failed,
            _ => ImGuiUtils.SolverState.Solving,
        };
        ImGuiUtils.DrawStateChip(chipState);

        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X);
        DrawSolverStageDots(snapshot);

        var algoName = snapshot.Name;
        var algoWidth = ImGui.CalcTextSize(algoName).X;
        ImGui.SameLine(0, 0);
        ImGuiUtils.AlignRight(algoWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(algoName);

        var config = new ProgressBarComponent.VisualConfig(
            Mode: ProgressBarComponent.DisplayMode.Horizontal,
            ColorTheme: progressType,
            Width: availWidth,
            ShowPercentage: true,
            ShowDetailedTooltip: true,
            ShowSummaryWhenAggregated: true
        );
        if (snapshots.Length == 1)
            ProgressBarComponent.DrawSingle(snapshots[0], config);
        else
            ProgressBarComponent.DrawAggregated(snapshots, config);
    }

    public static void DrawSolverStageDots(ProgressBarComponent.ProgressSnapshot snapshot)
    {
        var dl         = ImGui.GetWindowDrawList();
        var dotRadius  = 3f * ImGuiHelpers.GlobalScale;
        var dotDiameter = dotRadius * 2f;
        var dotGap     = 3f * ImGuiHelpers.GlobalScale;
        var cursor     = ImGui.GetCursorScreenPos();
        var centerY    = cursor.Y + ImGui.GetFrameHeight() * 0.5f;

        if (snapshot.IsIndeterminate)
        {
            for (var i = 0; i < 3; i++)
            {
                var phase = (float)((ImGui.GetTime() * 5.0 - i * 0.5) % (Math.PI * 2));
                var alpha = (MathF.Sin(phase) + 1f) * 0.4f + 0.2f;
                var scale = 0.85f + (MathF.Sin(phase) + 1f) * 0.15f;
                var cx    = cursor.X + dotRadius + i * (dotDiameter + dotGap);
                dl.AddCircleFilled(
                    new Vector2(cx, centerY),
                    dotRadius * scale,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.29f, 0.565f, 0.851f, alpha)));
            }
            ImGui.Dummy(new Vector2(3 * dotDiameter + 2 * dotGap, ImGui.GetFrameHeight()));
            return;
        }

        if (snapshot.Stage is not { } stage) return;

        var dotCount = snapshot.IsComplete
            ? Math.Min(stage + 1, 8)
            : Math.Min(stage + 2, 8);

        for (var i = 0; i < dotCount; i++)
        {
            var cx    = cursor.X + dotRadius + i * (dotDiameter + dotGap);
            var color = Colors.GetSolverProgressColors(i, ProgressBarType.Colorful).Foreground;

            if (i < stage)
            {
                dl.AddCircleFilled(new Vector2(cx, centerY), dotRadius,
                    ImGui.ColorConvertFloat4ToU32(color));
            }
            else if (i == stage && !snapshot.IsComplete)
            {
                var pulse = (MathF.Sin((float)(ImGui.GetTime() % 1.2 / 1.2 * (Math.PI * 2.0))) + 1f) * 0.5f;
                var scale = 0.85f + pulse * 0.30f;
                var alpha = 0.5f + pulse * 0.5f;
                dl.AddCircleFilled(new Vector2(cx, centerY), dotRadius * scale,
                    ImGui.ColorConvertFloat4ToU32(color with { W = alpha }));
            }
            else
            {
                dl.AddCircle(new Vector2(cx, centerY), dotRadius,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.27f, 0.27f, 0.36f, 1f)), 0, 1f);
            }
        }

        ImGui.Dummy(new Vector2(dotCount * dotDiameter + (dotCount - 1) * dotGap, ImGui.GetFrameHeight()));
    }
}
