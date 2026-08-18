using Artificer.Data;
using Artificer.Plugin;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Artificer.Application.CraftingLists;

internal enum MarketboardFrameworkOperation
{
    CacheRead,
    CacheWrite,
    Ipc
}

internal interface IMarketboardFrameworkDispatcher
{
    Task<T> RunAsync<T>(
        MarketboardFrameworkOperation operation,
        Func<T> action,
        CancellationToken token);
}

internal interface IMarketboardTransport
{
    Task<MarketScopePrice?> FetchWorldAsync(
        uint itemId,
        uint worldId,
        CancellationToken token);

    Task<MarketScopePrice?> FetchScopeAsync(
        uint itemId,
        string scope,
        CancellationToken token);
}

internal sealed class DalamudMarketboardFrameworkDispatcher : IMarketboardFrameworkDispatcher
{
    public Task<T> RunAsync<T>(
        MarketboardFrameworkOperation operation,
        Func<T> action,
        CancellationToken token) =>
        Service.Framework.Run(action, token);
}

internal sealed class UniversalisMarketboardTransport : IMarketboardTransport, IDisposable
{
    private readonly HttpClient _http;
    private readonly IMarketboardFrameworkDispatcher _dispatcher;
    private Func<uint, uint, string?>? _invokeIpc;

    internal UniversalisMarketboardTransport(
        IDalamudPluginInterface pluginInterface,
        HttpClient http,
        IMarketboardFrameworkDispatcher dispatcher)
    {
        _http = http;
        _dispatcher = dispatcher;
        try
        {
            var ipc = pluginInterface.GetIpcSubscriber<uint, uint, string?>("Universalis.PriceData");
            _invokeIpc = ipc.InvokeFunc;
        }
        catch
        {
            _invokeIpc = null;
        }
    }

    internal UniversalisMarketboardTransport(
        Func<uint, uint, string?>? invokeIpc,
        HttpClient http,
        IMarketboardFrameworkDispatcher dispatcher)
    {
        _invokeIpc = invokeIpc;
        _http = http;
        _dispatcher = dispatcher;
    }

    internal bool IsIpcAvailable => _invokeIpc != null;

    public async Task<MarketScopePrice?> FetchWorldAsync(
        uint itemId,
        uint worldId,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_invokeIpc != null)
        {
            try
            {
                var json = await _dispatcher.RunAsync(
                    MarketboardFrameworkOperation.Ipc,
                    () => _invokeIpc(itemId, worldId),
                    token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(json))
                {
                    var cachedAt = DateTime.UtcNow;
                    var parsed = MarketboardHelper.ParseScope(
                        itemId,
                        worldId.ToString(),
                        json,
                        cachedAt);
                    if (parsed != null)
                        return parsed;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                _invokeIpc = null;
            }
        }

        token.ThrowIfCancellationRequested();
        return await FetchScopeAsync(itemId, worldId.ToString(), token).ConfigureAwait(false);
    }

    public async Task<MarketScopePrice?> FetchScopeAsync(
        uint itemId,
        string scope,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            var url = $"https://universalis.app/api/v2/{Uri.EscapeDataString(scope)}/{itemId}?listings=5&entries=0";
            var json = await _http.GetStringAsync(url, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var cachedAt = DateTime.UtcNow;
            return MarketboardHelper.ParseScope(itemId, scope, json, cachedAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _invokeIpc = null;
}

/// <summary>
/// Fetches marketboard prices for items. Tries the Universalis plugin's IPC gate first,
/// falling back to the public Universalis REST API. Results are cached in the local
/// SQLite database with a configurable TTL.
/// </summary>
public sealed class MarketboardHelper : IDisposable
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CraftingListRepository _repo;
    private readonly IMarketboardTransport _transport;
    private readonly IMarketboardFrameworkDispatcher _dispatcher;
    private readonly Func<uint, bool> _isUntradeable;

    /// <summary>True while the Universalis IPC gate is available; flips false on first failure.</summary>
    public bool IsIpcAvailable =>
        _transport is UniversalisMarketboardTransport transport && transport.IsIpcAvailable;

    public MarketboardHelper(IDalamudPluginInterface pi, CraftingListRepository repo)
    {
        var dispatcher = new DalamudMarketboardFrameworkDispatcher();
        _repo = repo;
        _dispatcher = dispatcher;
        _transport = new UniversalisMarketboardTransport(pi, _http, dispatcher);
        _isUntradeable = IsUntradeable;
    }

    internal MarketboardHelper(
        CraftingListRepository repo,
        IMarketboardTransport transport,
        IMarketboardFrameworkDispatcher dispatcher,
        Func<uint, bool> isUntradeable)
    {
        _repo = repo;
        _transport = transport;
        _dispatcher = dispatcher;
        _isUntradeable = isUntradeable;
    }

    /// <summary>Crystals (item ids 2..19) are tradeable; only the sheet's untradable flag excludes an item.</summary>
    public static bool IsUntradeable(uint itemId) =>
        LuminaSheets.ItemSheet.GetRowOrDefault(itemId)?.IsUntradable ?? false;

    public async Task<MarketPrice?> GetPriceAsync(
        uint itemId,
        uint worldId,
        string dataCenterName,
        int ttlMinutes,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (_isUntradeable(itemId))
            return null;

        var worldScope = worldId.ToString();
        var currentWorld = await GetScopePriceAsync(
            itemId,
            worldScope,
            ttlMinutes,
            token,
            fetchToken => _transport.FetchWorldAsync(itemId, worldId, fetchToken)).ConfigureAwait(false);

        token.ThrowIfCancellationRequested();
        MarketScopePrice? dataCenter = null;
        if (!string.IsNullOrEmpty(dataCenterName) &&
            !string.Equals(dataCenterName, worldScope, StringComparison.Ordinal))
        {
            dataCenter = await GetScopePriceAsync(
                itemId,
                dataCenterName,
                ttlMinutes,
                token,
                fetchToken => _transport.FetchScopeAsync(
                    itemId,
                    dataCenterName,
                    fetchToken)).ConfigureAwait(false);
        }

        return Combine(currentWorld, dataCenter);
    }

    private async Task<MarketScopePrice?> GetScopePriceAsync(
        uint itemId,
        string scope,
        int ttlMinutes,
        CancellationToken token,
        Func<CancellationToken, Task<MarketScopePrice?>> fetchAsync)
    {
        token.ThrowIfCancellationRequested();
        var cached = await _dispatcher.RunAsync(
            MarketboardFrameworkOperation.CacheRead,
            () => _repo.GetCachedScopePrice(itemId, scope),
            token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (cached != null && !cached.IsStale(ttlMinutes))
            return cached;

        MarketScopePrice? fetched;
        try
        {
            fetched = await fetchAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return cached;
        }

        token.ThrowIfCancellationRequested();
        if (fetched == null)
            return cached;

        await _dispatcher.RunAsync(
            MarketboardFrameworkOperation.CacheWrite,
            () =>
            {
                _repo.SaveScopePrice(fetched);
                return fetched;
            },
            token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return fetched;
    }

    internal static MarketScopePrice? ParseScope(
        uint itemId,
        string scope,
        string json,
        DateTime cachedAt)
    {
        UniversalisResponse? resp;
        try
        {
            resp = JsonSerializer.Deserialize<UniversalisResponse>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
        if (resp == null)
            return null;

        var listings = resp.Listings ?? new List<UniversalisListing>();
        var cheapest = listings.MinBy(listing => listing.PricePerUnit);
        var cheapestPrice = cheapest?.PricePerUnit ?? resp.MinPrice;
        var cheapestServer = cheapest?.WorldName ?? string.Empty;

        var total = 0;
        foreach (var l in listings)
            total += l.Quantity;

        return new MarketScopePrice(
            itemId,
            scope,
            cheapestPrice,
            cheapestServer,
            total,
            cachedAt);
    }

    internal static MarketPrice? Combine(
        MarketScopePrice? currentWorld,
        MarketScopePrice? dataCenter)
    {
        if (currentWorld == null)
            return null;

        var cheapest = dataCenter ?? currentWorld;
        var cachedAt = currentWorld.CachedAt <= cheapest.CachedAt
            ? currentWorld.CachedAt
            : cheapest.CachedAt;

        return new MarketPrice(
            currentWorld.ItemId,
            currentWorld.PricePerUnit,
            cheapest.PricePerUnit,
            cheapest.ServerName,
            currentWorld.TotalAvailable,
            cachedAt);
    }

    public void Dispose()
    {
        if (_transport is IDisposable disposable)
            disposable.Dispose();
    }

    private sealed record UniversalisResponse(
        [property: JsonPropertyName("minPrice")] int MinPrice,
        [property: JsonPropertyName("listings")] List<UniversalisListing>? Listings
    );

    private sealed record UniversalisListing(
        [property: JsonPropertyName("pricePerUnit")] int PricePerUnit,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("worldName")] string? WorldName
    );
}
