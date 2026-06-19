// Artificer.UIStudio/StubUiServices.cs
using Artificer.Utils;
using ImGuiNET;
using System;
using System.Numerics;

namespace Artificer.UIStudio;

internal sealed class StubUiServices : IUiServices
{
    public float GlobalScale => 1f;
    public ImFontPtr IconFont => ImGui.GetFont();
    public ImFontPtr DefaultFont => ImGui.GetFont();
    public void OpenLink(string url) { }

    // Maps Artificer.UI's neutral ImGuiStyleVarId to ImGuiNET's named enum constants.
    // UIStudio runs against the standard cimgui (not Dalamud-patched), so standard values are correct.
    private static ImGuiStyleVar ToImGuiNET(ImGuiStyleVarId id) => id switch
    {
        ImGuiStyleVarId.WindowPadding => ImGuiStyleVar.WindowPadding,
        ImGuiStyleVarId.FrameRounding => ImGuiStyleVar.FrameRounding,
        ImGuiStyleVarId.ChildRounding  => ImGuiStyleVar.ChildRounding,
        ImGuiStyleVarId.FramePadding   => ImGuiStyleVar.FramePadding,
        ImGuiStyleVarId.ItemSpacing    => ImGuiStyleVar.ItemSpacing,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Add entry to StubUiServices.ToImGuiNET()")
    };

    public void PushStyleVar(ImGuiStyleVarId id, float val) =>
        ImGui.PushStyleVar(ToImGuiNET(id), val);

    public void PushStyleVar(ImGuiStyleVarId id, Vector2 val) =>
        ImGui.PushStyleVar(ToImGuiNET(id), val);

    // Maps Artificer.UI's neutral ImGuiKeyId to ImGuiNET's named enum constants.
    private static ImGuiKey ToImGuiNET(ImGuiKeyId id) => id switch
    {
        ImGuiKeyId.Enter  => ImGuiKey.Enter,
        ImGuiKeyId.Escape => ImGuiKey.Escape,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Add entry to StubUiServices.ToImGuiNET(ImGuiKeyId)")
    };

    public bool IsKeyPressed(ImGuiKeyId key) =>
        ImGui.IsKeyPressed(ToImGuiNET(key));
}
