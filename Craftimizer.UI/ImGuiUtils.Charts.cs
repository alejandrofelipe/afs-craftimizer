// Craftimizer.UI/ImGuiUtils.Charts.cs
using System;
using System.Numerics;

namespace Craftimizer.Utils;

public static partial class ImGuiUtils
{
    private static float Lerp(float a, float b, float t) =>
        MathF.FusedMultiplyAdd(b - a, t, a);

    // Draws a 280° arc (gap at bottom) into drawList.
    // screenPos = top-left of bounding square; size = side length; frac = 0..1 fill.
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
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(StartAngle)              * radius, MathF.Sin(StartAngle)              * radius), capR, trackColor);
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(StartAngle + SweepAngle) * radius, MathF.Sin(StartAngle + SweepAngle) * radius), capR, trackColor);

        if (frac > 0.005f)
        {
            var fillEnd = StartAngle + SweepAngle * MathF.Min(frac, 1f);
            drawList.PathArcTo(center, radius, StartAngle, fillEnd, 32);
            drawList.PathStroke(fillColor, ImDrawFlags.None, strokeW);
            drawList.AddCircleFilled(center + new Vector2(MathF.Cos(StartAngle) * radius, MathF.Sin(StartAngle) * radius), capR, fillColor);
            drawList.AddCircleFilled(center + new Vector2(MathF.Cos(fillEnd)    * radius, MathF.Sin(fillEnd)    * radius), capR, fillColor);
        }
    }
}
