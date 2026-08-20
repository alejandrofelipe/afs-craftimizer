using System;
using System.Collections.Generic;
using System.Linq;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Pure reconciliation of a list's material progress against its resolved ingredient tree and a
/// single inventory snapshot. Produces exactly one <see cref="MaterialProgress"/> per distinct
/// ingredient ItemId in the tree (needs summed across base/crystal/pre-craft categories), preserving
/// an existing row's Id when the item is still needed and dropping rows for items no longer in the
/// tree (orphans). "Collected" follows the same contract as inventory sync: bags + crystals + saddle,
/// plus loaded retainers when included, capped at the needed quantity.
/// </summary>
internal static class MaterialProgressReconciler
{
    public static IReadOnlyList<MaterialProgress> Reconcile(
        Guid listId,
        ResolvedIngredientTree tree,
        InventorySnapshot snapshot,
        bool includeRetainers,
        IReadOnlyList<MaterialProgress> existing,
        DateTime updatedAt)
    {
        var existingIdByItem = existing
            .GroupBy(p => p.ItemId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var result = new List<MaterialProgress>();
        foreach (var group in tree.BaseMaterials.Concat(tree.Crystals).Concat(tree.PreCrafts)
                     .GroupBy(i => i.ItemId))
        {
            var itemId = group.Key;
            var needed = group.Sum(i => i.Quantity);

            var owned = includeRetainers
                ? snapshot.GetTotal(itemId)
                : snapshot.MainBags.GetValueOrDefault(itemId)
                  + snapshot.Crystals.GetValueOrDefault(itemId)
                  + snapshot.SaddleBag.GetValueOrDefault(itemId);

            var id = existingIdByItem.TryGetValue(itemId, out var existingId) ? existingId : Guid.NewGuid();
            result.Add(new MaterialProgress(id, listId, itemId, needed, Math.Min(owned, needed), updatedAt));
        }
        return result;
    }
}
