using System;
using System.Threading;

namespace Artificer.Utils;

internal readonly record struct PriceLoadStartResult(
    bool Started,
    long Generation,
    CancellationToken Token,
    bool ShouldClearState,
    Exception? Exception);

internal readonly record struct PriceLoadStopResult(bool ShouldClearState, Exception? Exception);

internal sealed class CraftingListLoadPolicy
{
    private readonly CancellationGeneration _priceLoad = new();
    private long _priceLoadGeneration;
    private long _lifecycleEpoch;
    private bool _disposed;

    public long CaptureLifecycleEpoch() => _lifecycleEpoch;

    public void InvalidateLifecycle() => _lifecycleEpoch++;

    public bool CanCompleteInventorySync(
        long startedLifecycle,
        Guid startedListId,
        Guid? currentListId,
        bool isOpen,
        bool listExists) =>
        !_disposed &&
        _lifecycleEpoch == startedLifecycle &&
        isOpen &&
        currentListId == startedListId &&
        listExists;

    public bool IsPriceLoadCurrent(long generation) => _priceLoad.IsCurrent(generation);

    public PriceLoadStartResult TryBeginManualPriceLoad(
        bool treeLoading,
        bool showMarketPrices,
        bool hasList,
        bool hasTree,
        bool hasPlayer)
    {
        if (treeLoading || !showMarketPrices || !hasList || !hasTree || !hasPlayer)
            return default;

        return TryBeginPriceLoad();
    }

    public PriceLoadStartResult TryBeginPriceLoad()
    {
        var previousOwner = _priceLoadGeneration;
        try
        {
            var load = _priceLoad.Begin();
            if (!_priceLoad.IsCurrent(load.Generation))
                return default;

            _priceLoadGeneration = load.Generation;
            return new(true, load.Generation, load.Token, false, null);
        }
        catch (Exception exception)
        {
            return new(
                Started: false,
                Generation: 0,
                Token: default,
                ShouldClearState: _priceLoadGeneration == previousOwner,
                Exception: exception);
        }
    }

    public PriceLoadStopResult CancelPriceLoad()
    {
        var owner = _priceLoadGeneration;
        Exception? failure = null;
        try
        {
            _priceLoad.Cancel(owner);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        return new(_priceLoadGeneration == owner, failure);
    }

    public PriceLoadStopResult DisposePriceLoad()
    {
        if (_disposed)
            return default;

        _disposed = true;
        InvalidateLifecycle();
        var owner = _priceLoadGeneration;
        Exception? failure = null;
        try
        {
            _priceLoad.Dispose();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        return new(_priceLoadGeneration == owner, failure);
    }
}
