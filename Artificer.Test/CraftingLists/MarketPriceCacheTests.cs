using Artificer.Application.CraftingLists;
using Artificer.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class MarketPriceCacheTests
{
    [TestMethod]
    public void Constructor_WhenSchemaInitializationFails_ReleasesDatabaseFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"artificer-market-prices-{Guid.NewGuid():N}.db");

        try
        {
            using (var setup = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                setup.Open();
                using var command = setup.CreateCommand();
                command.CommandText = "CREATE TABLE crafting_list_recipes (id TEXT)";
                command.ExecuteNonQuery();
            }

            var exception = Assert.ThrowsException<SqliteException>(
                () => _ = new CraftingListRepository(path));

            Assert.AreEqual(1, exception.SqliteErrorCode);
            Assert.IsTrue(
                exception.Message.Contains("no such column: list_id", StringComparison.Ordinal),
                $"Unexpected SQLite error: {exception.Message}");

            SqliteConnection.ClearAllPools();
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // The RED state intentionally leaves the failed constructor's connection open.
                }
            }
        }
    }

    [TestMethod]
    public void ScopePrices_RoundTripWithoutMixingWorldAndDataCenter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"artificer-market-prices-{Guid.NewGuid():N}.db");
        var worldCachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var dataCenterCachedAt = new DateTime(2026, 8, 18, 12, 35, 0, DateTimeKind.Utc);

        try
        {
            using var repository = new CraftingListRepository(path);
            repository.SaveScopePrice(new MarketScopePrice(
                5333, "WorldA", 120, "WorldA", 14, worldCachedAt));
            repository.SaveScopePrice(new MarketScopePrice(
                5333, "Aether", 80, "WorldB", 27, dataCenterCachedAt));

            var worldPrice = repository.GetCachedScopePrice(5333, "WorldA");
            var dataCenterPrice = repository.GetCachedScopePrice(5333, "Aether");

            Assert.AreEqual(
                new MarketScopePrice(5333, "WorldA", 120, "WorldA", 14, worldCachedAt),
                worldPrice);
            Assert.AreEqual(
                new MarketScopePrice(5333, "Aether", 80, "WorldB", 27, dataCenterCachedAt),
                dataCenterPrice);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
