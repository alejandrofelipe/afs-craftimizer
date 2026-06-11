using Artificer.Plugin;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Resolves the recipes of a crafting list into the flattened set of base materials,
/// crystals, and intermediate (pre-craft) items still required, after subtracting what
/// the player already owns.
/// </summary>
public sealed class IngredientResolver
{
    // itemId -> recipeId (first recipe found per item wins).
    private Dictionary<uint, uint>? _itemToRecipe;

    private Dictionary<uint, uint> ItemToRecipe => _itemToRecipe ??= BuildItemToRecipe();

    private static Dictionary<uint, uint> BuildItemToRecipe()
    {
        var map = new Dictionary<uint, uint>();
        foreach (var recipe in LuminaSheets.RecipeSheet)
        {
            var itemId = recipe.ItemResult.RowId;
            if (itemId == 0)
                continue;
            map.TryAdd(itemId, recipe.RowId);
        }
        return map;
    }

    /// <summary>Crystals, shards, and clusters occupy item ids 2..19.</summary>
    public static bool IsCrystal(uint itemId) => itemId is >= 2 and <= 19;

    private uint? FindRecipeForItem(uint itemId) =>
        ItemToRecipe.TryGetValue(itemId, out var recipeId) ? recipeId : null;

    public ResolvedIngredientTree ResolveAll(
        IReadOnlyList<CraftingListRecipe> recipes,
        IReadOnlyDictionary<uint, int> owned)
    {
        // Working pool of available items, consumed as we walk the trees.
        var available = new Dictionary<uint, int>();
        foreach (var (id, qty) in owned)
            available[id] = qty;

        var baseMaterials = new Dictionary<uint, int>();
        var crystals = new Dictionary<uint, int>();
        var preCrafts = new Dictionary<uint, int>();

        foreach (var recipe in recipes)
            WalkRecipe(recipe.RecipeId, recipe.Quantity, available, baseMaterials, crystals, preCrafts);

        return new ResolvedIngredientTree(
            BuildIngredients(baseMaterials, IngredientKind.BaseMaterial),
            BuildIngredients(crystals, IngredientKind.Crystal),
            BuildIngredients(preCrafts, IngredientKind.PreCraft));
    }

    /// <summary>
    /// Walks the ingredient tree for <paramref name="recipeId"/> producing <paramref name="resultQuantity"/>
    /// output items, accumulating deficits into the supplied dictionaries.
    /// </summary>
    private void WalkRecipe(
        uint recipeId,
        int resultQuantity,
        Dictionary<uint, int> available,
        Dictionary<uint, int> baseMaterials,
        Dictionary<uint, int> crystals,
        Dictionary<uint, int> preCrafts)
    {
        var recipe = LuminaSheets.RecipeSheet.GetRowOrDefault(recipeId);
        if (recipe is not { } row)
            return;

        var amountResult = Math.Max(1, (int)row.AmountResult);
        // Number of times the recipe must be crafted to yield resultQuantity items.
        var crafts = (int)Math.Ceiling((double)resultQuantity / amountResult);

        var ingredients = row.Ingredient.Zip(row.AmountIngredient)
            .Take(6)
            .Where(i => i.First.IsValid);

        foreach (var (itemRef, amount) in ingredients)
        {
            var itemId = itemRef.RowId;
            var needed = amount * crafts;
            if (needed <= 0)
                continue;

            // Consume what we already have first.
            var have = available.GetValueOrDefault(itemId);
            var consumed = Math.Min(have, needed);
            if (consumed > 0)
                available[itemId] = have - consumed;

            var deficit = needed - consumed;
            if (deficit == 0)
                continue;

            if (IsCrystal(itemId))
            {
                crystals[itemId] = crystals.GetValueOrDefault(itemId) + deficit;
                continue;
            }

            var subRecipeId = FindRecipeForItem(itemId);
            if (subRecipeId is { } subId)
            {
                preCrafts[itemId] = preCrafts.GetValueOrDefault(itemId) + deficit;
                WalkRecipe(subId, deficit, available, baseMaterials, crystals, preCrafts);
            }
            else
            {
                baseMaterials[itemId] = baseMaterials.GetValueOrDefault(itemId) + deficit;
            }
        }
    }

    private List<ResolvedIngredient> BuildIngredients(Dictionary<uint, int> source, IngredientKind kind)
    {
        var result = new List<ResolvedIngredient>(source.Count);
        foreach (var (itemId, quantity) in source)
        {
            var name = GetItemName(itemId);
            var recipeId = kind == IngredientKind.PreCraft ? FindRecipeForItem(itemId) : null;
            result.Add(new ResolvedIngredient(
                itemId,
                name,
                quantity,
                kind,
                Array.Empty<GatheringLocation>(),
                recipeId));
        }
        return result;
    }

    private static string GetItemName(uint itemId) =>
        LuminaSheets.ItemSheet.GetRowOrDefault(itemId)?.Name.ExtractText() ?? string.Empty;
}
