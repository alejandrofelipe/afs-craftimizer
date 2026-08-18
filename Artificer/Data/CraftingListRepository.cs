using Artificer.Application.CraftingLists;
using Dalamud.Plugin;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace Artificer.Data;

/// <summary>
/// Persistent storage for crafting lists in a local SQLite database.
/// The database is created automatically at
/// <c>%APPDATA%\XIVLauncher\pluginConfigs\Artificer\crafting_lists.db</c> if it does not exist.
/// </summary>
public sealed class CraftingListRepository : IDisposable
{
    private readonly SqliteConnection _db;

    public CraftingListRepository(IDalamudPluginInterface pluginInterface)
        : this(GetDatabasePath(pluginInterface))
    {
    }

    internal CraftingListRepository(string databasePath)
    {
        _db = new SqliteConnection($"Data Source={databasePath}");
        try
        {
            _db.Open();
            EnsureSchema();
        }
        catch
        {
            _db.Dispose();
            throw;
        }
    }

    private static string GetDatabasePath(IDalamudPluginInterface pluginInterface)
    {
        var dir = pluginInterface.GetPluginConfigDirectory();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "crafting_lists.db");
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        Exec("PRAGMA journal_mode=WAL");
        Exec("PRAGMA foreign_keys=ON");
        Exec("""
            CREATE TABLE IF NOT EXISTS crafting_lists (
                id           TEXT    PRIMARY KEY,
                name         TEXT    NOT NULL DEFAULT '',
                created_at   INTEGER NOT NULL,
                updated_at   INTEGER NOT NULL,
                completed_at INTEGER,
                sort_order   INTEGER NOT NULL DEFAULT 0
            )
            """);
        Exec("""
            CREATE TABLE IF NOT EXISTS crafting_list_recipes (
                id         TEXT    PRIMARY KEY,
                list_id    TEXT    NOT NULL REFERENCES crafting_lists(id) ON DELETE CASCADE,
                recipe_id  INTEGER NOT NULL,
                item_id    INTEGER NOT NULL,
                quantity   INTEGER NOT NULL DEFAULT 1,
                added_at   INTEGER NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            )
            """);
        Exec("""
            CREATE TABLE IF NOT EXISTS material_progress (
                id                 TEXT    PRIMARY KEY,
                list_id            TEXT    NOT NULL REFERENCES crafting_lists(id) ON DELETE CASCADE,
                item_id            INTEGER NOT NULL,
                quantity_needed    INTEGER NOT NULL,
                quantity_collected INTEGER NOT NULL DEFAULT 0,
                updated_at         INTEGER NOT NULL,
                UNIQUE(list_id, item_id)
            )
            """);
        Exec("""
            CREATE TABLE IF NOT EXISTS market_price_cache (
                item_id         INTEGER NOT NULL,
                world_or_dc     TEXT    NOT NULL,
                price_per_unit  INTEGER NOT NULL,
                total_quantity  INTEGER NOT NULL,
                cheapest_server TEXT    NOT NULL DEFAULT '',
                cached_at       INTEGER NOT NULL,
                PRIMARY KEY (item_id, world_or_dc)
            )
            """);
        Exec("CREATE INDEX IF NOT EXISTS idx_recipes_list  ON crafting_list_recipes(list_id)");
        Exec("CREATE INDEX IF NOT EXISTS idx_progress_list ON material_progress(list_id)");
        Exec("CREATE INDEX IF NOT EXISTS idx_prices_item   ON market_price_cache(item_id)");
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    public List<CraftingList> GetAllLists()
    {
        var result = new List<CraftingList>();
        using var cmd = Command(
            "SELECT id, name, created_at, updated_at, completed_at, sort_order FROM crafting_lists ORDER BY sort_order DESC, created_at DESC");
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(ReadList(r));
        return result;
    }

    public CraftingList? GetList(Guid id)
    {
        using var cmd = Command(
            "SELECT id, name, created_at, updated_at, completed_at, sort_order FROM crafting_lists WHERE id=$id");
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadList(r) : null;
    }

    public void InsertList(CraftingList list)
    {
        using var cmd = Command(
            "INSERT INTO crafting_lists (id, name, created_at, updated_at, completed_at, sort_order) VALUES ($id, $name, $created, $updated, $completed, $order)");
        cmd.Parameters.AddWithValue("$id", list.Id.ToString());
        cmd.Parameters.AddWithValue("$name", list.Name);
        cmd.Parameters.AddWithValue("$created", ToUnix(list.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", ToUnix(list.UpdatedAt));
        cmd.Parameters.AddWithValue("$completed", ToUnixNullable(list.CompletedAt));
        cmd.Parameters.AddWithValue("$order", list.SortOrder);
        cmd.ExecuteNonQuery();
    }

    public void UpdateList(CraftingList list)
    {
        using var cmd = Command(
            "UPDATE crafting_lists SET name=$name, created_at=$created, updated_at=$updated, completed_at=$completed, sort_order=$order WHERE id=$id");
        cmd.Parameters.AddWithValue("$id", list.Id.ToString());
        cmd.Parameters.AddWithValue("$name", list.Name);
        cmd.Parameters.AddWithValue("$created", ToUnix(list.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", ToUnix(list.UpdatedAt));
        cmd.Parameters.AddWithValue("$completed", ToUnixNullable(list.CompletedAt));
        cmd.Parameters.AddWithValue("$order", list.SortOrder);
        cmd.ExecuteNonQuery();
    }

    public void DeleteList(Guid id)
    {
        using var cmd = Command("DELETE FROM crafting_lists WHERE id=$id");
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    private static CraftingList ReadList(SqliteDataReader r) =>
        new(
            Guid.Parse(r.GetString(0)),
            r.GetString(1),
            FromUnix(r.GetInt64(2)),
            FromUnix(r.GetInt64(3)),
            r.IsDBNull(4) ? null : FromUnix(r.GetInt64(4)),
            (int)r.GetInt64(5));

    // ── Recipes ───────────────────────────────────────────────────────────────

    public List<CraftingListRecipe> GetRecipesForList(Guid listId)
    {
        var result = new List<CraftingListRecipe>();
        using var cmd = Command(
            "SELECT id, list_id, recipe_id, item_id, quantity, added_at, sort_order FROM crafting_list_recipes WHERE list_id=$id ORDER BY sort_order, added_at");
        cmd.Parameters.AddWithValue("$id", listId.ToString());
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(ReadRecipe(r));
        return result;
    }

    public int CountRecipesForList(Guid listId)
    {
        using var cmd = Command(
            "SELECT COUNT(*) FROM crafting_list_recipes WHERE list_id=$id");
        cmd.Parameters.AddWithValue("$id", listId.ToString());
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void InsertRecipe(CraftingListRecipe recipe)
    {
        using var cmd = Command(
            "INSERT INTO crafting_list_recipes (id, list_id, recipe_id, item_id, quantity, added_at, sort_order) VALUES ($id, $list, $recipe, $item, $qty, $added, $order)");
        cmd.Parameters.AddWithValue("$id", recipe.Id.ToString());
        cmd.Parameters.AddWithValue("$list", recipe.ListId.ToString());
        cmd.Parameters.AddWithValue("$recipe", recipe.RecipeId);
        cmd.Parameters.AddWithValue("$item", recipe.ItemId);
        cmd.Parameters.AddWithValue("$qty", recipe.Quantity);
        cmd.Parameters.AddWithValue("$added", ToUnix(recipe.AddedAt));
        cmd.Parameters.AddWithValue("$order", recipe.SortOrder);
        cmd.ExecuteNonQuery();
    }

    public void UpdateRecipeQuantity(Guid recipeId, int quantity)
    {
        using var cmd = Command("UPDATE crafting_list_recipes SET quantity=$qty WHERE id=$id");
        cmd.Parameters.AddWithValue("$qty", quantity);
        cmd.Parameters.AddWithValue("$id", recipeId.ToString());
        cmd.ExecuteNonQuery();
    }

    public void DeleteRecipe(Guid recipeId)
    {
        using var cmd = Command("DELETE FROM crafting_list_recipes WHERE id=$id");
        cmd.Parameters.AddWithValue("$id", recipeId.ToString());
        cmd.ExecuteNonQuery();
    }

    public void DeleteRecipesForList(Guid listId)
    {
        using var cmd = Command("DELETE FROM crafting_list_recipes WHERE list_id=$id");
        cmd.Parameters.AddWithValue("$id", listId.ToString());
        cmd.ExecuteNonQuery();
    }

    public void MoveRecipe(Guid recipeId, Guid newListId)
    {
        using var cmd = Command("UPDATE crafting_list_recipes SET list_id=$newList WHERE id=$id");
        cmd.Parameters.AddWithValue("$newList", newListId.ToString());
        cmd.Parameters.AddWithValue("$id", recipeId.ToString());
        cmd.ExecuteNonQuery();
    }

    private static CraftingListRecipe ReadRecipe(SqliteDataReader r) =>
        new(
            Guid.Parse(r.GetString(0)),
            Guid.Parse(r.GetString(1)),
            (uint)r.GetInt64(2),
            (uint)r.GetInt64(3),
            (int)r.GetInt64(4),
            FromUnix(r.GetInt64(5)),
            (int)r.GetInt64(6));

    // ── Progress ──────────────────────────────────────────────────────────────

    public List<MaterialProgress> GetProgressForList(Guid listId)
    {
        var result = new List<MaterialProgress>();
        using var cmd = Command(
            "SELECT id, list_id, item_id, quantity_needed, quantity_collected, updated_at FROM material_progress WHERE list_id=$id");
        cmd.Parameters.AddWithValue("$id", listId.ToString());
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(ReadProgress(r));
        return result;
    }

    public void UpsertProgress(MaterialProgress progress)
    {
        using var cmd = Command(
            """
            INSERT INTO material_progress (id, list_id, item_id, quantity_needed, quantity_collected, updated_at)
            VALUES ($id, $list, $item, $needed, $collected, $updated)
            ON CONFLICT(list_id, item_id) DO UPDATE SET
                quantity_needed=$needed,
                quantity_collected=$collected,
                updated_at=$updated
            """);
        BindProgress(cmd, progress);
        cmd.ExecuteNonQuery();
    }

    public void UpsertProgressBatch(IEnumerable<MaterialProgress> progresses)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            foreach (var progress in progresses)
            {
                using var cmd = Command(
                    """
                    INSERT INTO material_progress (id, list_id, item_id, quantity_needed, quantity_collected, updated_at)
                    VALUES ($id, $list, $item, $needed, $collected, $updated)
                    ON CONFLICT(list_id, item_id) DO UPDATE SET
                        quantity_needed=$needed,
                        quantity_collected=$collected,
                        updated_at=$updated
                    """,
                    tx);
                BindProgress(cmd, progress);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    public void DeleteProgressForList(Guid listId)
    {
        using var cmd = Command("DELETE FROM material_progress WHERE list_id=$id");
        cmd.Parameters.AddWithValue("$id", listId.ToString());
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Moves the progress rows for the supplied item ids from one list to another.
    /// Existing destination rows for the same item are overwritten (UNIQUE constraint).
    /// </summary>
    public void MoveProgress(Guid sourceListId, Guid destListId, IReadOnlyList<uint> itemIds)
    {
        if (itemIds.Count == 0)
            return;

        using var tx = _db.BeginTransaction();
        try
        {
            foreach (var itemId in itemIds)
            {
                // Remove any conflicting destination row first.
                using (var del = Command("DELETE FROM material_progress WHERE list_id=$dst AND item_id=$item", tx))
                {
                    del.Parameters.AddWithValue("$dst", destListId.ToString());
                    del.Parameters.AddWithValue("$item", itemId);
                    del.ExecuteNonQuery();
                }
                using (var upd = Command("UPDATE material_progress SET list_id=$dst WHERE list_id=$src AND item_id=$item", tx))
                {
                    upd.Parameters.AddWithValue("$dst", destListId.ToString());
                    upd.Parameters.AddWithValue("$src", sourceListId.ToString());
                    upd.Parameters.AddWithValue("$item", itemId);
                    upd.ExecuteNonQuery();
                }
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    private static void BindProgress(SqliteCommand cmd, MaterialProgress progress)
    {
        cmd.Parameters.AddWithValue("$id", progress.Id.ToString());
        cmd.Parameters.AddWithValue("$list", progress.ListId.ToString());
        cmd.Parameters.AddWithValue("$item", progress.ItemId);
        cmd.Parameters.AddWithValue("$needed", progress.QuantityNeeded);
        cmd.Parameters.AddWithValue("$collected", progress.QuantityCollected);
        cmd.Parameters.AddWithValue("$updated", ToUnix(progress.UpdatedAt));
    }

    private static MaterialProgress ReadProgress(SqliteDataReader r) =>
        new(
            Guid.Parse(r.GetString(0)),
            Guid.Parse(r.GetString(1)),
            (uint)r.GetInt64(2),
            (int)r.GetInt64(3),
            (int)r.GetInt64(4),
            FromUnix(r.GetInt64(5)));

    // ── Market price cache ──────────────────────────────────────────────────────

    internal MarketScopePrice? GetCachedScopePrice(uint itemId, string worldOrDc)
    {
        using var cmd = Command(
            "SELECT price_per_unit, total_quantity, cheapest_server, cached_at " +
            "FROM market_price_cache WHERE item_id=$itemId AND world_or_dc=$wdc");
        cmd.Parameters.AddWithValue("$itemId", (long)itemId);
        cmd.Parameters.AddWithValue("$wdc", worldOrDc);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;
        return new MarketScopePrice(
            itemId,
            worldOrDc,
            r.GetInt32(0),
            r.GetString(2),
            r.GetInt32(1),
            DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(3)).UtcDateTime);
    }

    internal void SaveScopePrice(MarketScopePrice price)
    {
        using var cmd = Command("""
            INSERT INTO market_price_cache (item_id, world_or_dc, price_per_unit, total_quantity, cheapest_server, cached_at)
            VALUES ($itemId, $wdc, $price, $qty, $server, $at)
            ON CONFLICT(item_id, world_or_dc) DO UPDATE SET
                price_per_unit=$price, total_quantity=$qty, cheapest_server=$server, cached_at=$at
            """);
        cmd.Parameters.AddWithValue("$itemId", (long)price.ItemId);
        cmd.Parameters.AddWithValue("$wdc", price.Scope);
        cmd.Parameters.AddWithValue("$price", price.PricePerUnit);
        cmd.Parameters.AddWithValue("$qty", price.TotalAvailable);
        cmd.Parameters.AddWithValue("$server", price.ServerName);
        cmd.Parameters.AddWithValue("$at", ToUnix(price.CachedAt));
        cmd.ExecuteNonQuery();
    }

    // Transitional compatibility for MarketboardHelper until Tasks 2/3 migrate it to scope snapshots.
    public MarketPrice? GetCachedPrice(uint itemId, string worldOrDc)
    {
        var price = GetCachedScopePrice(itemId, worldOrDc);
        return price == null
            ? null
            : new MarketPrice(
                price.ItemId,
                price.PricePerUnit,
                price.PricePerUnit,
                price.ServerName,
                price.TotalAvailable,
                price.CachedAt);
    }

    // Transitional compatibility for MarketboardHelper until Tasks 2/3 migrate it to scope snapshots.
    public void SavePrice(uint itemId, string worldOrDc, int pricePerUnit, int totalQuantity, string cheapestServer) =>
        SaveScopePrice(new MarketScopePrice(
            itemId,
            worldOrDc,
            pricePerUnit,
            cheapestServer,
            totalQuantity,
            DateTime.UtcNow));

    // ── Transactions & helpers ──────────────────────────────────────────────────

    public SqliteTransaction BeginTransaction() => _db.BeginTransaction();

    private static long ToUnix(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static object ToUnixNullable(DateTime? dt) =>
        dt.HasValue ? ToUnix(dt.Value) : DBNull.Value;

    private static DateTime FromUnix(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private SqliteCommand Command(string sql, SqliteTransaction? tx = null)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        return cmd;
    }

    public void Dispose()
    {
        _db.Close();
        _db.Dispose();
    }
}
