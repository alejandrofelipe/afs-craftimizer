using Artificer.Application.CraftingLists;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class MarketboardHelperTests
{
    [TestMethod]
    public void ParseScope_ListingsOutOfOrder_UsesLowestPriceWorldAndAllQuantities()
    {
        var cachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        const string json = """
            {
              "minPrice": 80,
              "listings": [
                { "pricePerUnit": 120, "quantity": 4, "worldName": "WorldA" },
                { "pricePerUnit": 80, "quantity": 7, "worldName": "WorldB" },
                { "pricePerUnit": 100, "quantity": 3, "worldName": "WorldC" }
              ]
            }
            """;

        var result = MarketboardHelper.ParseScope(5333, "Aether", json, cachedAt);

        Assert.AreEqual(
            new MarketScopePrice(5333, "Aether", 80, "WorldB", 14, cachedAt),
            result);
    }

    [TestMethod]
    public void ParseScope_WithoutListings_UsesMinPriceAndEmptyAvailability()
    {
        var cachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        const string json = """
            {
              "minPrice": 95,
              "listings": []
            }
            """;

        var result = MarketboardHelper.ParseScope(5333, "WorldA", json, cachedAt);

        Assert.AreEqual(
            new MarketScopePrice(5333, "WorldA", 95, string.Empty, 0, cachedAt),
            result);
    }

    [TestMethod]
    public void ParseScope_WithInvalidJson_ReturnsNull()
    {
        var result = MarketboardHelper.ParseScope(
            5333,
            "WorldA",
            "{ invalid json",
            new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseScope_WithJsonNull_ReturnsNull()
    {
        var result = MarketboardHelper.ParseScope(
            5333,
            "WorldA",
            "null",
            new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Combine_WithWorldAndDataCenter_UsesEachScopeAndOldestTimestamp()
    {
        var worldCachedAt = new DateTime(2026, 8, 18, 12, 35, 0, DateTimeKind.Utc);
        var dataCenterCachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var currentWorld = new MarketScopePrice(
            5333, "WorldA", 120, "WorldA", 14, worldCachedAt);
        var dataCenter = new MarketScopePrice(
            5333, "Aether", 80, "WorldB", 27, dataCenterCachedAt);

        var combined = MarketboardHelper.Combine(currentWorld, dataCenter);

        Assert.AreEqual(
            new MarketPrice(5333, 120, 80, "WorldB", 14, dataCenterCachedAt),
            combined);
    }

    [TestMethod]
    public void Combine_WithOlderWorldSnapshot_UsesWorldTimestamp()
    {
        var worldCachedAt = new DateTime(2026, 8, 18, 12, 25, 0, DateTimeKind.Utc);
        var dataCenterCachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var currentWorld = new MarketScopePrice(
            5333, "WorldA", 120, "WorldA", 14, worldCachedAt);
        var dataCenter = new MarketScopePrice(
            5333, "Aether", 80, "WorldB", 27, dataCenterCachedAt);

        var combined = MarketboardHelper.Combine(currentWorld, dataCenter);

        Assert.AreEqual(
            new MarketPrice(5333, 120, 80, "WorldB", 14, worldCachedAt),
            combined);
    }

    [TestMethod]
    public void Combine_WithoutDataCenter_ReusesCurrentWorld()
    {
        var cachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var currentWorld = new MarketScopePrice(
            5333, "WorldA", 120, "WorldA", 14, cachedAt);

        var combined = MarketboardHelper.Combine(currentWorld, null);

        Assert.AreEqual(
            new MarketPrice(5333, 120, 120, "WorldA", 14, cachedAt),
            combined);
    }

    [TestMethod]
    public void Combine_WithoutCurrentWorld_ReturnsNull()
    {
        var dataCenter = new MarketScopePrice(
            5333,
            "Aether",
            80,
            "WorldB",
            27,
            new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc));

        var combined = MarketboardHelper.Combine(null, dataCenter);

        Assert.IsNull(combined);
    }
}
