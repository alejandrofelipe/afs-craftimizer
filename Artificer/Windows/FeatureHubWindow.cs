using Artificer.Utils;
using Artificer.Utils.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;
using PluginClass = Artificer.Plugin.Plugin;

namespace Artificer.Windows;

/// <summary>
/// A small always-visible floating launcher button that opens a popup menu
/// giving quick access to the plugin's feature windows (Crafting Lists, settings).
/// Registered unconditionally — independent of the EnableCraftingLists toggle.
/// </summary>
public sealed class FeatureHubWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;
    private bool _firstFrame = true;

    public FeatureHubWindow(PluginClass plugin) : base("###Artificer-feature-hub",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
        IsOpen = true;
    }

    public override void PreDraw()
    {
        if (_firstFrame)
        {
            var displaySize = ImGui.GetIO().DisplaySize;
            var scale = ImGuiHelpers.GlobalScale;
            ImGui.SetNextWindowPos(new Vector2(displaySize.X - 60 * scale, displaySize.Y - 60 * scale));
            _firstFrame = false;
        }
        Theme.Push();
    }

    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw() => DrawGuard.Try(DrawContent);

    private void DrawContent()
    {
        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Boxes))
            ImGui.OpenPopup("##FeatureHubPopup");
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Ferramentas Artificer");

        using var popup = ImRaii.Popup("##FeatureHubPopup");
        if (!popup)
            return;

        using (ImRaii.Disabled(!_plugin.Configuration.EnableCraftingLists))
        {
            if (ImGui.MenuItem("Lista de Coleta"))
                _plugin.CraftingListWindow.Toggle();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !_plugin.Configuration.EnableCraftingLists)
            ImGuiUtils.Tooltip("Habilite em Configurações → General → Crafting Lists");
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted("   Planeje materiais de crafting");

        ImGui.Separator();

        if (ImGui.MenuItem("Configurações"))
            _plugin.OpenSettingsTab("General");
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
