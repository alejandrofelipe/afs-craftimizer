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
using System.Threading;
using System.Threading.Tasks;
using PluginClass = Artificer.Plugin.Plugin;
using Service = Artificer.Plugin.Service;

namespace Artificer.Windows;

/// <summary>
/// Detail view of a single crafting list: editable recipes, resolved materials,
/// crystals, pre-crafts, inventory sync, market prices, export, and split/move.
/// </summary>
public sealed partial class CraftingListDetailWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;

    private Guid? _listId;
    private CraftingList? _list;
    private List<CraftingListRecipe> _recipes = new();
    private ResolvedIngredientTree? _tree;
    private Dictionary<uint, MaterialProgress> _progress = new();
    private Dictionary<uint, MarketPrice?> _prices = new();
    private readonly CraftingListLoadPolicy _loadPolicy = new();
    private bool _pricesLoading;
    private bool _treeLoading;
    private DateTime? _lastSyncTime;

    // Quantity debounce: recipeId -> (pending value, last-changed game time)
    private readonly Dictionary<Guid, (int Value, double LastChanged)> _quantityEdits = new();

    // Inline remove confirmation: set to recipe id when ✕ is first clicked
    private Guid? _pendingRemoveId;

    // Selection mode (for split / move)
    private bool _selectionMode;
    private readonly HashSet<Guid> _selectedRecipeIds = new();
    private string _moveTargetSelected = string.Empty;

    // Inline rename
    private bool _isRenaming;
    private string _renameBuffer = string.Empty;

    public CraftingListDetailWindow(PluginClass plugin) : base("###Artificer-cl-detail",
        ImGuiWindowFlags.NoScrollbar)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
        _plugin.CraftingListManager.ListsChanged += OnListsChanged;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(460, 420),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public void OpenList(Guid listId)
    {
        _loadPolicy.InvalidateLifecycle();
        if (_listId != listId)
            _tree = null;
        _listId = listId;
        _selectionMode = false;
        _selectedRecipeIds.Clear();
        _isRenaming = false;
        _pendingRemoveId = null;
        RefreshData();
        IsOpen = true;
        BringToFront();
        if (_plugin.Configuration.AutoSyncInventoryOnOpen)
            _ = SyncInventoryAsync();
    }

    private void RefreshData()
    {
        if (_listId is not { } id)
            return;

        if (!TryBeginPriceLoad(out var load))
            return;

        var (generation, token) = load;
        _treeLoading = true;
        try
        {
            _list = _plugin.CraftingListManager.Lists.FirstOrDefault(l => l.Id == id);
            _recipes = _plugin.CraftingListRepository.GetRecipesForList(id);
            var progressList = _plugin.CraftingListRepository.GetProgressForList(id);
            _progress = progressList.ToDictionary(p => p.ItemId);

            var showMarketPrices = _plugin.Configuration.ShowMarketPrices;
            var player = showMarketPrices ? Service.Objects.LocalPlayer : null;
            uint? worldId = null;
            var dataCenterName = string.Empty;
            if (player != null)
            {
                worldId = player.CurrentWorld.RowId;
                dataCenterName = player.CurrentWorld.Value.DataCenter.Value.Name.ExtractText();
            }
            var ttlMinutes = _plugin.Configuration.MarketPriceCacheTtlMinutes;
            var treeRequest = _plugin.CraftingListManager.ResolveIngredientsAsync(id);

            _ = RefreshTreeAsync(
                id,
                treeRequest,
                generation,
                token,
                showMarketPrices,
                worldId,
                dataCenterName,
                ttlMinutes);
        }
        catch (Exception exception)
        {
            _treeLoading = false;
            Log.Warning(exception, $"Failed to start crafting-list tree refresh {id}.");
            if (_loadPolicy.IsPriceLoadCurrent(generation))
                CancelPriceLoad();
        }
    }

    private void OnListsChanged()
    {
        if (_listId is not { } id)
            return;
        // Lista deletada externamente → fechar; senão, recarregar (conserta "não atualiza após add").
        if (_plugin.CraftingListManager.Lists.All(l => l.Id != id))
        {
            _loadPolicy.InvalidateLifecycle();
            _listId = null;
            _list = null;
            _tree = null;
            IsOpen = false;
            CancelPriceLoad();
            return;
        }
        RefreshData();
    }

    public override void PreDraw()
    {
        Theme.Push();
        WindowName = $"{(_list?.Name ?? "Lista")}###Artificer-cl-detail";
    }

    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void OnClose()
    {
        _loadPolicy.InvalidateLifecycle();
        IsOpen = false;
        try
        {
            CancelPriceLoad();
        }
        finally
        {
            base.OnClose();
        }
    }

    public override void Draw()
    {
        if (_listId is not { } || _list is not { } list)
        {
            ImGuiUtils.DrawEmptyState((int)FontAwesomeIcon.ListAlt, "Nenhuma lista aberta", null);
            return;
        }

        FlushPendingEdits();

        DrawHeader(list);
        ImGui.Separator();

        if (_plugin.Configuration.CraftingListViewMode == 1)
            DrawSimpleView();
        else
            DrawDetailedView();
    }

    public void Dispose()
    {
        IsOpen = false;
        try
        {
            var stopped = _loadPolicy.DisposePriceLoad();
            if (stopped.Exception is { } exception)
                Log.Warning(exception, "Failed to dispose crafting-list price load.");
            if (stopped.ShouldClearState)
                ClearPriceLoadState(clearTreeLoading: true);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to tear down crafting-list price load.");
        }
        finally
        {
            try
            {
                _plugin.CraftingListManager.ListsChanged -= OnListsChanged;
            }
            finally
            {
                _plugin.WindowSystem.RemoveWindow(this);
            }
        }
    }
}
