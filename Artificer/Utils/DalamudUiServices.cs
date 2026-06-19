// Artificer/Utils/DalamudUiServices.cs
using Artificer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using System;
using System.Numerics;
using DalamudImGui = Dalamud.Bindings.ImGui.ImGui;
using DalamudStyleVar = Dalamud.Bindings.ImGui.ImGuiStyleVar;
using DalamudKey = Dalamud.Bindings.ImGui.ImGuiKey;
using ImGuiNET;

namespace Artificer.Plugin;

internal sealed class DalamudUiServices : IUiServices
{
    private readonly IDalamudPluginInterface _pluginInterface;

    public DalamudUiServices(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public float GlobalScale => ImGuiHelpers.GlobalScale;

    // UiBuilder.IconFont returns Dalamud.Bindings.ImGui.ImFontPtr.
    // Convert via the underlying native pointer since both wrappers share the same ImFont*.
    public unsafe ImFontPtr IconFont => new ImFontPtr((nint)UiBuilder.IconFont.Handle);

    public unsafe ImFontPtr DefaultFont => new ImFontPtr((nint)UiBuilder.DefaultFont.Handle);

    public void OpenLink(string url) =>
        Dalamud.Utility.Util.OpenLink(url);

    // Maps Artificer.UI's neutral ImGuiStyleVarId to Dalamud's named enum constants.
    // Uses named constants (not raw ints) so a Dalamud rename causes a compile error
    // rather than a runtime crash.
    private static DalamudStyleVar ToDalamud(ImGuiStyleVarId id) => id switch
    {
        ImGuiStyleVarId.WindowPadding => DalamudStyleVar.WindowPadding,
        ImGuiStyleVarId.FrameRounding => DalamudStyleVar.FrameRounding,
        ImGuiStyleVarId.ChildRounding  => DalamudStyleVar.ChildRounding,
        ImGuiStyleVarId.FramePadding   => DalamudStyleVar.FramePadding,
        ImGuiStyleVarId.ItemSpacing    => DalamudStyleVar.ItemSpacing,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Add entry to DalamudUiServices.ToDalamud()")
    };

    public void PushStyleVar(ImGuiStyleVarId id, float val) =>
        DalamudImGui.PushStyleVar(ToDalamud(id), val);

    public void PushStyleVar(ImGuiStyleVarId id, Vector2 val) =>
        DalamudImGui.PushStyleVar(ToDalamud(id), val);

    // Maps Artificer.UI's neutral ImGuiKeyId to Dalamud's named enum constants.
    private static DalamudKey ToDalamud(ImGuiKeyId id) => id switch
    {
        ImGuiKeyId.Enter  => DalamudKey.Enter,
        ImGuiKeyId.Escape => DalamudKey.Escape,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Add entry to DalamudUiServices.ToDalamud(ImGuiKeyId)")
    };

    public bool IsKeyPressed(ImGuiKeyId key) =>
        DalamudImGui.IsKeyPressed(ToDalamud(key));
}
