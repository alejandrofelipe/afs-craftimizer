using Artificer.Application.CraftingLists;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PluginClass = Artificer.Plugin.Plugin;
using Service = Artificer.Plugin.Service;

namespace Artificer.Windows;

/// <summary>
/// Rota de coleta de uma lista: materiais base faltantes agrupados por zona,
/// com teleporte por zona, flag no mapa por material e preço de Market Board.
/// </summary>
public sealed class CraftingListGatheringWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;

    private Guid? _listId;
    private ResolvedIngredientTree? _tree;
    private Dictionary<uint, MaterialProgress> _progress = new();
    private readonly Dictionary<uint, MarketPrice?> _prices = new();
    private bool _treeLoading;
    private GatheringRoute? _route;

    public CraftingListGatheringWindow(PluginClass plugin) : base("Rota de Coleta###Artificer-cl-gather",
        ImGuiWindowFlags.NoScrollbar)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(420, 360),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public void OpenForList(Guid listId)
    {
        _listId = listId;
        _prices.Clear();
        Refresh();
        IsOpen = true;
        BringToFront();
    }

    private void Refresh()
    {
        if (_listId is not { } id)
            return;
        _progress = _plugin.CraftingListRepository.GetProgressForList(id).ToDictionary(p => p.ItemId);
        _ = RefreshTreeAsync(id);
    }

    private async Task RefreshTreeAsync(Guid id)
    {
        _treeLoading = true;
        try
        {
            _tree = await _plugin.CraftingListManager.ResolveIngredientsAsync(id).ConfigureAwait(false);
            _route = null;
            if (_plugin.Configuration.ShowMarketPrices)
                _ = LoadPricesAsync();
        }
        finally
        {
            _treeLoading = false;
        }
    }

    private async Task LoadPricesAsync()
    {
        if (_tree == null)
            return;
        var player = Service.Objects.LocalPlayer;
        if (player == null)
            return;
        var worldId = player.CurrentWorld.RowId;

        foreach (var itemId in _tree.BaseMaterials.Select(m => m.ItemId).Distinct())
        {
            if (_prices.ContainsKey(itemId))
                continue;
            _prices[itemId] = await _plugin.MarketboardHelper.GetPriceAsync(
                itemId, worldId, string.Empty, _plugin.Configuration.MarketPriceCacheTtlMinutes)
                .ConfigureAwait(false);
                _route = null;
        }
    }

    public override void PreDraw() => Theme.Push();
    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw()
    {
        if (_listId is not { } id)
        {
            ImGuiUtils.DrawEmptyState((int)FontAwesomeIcon.Map, "Nenhuma lista", null);
            return;
        }

        if (_treeLoading && _tree == null)
        {
            ImGuiUtils.DrawStateChip(ImGuiUtils.SolverState.Solving, "Resolvendo materiais...");
            return;
        }
        if (_tree == null)
            return;

        var route = _route ??= BuildRoute();

        if (route.MissingMaterialCount == 0)
        {
            ImGuiUtils.DrawEmptyState((int)FontAwesomeIcon.CheckCircle, "Tudo coletado 🎉",
                "Nenhum material base faltando nesta lista.");
            return;
        }

        var summary = $"{route.MissingMaterialCount} materiais faltando em {route.ZoneCount} zonas";
        if (_plugin.Configuration.ShowMarketPrices && route.TotalBuyCost > 0)
            summary += $" · comprar tudo no MB: ~{route.TotalBuyCost:N0}g";
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(summary);
        ImGui.Separator();

        using var scroll = ImRaii.Child("##gatherScroll", new Vector2(-1, -1), false);
        if (!scroll)
            return;

        foreach (var zone in route.Zones)
            DrawZone(zone);

        if (route.Others.Count > 0)
            DrawOthers(route.Others);
    }

    private GatheringRoute BuildRoute()
    {
        var showPrices = _plugin.Configuration.ShowMarketPrices;
        var inputs = _tree!.BaseMaterials.Select(m =>
        {
            _progress.TryGetValue(m.ItemId, out var prog);
            var collected = prog?.QuantityCollected ?? 0;
            int? unit = showPrices && _prices.TryGetValue(m.ItemId, out var p) && p != null
                ? p.PriceCurrentServer
                : null;
            var loc = m.GatheringLocations.Count > 0 ? m.GatheringLocations[0] : null;
            return new RouteMaterialInput(m.ItemId, m.ItemName, m.Quantity, collected, loc, unit);
        }).ToList();
        return GatheringRoutePlanner.Plan(inputs);
    }

    private void DrawZone(GatheringRouteZone zone)
    {
        using (ImRaii2.GroupPanel($"{zone.ZoneName}  ({zone.Materials.Count})", -1, out _))
        {
            var inCombat = Service.Condition[ConditionFlag.InCombat];
            using (ImRaii.Disabled(!_plugin.TeleportHelper.IsAvailable || inCombat || zone.AetheryteId == 0))
            {
                if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.LocationArrow,
                        $"Teleportar para {zone.AetheryteName}"))
                    _plugin.TeleportHelper.TeleportTo(zone.AetheryteId);
            }
            ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted(zone.AetheryteName);

            foreach (var mat in zone.Materials)
                DrawMaterialRow(mat, withFlag: true);
        }
    }

    private static void DrawOthers(IReadOnlyList<RouteMaterial> others)
    {
        using (ImRaii2.GroupPanel($"Comprar / Outros  ({others.Count})", -1, out _))
        {
            foreach (var mat in others)
                DrawMaterialRow(mat, withFlag: false);
        }
    }

    private static void DrawMaterialRow(RouteMaterial mat, bool withFlag)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using var id = ImRaii.PushId((int)mat.ItemId);

        PluginImGuiUtils.DrawItemIcon(mat.ItemId, ImGui.GetFrameHeight());
        ImGui.SameLine(0, 6 * scale);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(mat.ItemName);
        ImGui.SameLine(0, 8 * scale);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
            ImGui.TextUnformatted($"faltam {mat.Missing}");

        if (withFlag && mat.Location is { MapX: > 0f } loc)
        {
            ImGui.SameLine(0, 6 * scale);
            if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.MapMarkerAlt, "Marcar no mapa"))
                MapFlagHelper.FlagOnMap(loc);
        }

        if (mat.UnitPrice is { } price)
        {
            ImGui.SameLine(0, 8 * scale);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted($"~{price:N0}g/un");
        }
        else if (!withFlag)
        {
            ImGui.SameLine(0, 8 * scale);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted("—");
        }
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
