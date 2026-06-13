using Artificer.Plugin;
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

public sealed class FeatureHub : Window, IDisposable
{
    private readonly PluginClass _plugin;

    public FeatureHub(PluginClass plugin) : base("###Artificer-feature-hub",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
    }

    public override void Update()
    {
        IsOpen = Service.ClientState.IsLoggedIn;
    }

    public override void PreDraw()
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowPos(new Vector2(displaySize.X - 60 * scale, displaySize.Y - 60 * scale), ImGuiCond.FirstUseEver);
        Theme.Push();
    }

    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw() => DrawGuard.Try(DrawContent);

    private void DrawContent()
    {
        using (ImRaii.Disabled(!_plugin.Configuration.EnableCraftingLists))
        {
            if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Boxes))
                _plugin.CraftingListWindow.Toggle();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (_plugin.Configuration.EnableCraftingLists)
                ImGuiUtils.Tooltip("Lista de Coleta");
            else
                ImGuiUtils.Tooltip("Lista de Coleta\n(Habilite em Configurações → General)");
        }

        ImGui.SameLine();

        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Cog))
            _plugin.OpenSettingsTab("General");
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Configurações");
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
