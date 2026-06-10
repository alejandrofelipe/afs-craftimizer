using System;
using System.Numerics;

namespace Craftimizer.Utils;

public static partial class ImGuiUtils
{
    // ── Cosmic Exploration UI helpers ─────────────────────────────────────────
    // Usados exclusivamente por Windows/CosmicTracker.cs.
    // Dependem de Colors.Cosmic* definidos em Colors.cs.

    public enum ResearchTypeState { Locked, Active, Complete, Maxed }

    private static (Vector4 Label, Vector4 Num, Vector4 Marker) GetResearchTypeColors(ResearchTypeState state) => state switch
    {
        ResearchTypeState.Active   => (Colors.CosmicActive,   Colors.CosmicActive   with { W = 0.8f }, Colors.CosmicUpgrade),
        ResearchTypeState.Complete => (Colors.CosmicComplete, Colors.CosmicComplete with { W = 0.8f }, Colors.CosmicComplete),
        ResearchTypeState.Maxed    => (Colors.CosmicMaxed,    Colors.CosmicMaxed    with { W = 0.8f }, Colors.CosmicMaxed),
        _                          => (Colors.CosmicLocked,   Colors.CosmicLocked   with { W = 0.5f }, Colors.CosmicUpgrade),
    };

    /// <summary>
    /// Draws a full research-type row as seen in the CosmicTracker window:
    /// <code>
    ///   [Type III ◀]   1.284 / 7.500
    ///   [████░░░░░░|░░░░░░░░░░]      ← fill=current/max, marker=needed/max
    ///   upgrade: 5.000      máx: 7.500
    /// </code>
    /// </summary>
    public static void DrawResearchTypeRow(
        string label, int current, int needed, int max,
        ResearchTypeState state, float barWidth,
        int? delta = null)
    {
        var (labelColor, numColor, upgradeColor) = GetResearchTypeColors(state);

        using var id       = ImRaii.PushId(label);
        var drawList       = ImGui.GetWindowDrawList();
        var topLeft        = ImGui.GetCursorScreenPos();
        var highlightColor = ImGui.GetColorU32(Colors.CosmicChanged);

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        // ── Header: label + current/max ──────────────────────────────────────
        using (ImRaii.Group())
        {
            var suffix = state switch
            {
                ResearchTypeState.Active   => " ◄",
                ResearchTypeState.Complete => " ✓",
                ResearchTypeState.Maxed    => " ★",
                _                          => "",
            };

            using (ImRaii.PushColor(ImGuiCol.Text, labelColor))
                ImGui.TextUnformatted($"{label}{suffix}");

            string numText;
            if (state == ResearchTypeState.Locked)
            {
                numText = "— / —";
            }
            else if (delta is { } d && d > 0)
            {
                var baseText   = $"{current:N0} / {max:N0}";
                var deltaText  = $" (+{d:N0})";
                var totalWidth = ImGui.CalcTextSize(baseText).X + ImGui.CalcTextSize(deltaText).X;
                ImGui.SameLine(barWidth - totalWidth);
                using (ImRaii.PushColor(ImGuiCol.Text, numColor))
                    ImGui.TextUnformatted($"{current:N0}");
                ImGui.SameLine(0, 0);
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicMission))
                    ImGui.TextUnformatted(deltaText);
                ImGui.SameLine(0, 0);
                using (ImRaii.PushColor(ImGuiCol.Text, numColor))
                    ImGui.TextUnformatted($" / {max:N0}");
                numText = null!;
            }
            else
            {
                numText = $"{current:N0} / {max:N0}";
            }

            if (numText != null)
            {
                var numWidth = ImGui.CalcTextSize(numText).X;
                ImGui.SameLine(barWidth - numWidth);
                using (ImRaii.PushColor(ImGuiCol.Text, numColor))
                    ImGui.TextUnformatted(numText);
            }
        }

        // ── Bar ──────────────────────────────────────────────────────────────
        if (state != ResearchTypeState.Locked)
        {
            var fillFraction    = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
            // No upgrade marker when maxed — bar is already full
            var upgradeFraction = (state != ResearchTypeState.Maxed && max > 0)
                ? Math.Clamp((float)needed / max, 0f, 1f)
                : 0f;

            DrawResearchTypeBar(fillFraction, upgradeFraction, upgradeColor, state,
                new Vector2(barWidth, 6f * UiServices.Current.GlobalScale));
        }
        else
        {
            DrawResearchTypeBar(0f, 0f, Colors.CosmicLocked, state,
                new Vector2(barWidth, 6f * UiServices.Current.GlobalScale));
        }

        // ── Sub-limits ───────────────────────────────────────────────────────
        if (state == ResearchTypeState.Locked)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicLocked))
            {
                ImGui.TextUnformatted("—");
                ImGui.SameLine(barWidth - ImGui.CalcTextSize("—").X);
                ImGui.TextUnformatted("—");
            }
        }
        else if (state == ResearchTypeState.Maxed)
        {
            var maxLabel = $"máx: {max:N0} ★";
            var maxWidth = ImGui.CalcTextSize(maxLabel).X;
            ImGui.TextUnformatted("  "); // left anchor
            ImGui.SameLine(barWidth - maxWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicMaxed with { W = 0.8f }))
                ImGui.TextUnformatted(maxLabel);
        }
        else
        {
            var upgradeLabel = state == ResearchTypeState.Complete
                ? $"upgrade: {needed:N0} ✓"
                : $"upgrade: {needed:N0}";
            var maxLabel = $"máx: {max:N0}";

            using (ImRaii.PushColor(ImGuiCol.Text, upgradeColor with { W = 0.7f }))
                ImGui.TextUnformatted(upgradeLabel);

            var maxWidth = ImGui.CalcTextSize(maxLabel).X;
            ImGui.SameLine(barWidth - maxWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted with { W = 0.6f }))
                ImGui.TextUnformatted(maxLabel);
        }

        ImGui.Spacing();

        // ── Background highlight ──────────────────────────────────────────────
        if (delta != null)
        {
            var bottomRight = new Vector2(topLeft.X + barWidth, ImGui.GetCursorScreenPos().Y);
            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(topLeft, bottomRight, highlightColor, 4f * UiServices.Current.GlobalScale);
        }

        drawList.ChannelsMerge();
    }

    /// <summary>
    /// Compact row for minimized mode: fixed-width label + wide bar only.
    /// Numbers are shown in a tooltip on hover.
    /// </summary>
    public static void DrawResearchTypeRowMinimized(
        string label, int current, int needed, int max,
        ResearchTypeState state, float barWidth)
    {
        var labelWidth   = 60f * UiServices.Current.GlobalScale;
        var barAreaWidth = barWidth - labelWidth - ImGui.GetStyle().ItemSpacing.X;

        var (labelColor, _, markerColor) = GetResearchTypeColors(state);

        using var id = ImRaii.PushId(label);
        using (ImRaii.Group())
        {
            using (ImRaii.PushColor(ImGuiCol.Text, labelColor))
                ImGui.TextUnformatted(label);

            ImGui.SameLine(labelWidth);

            var fillFraction    = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
            var upgradeFraction = (state != ResearchTypeState.Maxed && max > 0)
                ? Math.Clamp((float)needed / max, 0f, 1f)
                : 0f;

            DrawResearchTypeBar(fillFraction, upgradeFraction, markerColor, state,
                new Vector2(barAreaWidth, 8f * UiServices.Current.GlobalScale));
        }

        if (ImGui.IsItemHovered())
        {
            var tip = state == ResearchTypeState.Maxed
                ? $"{current:N0} / {max:N0} ★"
                : $"{current:N0} / {max:N0}  —  upgrade: {needed:N0}";
            Tooltip(tip);
        }
    }

    /// <summary>
    /// Draws only the progress bar with fill (current/max) and an upgrade marker
    /// line at <paramref name="upgradeFraction"/> of the bar width.
    /// </summary>
    public static void DrawResearchTypeBar(
        float fillFraction,
        float upgradeFraction,
        Vector4 markerColor,
        ResearchTypeState state,
        Vector2 size)
    {
        var pos      = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var scale    = UiServices.Current.GlobalScale;

        var rounding = size.Y * 0.5f;

        var trackColor = state == ResearchTypeState.Locked
            ? ImGui.GetColorU32(Colors.CosmicLocked with { W = 0.3f })
            : ImGui.GetColorU32(ImGuiCol.FrameBg);

        drawList.AddRectFilled(pos, pos + size, trackColor, rounding);

        if (fillFraction > 0f && state != ResearchTypeState.Locked)
        {
            var fillColor = state switch
            {
                ResearchTypeState.Complete => ImGui.GetColorU32(Colors.CosmicComplete),
                ResearchTypeState.Maxed    => ImGui.GetColorU32(Colors.CosmicMaxed),
                _                          => ImGui.GetColorU32(Colors.CosmicActive),
            };
            var fillEnd = new Vector2(pos.X + size.X * fillFraction, pos.Y + size.Y);
            drawList.AddRectFilled(pos, fillEnd, fillColor, rounding);
        }

        if (upgradeFraction > 0f && upgradeFraction < 1f && state != ResearchTypeState.Locked)
        {
            var markerX  = pos.X + size.X * upgradeFraction;
            var markerP1 = new Vector2(markerX, pos.Y - 2f * scale);
            var markerP2 = new Vector2(markerX, pos.Y + size.Y + 2f * scale);
            drawList.AddLine(markerP1, markerP2, ImGui.GetColorU32(markerColor), 2f * scale);
        }

        ImGui.Dummy(size);
    }

    /// <summary>
    /// Draws a stage badge pill.
    /// </summary>
    public static void DrawCosmicStageBadge(int stage, bool complete, int maxStage = 0)
    {
        if (complete)
            DrawBadgePill(maxStage > 0 ? $"Stage {stage}/{maxStage} ✓" : $"Stage {stage} ✓", Colors.CosmicComplete);
        else if (maxStage > 0)
            DrawBadgePill($"Stage {stage}/{maxStage}", Colors.CosmicActive);
        else
            DrawBadgePill($"Stage {stage} → {stage + 1}", Colors.CosmicActive);
    }
}
