namespace Artificer.Utils;

/// <summary>Visual variant for <see cref="ImGuiUtils.DrawAlert"/>.</summary>
public enum AlertVariant { Info, Success, Warning, Danger }

public static partial class ImGuiUtils
{
    /// <summary>
    /// Compact alert: 3px colored left bar + tinted background + uppercase title + body message.
    /// All text is rendered via DrawList — the sole layout item is <c>Dummy(width, height)</c>,
    /// so this component cannot cause horizontal window growth regardless of text length or placement.
    /// Does not reference Dalamud — pass <c>ImGuiHelpers.GlobalScale</c> as <paramref name="scale"/> from plugin context.
    /// </summary>
    public static void DrawAlert(AlertVariant variant, string title, string message, float scale = 1f, float width = -1f)
    {
        var barColor = variant switch
        {
            AlertVariant.Success => Colors.Good,
            AlertVariant.Warning => Colors.ActionBuff,
            AlertVariant.Danger  => Colors.Bad,
            _                    => new Vector4(0.23f, 0.51f, 0.96f, 1f),
        };

        var dl     = ImGui.GetWindowDrawList();
        var pos    = ImGui.GetCursorScreenPos();
        var availW = width > 0 ? width : ImGui.GetContentRegionAvail().X;
        var padX   = ImGui.GetStyle().WindowPadding.X;
        var padY   = ImGui.GetStyle().FramePadding.Y;
        var lineH  = ImGui.GetTextLineHeightWithSpacing();
        var totalH = lineH + ImGui.GetTextLineHeight() + padY * 2f;
        var barW   = 3f * scale;
        var textX  = pos.X + barW + padX;

        dl.AddRectFilled(pos, pos + new Vector2(availW, totalH),
            ImGui.ColorConvertFloat4ToU32(barColor with { W = 0.08f }));
        dl.AddRectFilled(pos, pos + new Vector2(barW, totalH),
            ImGui.ColorConvertFloat4ToU32(barColor));
        dl.AddText(new Vector2(textX, pos.Y + padY),
            ImGui.ColorConvertFloat4ToU32(barColor), title.ToUpperInvariant());
        dl.AddText(new Vector2(textX, pos.Y + padY + lineH),
            ImGui.ColorConvertFloat4ToU32(Colors.TextMuted), message);

        ImGui.Dummy(new Vector2(availW, totalH));
    }
}
