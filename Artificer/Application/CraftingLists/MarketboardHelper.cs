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
using System.Threading.Tasks;

namespace Artificer.Application.CraftingLists;

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
    private ICallGateSubscriber<uint, uint, string?>? _ipc;

    /// <summary>True while the Universalis IPC gate is available; flips false on first failure.</summary>
    public bool IsIpcAvailable { get; private set; }

    public MarketboardHelper(IDalamudPluginInterface pi, CraftingListRepository repo)
    {
        _repo = repo;
        try
        {
            _ipc = pi.GetIpcSubscriber<uint, uint, string?>("Universalis.PriceData");
            IsIpcAvailable = true;
        }
        catch
        {
            _ipc = null;
            IsIpcAvailable = false;
        }
    }

    /// <summary>Crystals (item ids 2..19) are tradeable; only the sheet's untradable flag excludes an item.</summary>
    public static bool IsUntradeable(uint itemId) =>
        LuminaSheets.ItemSheet.GetRowOrDefault(itemId)?.IsUntradable ?? false;

    public async Task<MarketPrice?> GetPriceAsync(uint itemId, uint worldId, string dcName, int ttlMinutes)
    {
        if (IsUntradeable(itemId))
            return null;

        var worldOrDc = string.IsNullOrEmpty(dcName) ? worldId.ToString() : dcName;

        var cached = _repo.GetCachedScopePrice(itemId, worldOrDc);
        if (cached != null && !cached.IsStale(ttlMinutes))
            return Combine(cached, null);

        var fetched = await Fetch(itemId, worldId, worldOrDc).ConfigureAwait(false);
        if (fetched == null)
            return Combine(cached, null);

        _repo.SaveScopePrice(fetched);
        return Combine(fetched, null);
    }

    private async Task<MarketScopePrice?> Fetch(uint itemId, uint worldId, string worldOrDc)
    {
        // Try IPC first.
        if (_ipc != null)
        {
            try
            {
                var json = _ipc.InvokeFunc(itemId, worldId);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = ParseScope(itemId, worldOrDc, json, DateTime.UtcNow);
                    if (parsed != null)
                        return parsed;
                }
            }
            catch
            {
                _ipc = null;
                IsIpcAvailable = false;
            }
        }

        // Fallback to REST.
        try
        {
            var url = $"https://universalis.app/api/v2/{Uri.EscapeDataString(worldOrDc)}/{itemId}?listings=5&entries=0";
            var json = await _http.GetStringAsync(url).ConfigureAwait(false);
            return ParseScope(itemId, worldOrDc, json, DateTime.UtcNow);
        }
        catch
        {
            return null;
        }
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

    public void Dispose() => _ipc = null;

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
