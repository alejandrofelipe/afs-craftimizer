using System;
using System.Collections.Generic;
using System.Linq;

namespace Craftimizer.Application.CraftingLists;

/// <summary>A user-defined crafting list (a named collection of recipes to craft).</summary>
public sealed record CraftingList(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    int SortOrder);

/// <summary>A recipe entry within a crafting list, with the desired output quantity.</summary>
public sealed record CraftingListRecipe(
    Guid Id,
    Guid ListId,
    uint RecipeId,
    uint ItemId,
    int Quantity,
    DateTime AddedAt,
    int SortOrder);

/// <summary>Tracks collection progress for a single material needed by a list.</summary>
public sealed record MaterialProgress(
    Guid Id,
    Guid ListId,
    uint ItemId,
    int QuantityNeeded,
    int QuantityCollected,
    DateTime UpdatedAt)
{
    public bool IsComplete => QuantityCollected >= QuantityNeeded;

    public float Fraction =>
        QuantityNeeded > 0 ? Math.Clamp((float)QuantityCollected / QuantityNeeded, 0f, 1f) : 1f;
}

/// <summary>The category an ingredient falls into when resolving a crafting list.</summary>
public enum IngredientKind
{
    BaseMaterial,
    PreCraft,
    Crystal
}

/// <summary>A gathering location for a base material (populated by GatheringLocator).</summary>
public sealed record GatheringLocation(
    uint TerritoryTypeId,
    string ZoneName,
    float MapX,
    float MapY,
    uint NearestAetheryteId,
    string NearestAetheryteName,
    GatheringKind Kind);

/// <summary>The kind of gathering node (or other acquisition method) for a material.</summary>
public enum GatheringKind { Mining, Botany, Fishing, Mob, Vendor, Unknown }

/// <summary>A marketboard price snapshot for an item, persisted in the price cache.</summary>
public sealed record MarketPrice(
    uint ItemId,
    int PriceCurrentServer,
    int PriceCheapestServer,
    string CheapestServerName,
    int TotalAvailable,
    DateTime CachedAt)
{
    public bool IsStale(int ttlMinutes) =>
        DateTime.UtcNow - CachedAt > TimeSpan.FromMinutes(ttlMinutes);
}

/// <summary>A fully resolved ingredient: what it is, how much is needed, and where to get it.</summary>
public sealed record ResolvedIngredient(
    uint ItemId,
    string ItemName,
    int Quantity,
    IngredientKind Kind,
    IReadOnlyList<GatheringLocation> GatheringLocations,
    uint? PreCraftRecipeId);

/// <summary>A node in the pre-craft dependency tree.</summary>
public sealed record PreCraftNode(
    uint ItemId,
    int QuantityNeeded,
    uint RecipeId);

/// <summary>The result of resolving every recipe in a list into its deficit ingredients.</summary>
public sealed record ResolvedIngredientTree(
    IReadOnlyList<ResolvedIngredient> BaseMaterials,
    IReadOnlyList<ResolvedIngredient> Crystals,
    IReadOnlyList<ResolvedIngredient> PreCrafts);

/// <summary>A point-in-time snapshot of a retainer's inventory.</summary>
public sealed record RetainerSnapshot(
    string RetainerName,
    IReadOnlyDictionary<uint, int> Items,
    bool Loaded);

/// <summary>A point-in-time snapshot of all the player's item sources.</summary>
public sealed record InventorySnapshot(
    IReadOnlyDictionary<uint, int> MainBags,
    IReadOnlyDictionary<uint, int> Crystals,
    IReadOnlyDictionary<uint, int> SaddleBag,
    IReadOnlyList<RetainerSnapshot> Retainers)
{
    public static InventorySnapshot Empty { get; } = new(
        new Dictionary<uint, int>(),
        new Dictionary<uint, int>(),
        new Dictionary<uint, int>(),
        Array.Empty<RetainerSnapshot>());

    /// <summary>Sum of an item across every source (only retainers that are loaded).</summary>
    public int GetTotal(uint itemId)
    {
        var total = 0;
        total += MainBags.GetValueOrDefault(itemId);
        total += Crystals.GetValueOrDefault(itemId);
        total += SaddleBag.GetValueOrDefault(itemId);
        foreach (var retainer in Retainers)
        {
            if (retainer.Loaded)
                total += retainer.Items.GetValueOrDefault(itemId);
        }
        return total;
    }
}
