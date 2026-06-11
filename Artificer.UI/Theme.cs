using System.Numerics;

namespace Artificer.Utils;

public static class Theme
{
    private static readonly Vector4 BgSurface  = new(0.051f, 0.067f, 0.125f, 1f); // #0D1120
    private static readonly Vector4 BgElevated = new(0.078f, 0.098f, 0.157f, 1f); // #141928
    private static readonly Vector4 BgOverlay  = new(0.106f, 0.133f, 0.208f, 1f); // #1B2235
    private static readonly Vector4 BgHover    = new(0.129f, 0.165f, 0.251f, 1f); // #212A40
    private static readonly Vector4 BgBase     = new(0.024f, 0.031f, 0.063f, 1f); // #060810
    private static readonly Vector4 Border     = new(0.392f, 0.549f, 0.784f, 0.30f);

    private const int ColorCount = 17;
    private const int VarCount   = 3;

    // Dalamud's patched cimgui shifted DisabledAlpha from index 1 to 24,
    // so all other ImGuiStyleVar values are off by 1 vs ImGuiNET defaults.
    // Call ConfigureForDalamud() from Plugin constructor to fix this.
    private static ImGuiStyleVar _windowPadding = ImGuiStyleVar.WindowPadding; // ImGuiNET = 2
    private static ImGuiStyleVar _frameRounding = ImGuiStyleVar.FrameRounding; // ImGuiNET = 12
    private static ImGuiStyleVar _childRounding  = ImGuiStyleVar.ChildRounding;  // ImGuiNET = 7

    public static void ConfigureForDalamud()
    {
        // Dalamud.Bindings.ImGui values: WindowPadding=1, FrameRounding=11, ChildRounding=6
        _windowPadding = (ImGuiStyleVar)1;
        _frameRounding = (ImGuiStyleVar)11;
        _childRounding  = (ImGuiStyleVar)6;
        ImRaii.ConfigureForDalamud();
    }

    public static void Push()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,             BgSurface);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,              BgElevated);
        ImGui.PushStyleColor(ImGuiCol.FrameBg,              BgOverlay);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,       BgHover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,        BgOverlay);
        ImGui.PushStyleColor(ImGuiCol.Border,               Border);
        ImGui.PushStyleColor(ImGuiCol.Button,               BgOverlay);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,        BgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,         BgSurface);
        ImGui.PushStyleColor(ImGuiCol.Header,               BgOverlay);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,        BgHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,         BgSurface);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,              BgSurface);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,        BgElevated);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,          BgBase);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,        BgOverlay);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, BgHover);

        ImGui.PushStyleVar(_windowPadding, new Vector2(12f, 8f) * UiServices.Current.GlobalScale);
        ImGui.PushStyleVar(_frameRounding, 4f * UiServices.Current.GlobalScale);
        ImGui.PushStyleVar(_childRounding,  6f * UiServices.Current.GlobalScale);
    }

    public static void Pop()
    {
        ImGui.PopStyleColor(ColorCount);
        ImGui.PopStyleVar(VarCount);
    }

    private const int PrimaryColorCount = 3;

    public static void PushPrimaryButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        Colors.ActionBuff with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Colors.ActionBuff);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  Colors.ActionBuff with { W = 0.70f });
    }

    public static void PopPrimaryButton() => ImGui.PopStyleColor(PrimaryColorCount);

    private const int DangerColorCount = 3;

    public static void PushDangerButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        Colors.Bad with { W = 0.70f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Colors.Bad with { W = 0.90f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  Colors.Bad with { W = 0.55f });
    }

    public static void PopDangerButton() => ImGui.PopStyleColor(DangerColorCount);
}
