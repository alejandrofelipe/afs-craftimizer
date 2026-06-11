using System;
using System.Numerics;

namespace Artificer.Utils;

public static partial class ImGuiUtils
{
    // ── Solver state chip ─────────────────────────────────────────────────────

    public enum SolverState { Solving, Complete, Suboptimal, Failed }

    /// <summary>
    /// Renders a small colored dot followed by a label.
    /// The dot pulses when <paramref name="state"/> is <see cref="SolverState.Solving"/>.
    /// </summary>
    public static void DrawStateChip(SolverState state, string? label = null)
    {
        var (color, defaultLabel) = state switch
        {
            SolverState.Solving    => (Colors.ActionBuff,     "Solving…"),
            SolverState.Complete   => (Colors.ConditionSturdy, "Complete"),
            SolverState.Suboptimal => (Colors.ConditionGood,  "Suboptimal"),
            SolverState.Failed     => (new Vector4(1f, 0.36f, 0.43f, 1f), "Failed"),
            _ => (Vector4.One, "Unknown")
        };
        var text = label ?? defaultLabel;

        var dotRadius = ImGui.GetTextLineHeight() * 0.25f;
        var spacing   = ImGui.GetStyle().ItemSpacing.X * 0.5f;

        var dotAlpha = state == SolverState.Solving
            ? MathF.Abs(MathF.Sin((float)(ImGui.GetTime() * MathF.PI / 0.6f))) * 0.6f + 0.4f
            : 1f;

        using (ImRaii.Group())
        {
            var dotCenter = ImGui.GetCursorScreenPos() + new Vector2(dotRadius, ImGui.GetTextLineHeight() * 0.5f);
            ImGui.GetWindowDrawList().AddCircleFilled(
                dotCenter, dotRadius,
                ImGui.GetColorU32(new Vector4(color.X, color.Y, color.Z, color.W * dotAlpha)));
            ImGui.Dummy(new Vector2(dotRadius * 2, ImGui.GetTextLineHeight()));
            ImGui.SameLine(0, spacing);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
                ImGui.TextUnformatted(text);
        }
    }

    // ── Badge pills ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the size (in pixels) that a badge pill with the given text will occupy.
    /// </summary>
    public static Vector2 CalcBadgePillSize(string text)
    {
        var padding = new Vector2(7f, 2f) * UiServices.Current.GlobalScale;
        return ImGui.CalcTextSize(text) + padding * 2;
    }

    /// <summary>
    /// Draws a rounded pill badge with a translucent fill and border in <paramref name="foreColor"/>.
    /// Advances the cursor by the full pill size (safe to use inline or with SameLine).
    /// </summary>
    public static void DrawBadgePill(string text, Vector4 foreColor)
    {
        var padding    = new Vector2(7f, 2f) * UiServices.Current.GlobalScale;
        var textSize   = ImGui.CalcTextSize(text);
        var totalSize  = textSize + padding * 2;
        var rounding   = totalSize.Y / 2f;

        var pos      = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        // Reserve layout space first, then draw on top via the draw list.
        ImGui.Dummy(totalSize);

        drawList.AddRectFilled(pos, pos + totalSize,
            ImGui.GetColorU32(foreColor with { W = 0.14f }), rounding);
        drawList.AddRect(pos, pos + totalSize,
            ImGui.GetColorU32(foreColor with { W = 0.30f }), rounding);
        drawList.AddText(pos + padding, ImGui.GetColorU32(foreColor), text);
    }

    /// <summary>
    /// Draws a texture badge image (with optional tint) and shows <paramref name="tooltip"/> on hover.
    /// </summary>
    public static void DrawBadge(nint handle, Vector2 size, string tooltip, Vector4? tint = null)
    {
        if (tint.HasValue)
            ImGui.Image(handle, size, Vector2.Zero, Vector2.One, tint.Value);
        else
            ImGui.Image(handle, size);
        if (ImGui.IsItemHovered())
            Tooltip(tooltip);
    }

    // Overload for cross-assembly use: Dalamud ImTextureID.Handle is ulong (pointer-sized on x64).
    public static void DrawBadge(ulong handle, Vector2 size, string tooltip, Vector4? tint = null)
        => DrawBadge((nint)(long)handle, size, tooltip, tint);

    // ── Progress bar ──────────────────────────────────────────────────────────

    public static void ProgressBar(float value, Vector2 size, string? overlay = null)
    {
        var style = ImGui.GetStyle();
        var pos = ImGui.GetCursorScreenPos();

        if (size.X <= 0) size = size with { X = ImGui.GetContentRegionAvail().X + size.X };

        var bbMin = pos;
        var bbMax = pos + size;
        ImGuiExtras.ItemSize(size, style.FramePadding.Y);
        if (!ImGuiExtras.ItemAdd(new(bbMin.X, bbMin.Y, bbMax.X, bbMax.Y), 0))
            return;

        var bar_begin = 0.0f;
        var bar_end = Math.Clamp(value, 0, 1);

        var indeterminate = value < 0.0f;
        if (indeterminate)
        {
            const float bar_fraction = 0.2f;
            bar_begin = (-value % 1.0f * (1.0f + bar_fraction)) - bar_fraction;
            bar_end = bar_begin + bar_fraction;
        }

        ImGuiExtras.RenderFrame(bbMin, bbMax, ImGui.GetColorU32(ImGuiCol.FrameBg), true, style.FrameRounding);

        bbMin += new Vector2(style.FrameBorderSize);
        bbMax -= new Vector2(style.FrameBorderSize);

        var dl = ImGui.GetWindowDrawList();
        ImGuiExtras.RenderRectFilledRangeH(dl, new(bbMin.X, bbMin.Y, bbMax.X, bbMax.Y), ImGui.GetColorU32(ImGuiCol.PlotHistogram), bar_begin, bar_end, style.FrameRounding);

        if (overlay != null)
        {
            var textSz  = ImGui.CalcTextSize(overlay);
            var textPos = new Vector2(bbMin.X + (bbMax.X - bbMin.X - textSz.X) * 0.5f, bbMin.Y + (bbMax.Y - bbMin.Y - textSz.Y) * 0.5f);
            dl.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), overlay);
        }
    }
}
