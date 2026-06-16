using System.Collections.Generic;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Artificer.Utils;

public static partial class ImGuiUtils
{
    private static readonly Stack<(Vector2 Min, Vector2 Max, float TopPadding)> GroupPanelLabelStack = new();

    // Adapted from https://github.com/ocornut/imgui/issues/1496#issuecomment-655048353
    // width = -1 -> size to parent
    // width = 0 -> size to content
    // returns available width (better since it accounts for the right side padding)
    // ^ only useful if width = -1
    public static float BeginGroupPanel(string name, float width, bool accentLabel = true, Action? titleSuffix = null)
    {
        ImGui.PushID(name);

        // container group
        ImGui.BeginGroup();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var frameHeight = ImGui.GetFrameHeight();
        width = width < 0 ? ImGui.GetContentRegionAvail().X - (2 * itemSpacing.X) : width;
        var fullWidth = width > 0 ? width + (2 * itemSpacing.X) : 0;
        {
            using var noPadding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
            using var noSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            // inner group
            ImGui.BeginGroup();
            ImGui.Dummy(new Vector2(fullWidth, 0));
            ImGui.Dummy(new Vector2(itemSpacing.X, 0)); // shifts next group by is.x
            ImGui.SameLine(0, 0);

            // label group
            ImGui.BeginGroup();
            if (ImGui.CalcTextSize(name, true).X == 0)
            {
                GroupPanelLabelStack.Push(default);
                ImGui.Dummy(new Vector2(0f, itemSpacing.Y)); // shifts content by is.y
            }
            else
            {
                ImGui.Dummy(new Vector2(frameHeight / 2, 0)); // shifts text by fh/2
                ImGui.SameLine(0, 0);
                var textFrameHeight = ImGui.GetFrameHeight();
                ImGui.AlignTextToFramePadding();
                {
                    if (accentLabel)
                        ImGui.PushStyleColor(ImGuiCol.Text, Colors.ActionBuff);
                    ImGui.TextUnformatted(name);
                    if (accentLabel)
                        ImGui.PopStyleColor();
                }
                GroupPanelLabelStack.Push((ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), textFrameHeight / 2f)); // push rect to stack
                titleSuffix?.Invoke();
                ImGui.SameLine(0, 0);
                ImGui.Dummy(new Vector2(0f, textFrameHeight + itemSpacing.Y)); // shifts content by fh + is.y
            }

            // content group
            ImGui.BeginGroup();
        }

        return width;
    }

    public static void EndGroupPanel()
    {
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        {
            using var noPadding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
            using var noSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            // content group
            ImGui.EndGroup();

            // label group
            ImGui.EndGroup();

            ImGui.SameLine(0, 0);
            // shifts full size by is (for rect placement)
            ImGui.Dummy(new(itemSpacing.X, 0));
            ImGui.Dummy(new(0, itemSpacing.Y * 2)); // * 2 for some reason (otherwise the bottom is too skinny)

            // inner group
            ImGui.EndGroup();

            var (labelMin, labelMax, labelPadding) = GroupPanelLabelStack.Pop();

            var innerMin = ImGui.GetItemRectMin();
            var innerMax = ImGui.GetItemRectMax();
            // If there was actual text
            if (labelMax.X != labelMin.X)
            {
                innerMin += new Vector2(0, labelPadding);

                // add itemspacing padding on the label's sides
                labelMin.X -= itemSpacing.X / 2;
                labelMax.X += itemSpacing.X / 2;
            }
            for (var i = 0; i < 4; ++i)
            {
                var (minClip, maxClip) = i switch
                {
                    0 => (new Vector2(float.NegativeInfinity), new Vector2(labelMin.X, float.PositiveInfinity)),
                    1 => (new Vector2(labelMax.X, float.NegativeInfinity), new Vector2(float.PositiveInfinity)),
                    2 => (new Vector2(labelMin.X, float.NegativeInfinity), new Vector2(labelMax.X, labelMin.Y)),
                    3 => (new Vector2(labelMin.X, labelMax.Y), new Vector2(labelMax.X, float.PositiveInfinity)),
                    _ => (Vector2.Zero, Vector2.Zero)
                };

                ImGui.PushClipRect(minClip, maxClip, true);
                ImGui.GetWindowDrawList().AddRect(
                    innerMin, innerMax,
                    ImGui.GetColorU32(ImGuiCol.Border),
                    itemSpacing.X);
                ImGui.PopClipRect();
            }

            ImGui.Dummy(Vector2.Zero);
        }

        ImGui.EndGroup();

        ImGui.PopID();
    }

    // ── Text / Layout helpers ─────────────────────────────────────────────────

    public static bool InputTextMultilineWithHint(string label, string hint, ref string input, int maxLength, Vector2 size, int flags = 0, ImGuiInputTextCallback? callback = null, IntPtr user_data = default)
    {
        const int Multiline = 1 << 26;
        return ImGuiExtras.InputTextEx(label, hint, ref input, maxLength, size, (ImGuiInputTextFlags)(flags | Multiline), callback, user_data);
    }

    private static Vector2 GetIconSize(FontAwesomeIcon icon)
    {
        using var font = ImRaii.PushFont(UiServices.Current.IconFont);
        return ImGui.CalcTextSize(icon.ToIconString());
    }

    internal static Vector2 CenteredOffset(Vector2 iconSize, Vector2 area) =>
        (area - iconSize) * 0.5f;

    public static void DrawCenteredIcon(FontAwesomeIcon icon, Vector2 offset, Vector2 size, bool isDisabled = false)
    {
        var iconSize = GetIconSize(icon);
        var iconOffset = CenteredOffset(iconSize, size);

        using (ImRaii.PushFont(UiServices.Current.IconFont))
            ImGui.GetWindowDrawList().AddText(
                offset + iconOffset,
                ImGui.GetColorU32(!isDisabled ? ImGuiCol.Text : ImGuiCol.TextDisabled),
                icon.ToIconString());
    }

    // Overload for cross-assembly use: Dalamud.Interface.FontAwesomeIcon is int-based.
    public static bool IconButtonSquare(int icon, float size = -1) => IconButtonSquare((FontAwesomeIcon)(ushort)icon, size);

    public static bool IconButtonSquare(FontAwesomeIcon icon, float size = -1)
    {
        var ret = false;

        var buttonSize = new Vector2(size == -1 ? ImGui.GetFrameHeight() : size);
        var pos = ImGui.GetCursorScreenPos();
        var spacing = new Vector2(ImGui.GetStyle().FramePadding.Y);

        if (ImGui.Button($"###{icon.ToIconString()}", buttonSize))
            ret = true;

        var isDisabled = ImGuiExtras.GetItemFlags().HasFlag(ImGuiItemFlags.Disabled);
        DrawCenteredIcon(icon, pos + spacing, buttonSize - spacing * 2, isDisabled);

        return ret;
    }

    public static bool IconButtonWithTooltip(
        FontAwesomeIcon icon,
        string tooltip,
        float size = -1,
        int flags = 0)
    {
        var clicked = IconButtonSquare(icon, size);
        HoveredTooltip(tooltip, flags);
        return clicked;
    }

    // Overload cross-assembly: Dalamud expõe FontAwesomeIcon como int em projetos externos
    public static bool IconButtonWithTooltip(
        int icon,
        string tooltip,
        float size = -1,
        int flags = 0)
        => IconButtonWithTooltip((FontAwesomeIcon)(ushort)icon, tooltip, size, flags);

    // https://gist.github.com/dougbinks/ef0962ef6ebe2cadae76c4e9f0586c69#file-imguiutils-h-L219
    private static void UnderlineLastItem(Vector4 color)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        min.Y = max.Y;
        ImGui.GetWindowDrawList().AddLine(min, max, ImGui.ColorConvertFloat4ToU32(color), 1);
    }

    // https://gist.github.com/dougbinks/ef0962ef6ebe2cadae76c4e9f0586c69#file-imguiutils-h-L228
    public static unsafe void Hyperlink(string text, string url, bool underline = true)
    {
        ImGui.TextUnformatted(text);
        if (underline)
            UnderlineLastItem(*ImGui.GetStyleColorVec4(ImGuiCol.Text));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                UiServices.Current.OpenLink(url);
            var urlWithoutScheme = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                urlWithoutScheme = uri.Host + (string.Equals(uri.PathAndQuery, "/", StringComparison.Ordinal) ? string.Empty : uri.PathAndQuery);
            Tooltip(urlWithoutScheme);
        }
    }

    public static void Tooltip(string text, float? wrapWidth = null)
    {
        using var _font = ImRaii.PushFont(UiServices.Current.DefaultFont);
        using var _tooltip = ImRaii.Tooltip();
        if (wrapWidth.HasValue)
        {
            using var _wrap = ImRaii2.TextWrapPos(wrapWidth.Value * UiServices.Current.GlobalScale);
            ImGui.TextUnformatted(text);
        }
        else
        {
            ImGui.TextUnformatted(text);
        }
    }

    public static void TooltipWrapped(string text, float width = 300) => Tooltip(text, width);

    public static void HoveredTooltip(
        string text,
        int flags = 0,
        float? wrapWidth = null)
    {
        if (ImGui.IsItemHovered((ImGuiHoveredFlags)flags))
            Tooltip(text, wrapWidth);
    }

    public static void TextWrappedTo(string text, float wrapPosX = default, float basePosX = default)
    {
        var font = ImGui.GetFont();

        var currentPos = ImGui.GetCursorPosX();

        if (basePosX == default)
            basePosX = ImGui.GetCursorStartPos().X;

        float currentWrapWidth;
        if (wrapPosX == default)
            currentWrapWidth = ImGui.GetContentRegionAvail().X;
        else
            currentWrapWidth = wrapPosX - currentPos;

        var textBuf = text.AsSpan();
        var lineSize = ImGuiExtras.CalcWordWrapPositionA(font, UiServices.Current.GlobalScale, textBuf, currentWrapWidth) ?? 0;
        if (lineSize == 0)
            lineSize = textBuf.Length;
        var lineBuf = textBuf[..lineSize];
        ImGui.TextUnformatted(lineBuf.ToString());
        var remainingBuf = textBuf[lineSize..].TrimStart();

        if (!remainingBuf.IsEmpty)
        {
            ImGui.SetCursorPosX(basePosX);
            using (ImRaii2.TextWrapPos(wrapPosX))
                ImGui.TextWrapped(remainingBuf.ToString());
        }
    }

    public static void AlignCentered(float width, float availWidth = default)
    {
        if (availWidth == default)
            availWidth = ImGui.GetContentRegionAvail().X;
        if (availWidth > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + (availWidth - width) / 2);
    }

    public static void AlignRight(float width, float availWidth = default)
    {
        if (availWidth == default)
            availWidth = ImGui.GetContentRegionAvail().X;
        if (availWidth > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + availWidth - width);
    }

    public static void AlignMiddle(Vector2 size, Vector2 availSize = default)
    {
        if (availSize == default)
            availSize = ImGui.GetContentRegionAvail();
        if (availSize.X > size.X)
            ImGui.SetCursorPosX(ImGui.GetCursorPos().X + (availSize.X - size.X) / 2);
        if (availSize.Y > size.Y)
            ImGui.SetCursorPosY(ImGui.GetCursorPos().Y + (availSize.Y - size.Y) / 2);
    }

    // https://stackoverflow.com/a/67855985
    public static void TextCentered(string text, float availWidth = default)
    {
        AlignCentered(ImGui.CalcTextSize(text).X, availWidth);
        ImGui.TextUnformatted(text);
    }

    public static void TextRight(string text, float availWidth = default)
    {
        AlignRight(ImGui.CalcTextSize(text).X, availWidth);
        ImGui.TextUnformatted(text);
    }

    public static void TextMiddleNewLine(string text, Vector2 availSize)
    {
        if (availSize == default)
            availSize = ImGui.GetContentRegionAvail();
        var c = ImGui.GetCursorPos();
        AlignMiddle(ImGui.CalcTextSize(text), availSize);
        ImGui.TextUnformatted(text);
        ImGui.SetCursorPos(c + new Vector2(0, availSize.Y + ImGui.GetStyle().ItemSpacing.Y - 1));
    }

    public static bool ButtonCentered(string text, Vector2 buttonSize = default)
    {
        var buttonWidth = buttonSize.X;
        if (buttonSize == default)
            buttonWidth = ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2;
        AlignCentered(buttonWidth);
        return ImGui.Button(text, buttonSize);
    }
}
