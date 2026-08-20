using System;
using System.Collections.Generic;
using System.Linq;

namespace Artificer.Application.CraftingLists;

/// <summary>The final recipe sets for source and destination after a move, computed without I/O.</summary>
internal sealed record CraftingListMovePlan(
    IReadOnlyList<CraftingListRecipe> SourceRecipes,
    IReadOnlyList<CraftingListRecipe> DestinationRecipes);

/// <summary>
/// Pure planner for moving recipes between crafting lists: validates the selection, removes the moved
/// recipes from the source, and folds them into the destination — summing quantities when a RecipeId
/// already exists there (keeping the destination entry's Id) and otherwise re-homing the moved entry
/// (keeping its Id). No Lumina/SQLite dependency; inputs are never mutated.
/// </summary>
internal static class CraftingListMovePlanner
{
    public static CraftingListMovePlan Plan(
        Guid sourceListId,
        Guid destinationListId,
        IReadOnlyList<CraftingListRecipe> source,
        IReadOnlyList<CraftingListRecipe> destination,
        IReadOnlyList<Guid> selectedRecipeIds)
    {
        if (sourceListId == destinationListId)
            throw new ArgumentException("Source and destination lists must differ.", nameof(destinationListId));
        if (selectedRecipeIds.Count == 0)
            throw new ArgumentException("No recipes selected to move.", nameof(selectedRecipeIds));
        if (selectedRecipeIds.Distinct().Count() != selectedRecipeIds.Count)
            throw new ArgumentException("Selected recipe ids contain duplicates.", nameof(selectedRecipeIds));

        var sourceById = source.ToDictionary(r => r.Id);
        var moved = new List<CraftingListRecipe>(selectedRecipeIds.Count);
        foreach (var id in selectedRecipeIds)
        {
            if (!sourceById.TryGetValue(id, out var recipe))
                throw new ArgumentException($"Recipe {id} is not in the source list.", nameof(selectedRecipeIds));
            moved.Add(recipe);
        }

        var movedIds = selectedRecipeIds.ToHashSet();
        var remainingSource = source.Where(r => !movedIds.Contains(r.Id)).ToList();

        // Fold moved recipes into the destination, grouping by RecipeId so duplicates merge into one.
        var destByRecipeId = destination.ToDictionary(r => r.RecipeId);
        var addedQuantityByDestId = new Dictionary<Guid, int>();
        var appended = new List<CraftingListRecipe>();
        foreach (var group in moved.GroupBy(r => r.RecipeId))
        {
            var addQuantity = group.Sum(r => r.Quantity);
            if (destByRecipeId.TryGetValue(group.Key, out var existing))
                addedQuantityByDestId[existing.Id] = addQuantity;
            else
                appended.Add(group.First() with { ListId = destinationListId, Quantity = addQuantity });
        }

        var destinationRecipes = destination
            .Select(d => addedQuantityByDestId.TryGetValue(d.Id, out var add)
                ? d with { Quantity = d.Quantity + add }
                : d)
            .ToList();
        destinationRecipes.AddRange(appended);

        return new CraftingListMovePlan(remainingSource, destinationRecipes);
    }
}
