// Craftimizer.UI/ImGuiUtils.Charts.cs
using System;
using System.Collections.Generic;
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
        var endAngle   = StartAngle + SweepAngle;

        Vector2 ArcPt(float a) => center + new Vector2(MathF.Cos(a) * radius, MathF.Sin(a) * radius);

        void Stroke(float a0, float a1, uint col)
        {
            drawList.PathArcTo(center, radius, a0, a1, 0);
            drawList.PathStroke(col, ImDrawFlags.None, strokeW);
        }

        // Filled semicircle whose flat edge aligns with the stroke's flat end at `angle`.
        // backward=true  → faces away from arc start (used at the start of an arc segment).
        // backward=false → faces away from arc end   (used at the end of an arc segment).
        // The semicircle is adjacent to the stroke body with zero overlap → no alpha compositing.
        void RoundCap(float angle, bool backward, uint col)
        {
            var a0 = backward ? angle + MathF.PI : angle;
            drawList.PathArcTo(ArcPt(angle), capR, a0, a0 + MathF.PI, 0);
            drawList.PathFillConvex(col);
        }

        if (frac <= 0.005f)
        {
            Stroke(StartAngle, endAngle, trackColor);
            RoundCap(StartAngle, backward: true,  trackColor);
            RoundCap(endAngle,   backward: false, trackColor);
            return;
        }

        var fillEnd = StartAngle + SweepAngle * MathF.Min(frac, 1f);

        Stroke(StartAngle, endAngle, trackColor);
        RoundCap(endAngle, backward: false, trackColor);

        Stroke(StartAngle, fillEnd, fillColor);
        RoundCap(StartAngle, backward: true,  fillColor);
        RoundCap(fillEnd,    backward: false, fillColor);
    }

    /// <summary>
    /// Data for a single arc column in <see cref="DrawBarRow"/>.
    /// </summary>
    /// <param name="Name">Label displayed below the arc.</param>
    /// <param name="Color">Arc and value text color.</param>
    /// <param name="Value">Current value.</param>
    /// <param name="Max">Maximum value.</param>
    /// <param name="Caption">If set, replaces the "value/max" text inside the arc.</param>
    /// <param name="TooltipContent">Invoked inside BeginTooltip/EndTooltip when hovering the arc. Null = no tooltip.</param>
    public readonly record struct BarData(
        string Name,
        Vector4 Color,
        float Value,
        float Max,
        string? Caption = null,
        Action? TooltipContent = null
    );

    /// <summary>
    /// Draws a horizontal row of circular arc columns: arc + centered value/max text + name label.
    /// </summary>
    public static void DrawBarRow(IReadOnlyList<BarData> bars, float? totalWidth = null)
    {
        if (bars.Count == 0) return;

        var style    = ImGui.GetStyle();
        var spacingX = style.ItemSpacing.X;
        var spacingY = style.ItemSpacing.Y;
        var arcSize  = ImGui.GetFrameHeight() * 3f;
        var totalW   = totalWidth ?? ImGui.GetContentRegionAvail().X;
        var n        = bars.Count;
        var colW     = (totalW - spacingX * (n - 1)) / n;
        var colH     = arcSize + spacingY + ImGui.GetTextLineHeight();

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(totalW, colH));
        var dl = ImGui.GetWindowDrawList();

        for (var i = 0; i < n; i++)
        {
            var bar  = bars[i];
            var colX = origin.X + i * (colW + spacingX);
            var frac = bar.Max > 0 ? Math.Clamp(bar.Value / bar.Max, 0f, 1f) : 0f;

            var arcPos = new Vector2(colX + (colW - arcSize) * 0.5f, origin.Y);
            DrawStatArc(dl, arcPos, arcSize, frac, bar.Color);

            if (bar.TooltipContent != null &&
                ImGui.IsMouseHoveringRect(arcPos, arcPos + new Vector2(arcSize)))
            {
                ImGui.BeginTooltip();
                bar.TooltipContent();
                ImGui.EndTooltip();
            }

            var mutedCol = ImGui.GetColorU32(ImGuiCol.TextDisabled);
            var statCol  = ImGui.GetColorU32(bar.Color);
            var center   = arcPos + new Vector2(arcSize * 0.5f, arcSize * 0.5f);

            var font       = ImGui.GetFont();
            var baseFontSz = ImGui.GetFontSize();
            var valFontSz  = arcSize * 0.22f;
            var maxFontSz  = arcSize * 0.16f;
            var valScale   = valFontSz / baseFontSz;
            var maxScale   = maxFontSz / baseFontSz;

            var valStr = bar.Caption ?? $"{bar.Value:0}";
            var valSz  = ImGui.CalcTextSize(valStr) * valScale;

            if (bar.Caption is null)
            {
                var maxStr = $"/{bar.Max:0}";
                var maxSz  = ImGui.CalcTextSize(maxStr) * maxScale;
                const float innerGap = 1f;
                var blockH  = valSz.Y + innerGap + maxSz.Y;
                var startY  = center.Y - blockH * 0.5f;
                dl.AddText(font, valFontSz, new Vector2(center.X - valSz.X * 0.5f, startY),                      statCol, valStr);
                dl.AddText(font, maxFontSz, new Vector2(center.X - maxSz.X * 0.5f, startY + valSz.Y + innerGap), mutedCol, maxStr);
            }
            else
            {
                dl.AddText(font, valFontSz, new Vector2(center.X - valSz.X * 0.5f, center.Y - valSz.Y * 0.5f), statCol, valStr);
            }

            var nameSz = ImGui.CalcTextSize(bar.Name);
            dl.AddText(new Vector2(colX + (colW - nameSz.X) * 0.5f, origin.Y + arcSize + spacingY), mutedCol, bar.Name);
        }
    }
}
