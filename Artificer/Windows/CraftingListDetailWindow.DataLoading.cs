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

public sealed partial class CraftingListDetailWindow
{
    private async Task RefreshTreeAsync(
        Guid listId,
        Task<ResolvedIngredientTree> treeRequest,
        long generation,
        CancellationToken token,
        bool showMarketPrices,
        uint? worldId,
        string dataCenterName,
        int ttlMinutes)
    {
        try
        {
            var tree = await treeRequest.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            var shouldLoadPrices = await Service.Framework.Run(() =>
            {
                if (!_loadPolicy.IsPriceLoadCurrent(generation) || _listId != listId)
                    return false;

                _tree = tree;
                _treeLoading = false;
                if (!showMarketPrices || worldId is null)
                {
                    _pricesLoading = false;
                    return false;
                }

                _pricesLoading = true;
                return true;
            }, token).ConfigureAwait(false);

            if (shouldLoadPrices)
            {
                await LoadPricesAsync(
                    listId,
                    tree,
                    generation,
                    token,
                    worldId.GetValueOrDefault(),
                    dataCenterName,
                    ttlMinutes).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer refresh, close, or dispose owns the state now.
        }
        catch (Exception exception)
        {
            Log.Warning(exception, $"Failed to refresh crafting-list tree {listId}.");
        }
        finally
        {
            await FinishTreeLoadingAsync(listId, generation, token).ConfigureAwait(false);
        }
    }

    private async Task SyncInventoryAsync()
    {
        if (_listId is not { } id)
            return;

        var lifecycleEpoch = _loadPolicy.CaptureLifecycleEpoch();
        try
        {
            await _plugin.CraftingListManager.SyncWithInventoryAsync(
                id,
                _plugin.Configuration.IncludeRetainersInSync).ConfigureAwait(false);
            await Service.Framework.Run(() =>
            {
                var listExists = _plugin.CraftingListManager.Lists.Any(list => list.Id == id);
                if (!_loadPolicy.CanCompleteInventorySync(
                        lifecycleEpoch,
                        id,
                        _listId,
                        IsOpen,
                        listExists))
                    return;

                _lastSyncTime = DateTime.UtcNow;
                RefreshData();
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, $"Failed to sync inventory for crafting list {id}.");
        }
    }

    private async Task LoadPricesAsync(
        Guid listId,
        ResolvedIngredientTree tree,
        long generation,
        CancellationToken token,
        uint worldId,
        string dataCenterName,
        int ttlMinutes)
    {
        try
        {
            var loadedPrices = new Dictionary<uint, MarketPrice?>();
            var items = tree.BaseMaterials.Concat(tree.Crystals).Select(m => m.ItemId).Distinct().ToList();
            foreach (var itemId in items)
            {
                token.ThrowIfCancellationRequested();
                var price = await _plugin.MarketboardHelper.GetPriceAsync(
                    itemId,
                    worldId,
                    dataCenterName,
                    ttlMinutes,
                    token).ConfigureAwait(false);
                loadedPrices.Add(itemId, price);
            }

            await Service.Framework.Run(() =>
            {
                if (!_loadPolicy.IsPriceLoadCurrent(generation) || _listId != listId)
                    return;

                _prices = loadedPrices;
                _pricesLoading = false;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer refresh, close, or dispose owns the state now.
        }
        catch (Exception exception)
        {
            Log.Warning(exception, $"Failed to load market prices for crafting list {listId}.");
        }
        finally
        {
            await FinishPriceLoadingAsync(listId, generation, token).ConfigureAwait(false);
        }
    }

    private void RefreshPrices()
    {
        var showMarketPrices = _plugin.Configuration.ShowMarketPrices;
        var listId = _listId;
        var tree = _tree;
        var player = showMarketPrices ? Service.Objects.LocalPlayer : null;
        var start = _loadPolicy.TryBeginManualPriceLoad(
            _treeLoading,
            showMarketPrices,
            listId.HasValue,
            tree is not null,
            player is not null);
        if (!ApplyPriceLoadStart(start, out var load))
            return;

        var (generation, token) = load;
        var capturedListId = listId.GetValueOrDefault();
        var capturedTree = tree!;
        var capturedPlayer = player!;
        var worldId = capturedPlayer.CurrentWorld.RowId;
        var dataCenterName = capturedPlayer.CurrentWorld.Value.DataCenter.Value.Name.ExtractText();
        var ttlMinutes = _plugin.Configuration.MarketPriceCacheTtlMinutes;
        _pricesLoading = true;
        _ = LoadPricesAsync(
            capturedListId,
            capturedTree,
            generation,
            token,
            worldId,
            dataCenterName,
            ttlMinutes);
    }

    private bool TryBeginPriceLoad(out (long Generation, CancellationToken Token) load) =>
        ApplyPriceLoadStart(_loadPolicy.TryBeginPriceLoad(), out load);

    private bool ApplyPriceLoadStart(
        PriceLoadStartResult start,
        out (long Generation, CancellationToken Token) load)
    {
        if (start.Exception is { } exception)
            Log.Warning(exception, "Failed to start crafting-list price load.");

        if (!start.Started)
        {
            if (start.ShouldClearState)
                ClearPriceLoadState(clearTreeLoading: true);
            load = default;
            return false;
        }

        _prices = new();
        _pricesLoading = false;
        load = (start.Generation, start.Token);
        return true;
    }

    private void CancelPriceLoad()
    {
        var stopped = _loadPolicy.CancelPriceLoad();
        if (stopped.Exception is { } exception)
            Log.Warning(exception, "Failed to cancel crafting-list price load.");
        if (stopped.ShouldClearState)
            ClearPriceLoadState(clearTreeLoading: true);
    }

    private void ClearPriceLoadState(bool clearTreeLoading)
    {
        _prices = new();
        _pricesLoading = false;
        if (clearTreeLoading)
            _treeLoading = false;
    }

    private async Task FinishTreeLoadingAsync(Guid listId, long generation, CancellationToken token)
    {
        try
        {
            await Service.Framework.Run(() =>
            {
                if (_loadPolicy.IsPriceLoadCurrent(generation) && _listId == listId)
                    _treeLoading = false;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The superseding generation already owns the loading state.
        }
        catch (Exception exception)
        {
            Log.Warning(exception, $"Failed to finish crafting-list tree refresh {listId}.");
        }
    }

    private async Task FinishPriceLoadingAsync(Guid listId, long generation, CancellationToken token)
    {
        try
        {
            await Service.Framework.Run(() =>
            {
                if (_loadPolicy.IsPriceLoadCurrent(generation) && _listId == listId)
                    _pricesLoading = false;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The superseding generation already owns the loading state.
        }
        catch (Exception exception)
        {
            Log.Warning(exception, $"Failed to finish market-price refresh for crafting list {listId}.");
        }
    }

    private void FlushPendingEdits()
    {
        var now = ImGui.GetTime();
        foreach (var (id, (value, changedAt)) in _quantityEdits.ToList())
        {
            if (now - changedAt >= 0.3)
            {
                _ = _plugin.CraftingListManager.UpdateRecipeQuantityAsync(id, value);
                _quantityEdits.Remove(id);
                RefreshData();
            }
        }
    }
}
