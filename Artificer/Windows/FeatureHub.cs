using Artificer.Plugin;
using Artificer.Utils;
using Artificer.Utils.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using DalamudIcon = Dalamud.Interface.FontAwesomeIcon;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
        Theme.Push();
        if (_plugin.Configuration.FeatureHubAnchored)
        {
            ImGui.SetNextWindowPos(ResolveAnchorPosition(), ImGuiCond.Always);
            Flags |= ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
    }

    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw() => DrawGuard.Try(DrawContent);

    private void DrawContent()
    {
        if (_plugin.Configuration.FeatureHubMinimized)
            DrawMinimized();
        else
            DrawExpanded();
    }

    // ── Minimized ────────────────────────────────────────────────────────────

    private void DrawMinimized()
    {
        var size = 30 * ImGuiHelpers.GlobalScale;
        if (ImGuiUtils.IconButtonSquare((int)DalamudIcon.Hammer, size))
        {
            _plugin.Configuration.FeatureHubMinimized = false;
            _plugin.Configuration.Save();
        }
        ImGuiUtils.HoveredTooltip("Artificer — Clique para expandir");
    }

    // ── Expanded ─────────────────────────────────────────────────────────────

    private void DrawExpanded()
    {
        DrawHeader();
        DrawTiles();
    }

    private void DrawHeader()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var btnSize = 18 * scale;

        // Right-align the two header buttons inside the window's content width
        var contentW = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X * 2;
        var headerW = btnSize * 2 + 4 * scale;
        ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X + Math.Max(0f, contentW - headerW));

        // ⚓ Anchor toggle
        var isAnchored = _plugin.Configuration.FeatureHubAnchored;
        var anchorColor = isAnchored
            ? Colors.FeatureHubAnchored
            : Colors.FeatureHubFree;
        using (ImRaii.PushColor(ImGuiCol.Text, anchorColor))
        {
            if (ImGuiUtils.IconButtonSquare((int)DalamudIcon.Anchor, btnSize))
            {
                _plugin.Configuration.FeatureHubAnchored = !isAnchored;
                _plugin.Configuration.Save();
            }
        }
        ImGuiUtils.HoveredTooltip(isAnchored ? "Ancorado — clique para liberar" : "Livre — clique para ancorar");

        ImGui.SameLine(0, 4 * scale);

        // ⚒️ Minimize button (plugin icon)
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.FeatureHubGold))
        {
            if (ImGuiUtils.IconButtonSquare((int)DalamudIcon.Hammer, btnSize))
            {
                _plugin.Configuration.FeatureHubMinimized = true;
                _plugin.Configuration.Save();
            }
        }
        ImGuiUtils.HoveredTooltip("Minimizar");
    }

    private void DrawTiles()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var craftingEnabled = _plugin.Configuration.EnableCraftingLists;

        if (DrawTile((int)DalamudIcon.Boxes, "Coleta", !craftingEnabled))
            _plugin.CraftingListWindow.Toggle();
        ImGuiUtils.HoveredTooltip(craftingEnabled
            ? "Lista de Coleta"
            : "Lista de Coleta\n(Habilite em Configurações → General)", (int)ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.SameLine(0, 5 * scale);

        if (DrawTile((int)DalamudIcon.Cog, "Config."))
            _plugin.OpenSettingsTab("General");
        ImGuiUtils.HoveredTooltip("Configurações");
    }

    // ── Tile widget ───────────────────────────────────────────────────────────

    private static bool DrawTile(int icon, string label, bool disabled = false)
    {
        const float TileW = 56f;
        const float TileH = 52f;
        const float IconBoxSize = 28f;

        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(TileW * scale, TileH * scale);
        var iconBox = new Vector2(IconBoxSize * scale);
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        bool clicked;
        using (ImRaii.Disabled(disabled))
            clicked = ImGui.InvisibleButton($"##tile_{label}", size);

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !disabled;

        // Tile: hover fill + border
        if (hovered)
            dl.AddRectFilled(pos, pos + size, 0x0FFFFFFF, 7 * scale);
        dl.AddRect(pos, pos + size, 0x12FFFFFF, 7 * scale);

        // Icon background box (centered horizontally, 6px from top)
        var iconBgPos = new Vector2(pos.X + (size.X - iconBox.X) / 2f, pos.Y + 6 * scale);
        uint fillColor   = disabled ? 0x22555555u : 0x226ea9c8u; // ABGR: gold #c8a96e or grey
        uint borderColor = disabled ? 0x55555555u : 0x556ea9c8u;
        dl.AddRectFilled(iconBgPos, iconBgPos + iconBox, fillColor, 7 * scale);
        dl.AddRect(iconBgPos, iconBgPos + iconBox, borderColor, 7 * scale);

        // Centered icon inside the box
        ImGuiUtils.DrawCenteredIcon((Artificer.Utils.FontAwesomeIcon)(ushort)icon, iconBgPos, iconBox, disabled);

        // Label: centered horizontally, 5px from bottom
        var labelSize = ImGui.CalcTextSize(label);
        var labelPos = new Vector2(
            pos.X + (size.X - labelSize.X) / 2f,
            pos.Y + size.Y - labelSize.Y - 5 * scale);
        uint labelColor = disabled ? 0xFF555555u : 0xFF6ea9c8u;
        dl.AddText(labelPos, labelColor, label);

        return clicked;
    }

    // ── Anchor position ───────────────────────────────────────────────────────

    private Vector2 ResolveAnchorPosition()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var displaySize = ImGui.GetIO().DisplaySize;

        // hubWidth from last frame; 0 on first frame — use safe default
        var hubWidth = ImGui.GetWindowSize().X;
        if (hubWidth == 0) hubWidth = 140 * scale;

        return TryGetAddonBottomLeft("_NaviMap")
            ?? TryGetAddonBottomLeft("_ToDoList")
            ?? TryGetAddonBottomLeft("ScenarioTree")
            ?? new Vector2(displaySize.X - hubWidth - 30 * scale, 160 * scale);
    }

    private unsafe Vector2? TryGetAddonBottomLeft(string addonName)
    {
        var addr = Service.GameGui.GetAddonByName(addonName).Address;
        if (addr == nint.Zero) return null;
        var unit = (AtkUnitBase*)addr;
        if (!unit->IsVisible || unit->RootNode == null) return null;
        return new Vector2(unit->X, unit->Y + unit->RootNode->Height * unit->Scale);
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
