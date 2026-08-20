using System;
using System.Collections.Generic;
using Artificer.Application.CraftingLists;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class CraftingListMovePlannerTests
{
    private static readonly Guid Src = Guid.NewGuid();
    private static readonly Guid Dst = Guid.NewGuid();

    private static CraftingListRecipe Recipe(Guid listId, uint recipeId, int qty, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), listId, recipeId, recipeId + 1000, qty, DateTime.UnixEpoch, 0);

    [TestMethod]
    public void Plan_NoCollision_MovesRecipeKeepingIdAndQuantity()
    {
        var r = Recipe(Src, recipeId: 10, qty: 3);
        var plan = CraftingListMovePlanner.Plan(Src, Dst, [r], [], [r.Id]);

        Assert.AreEqual(0, plan.SourceRecipes.Count);
        Assert.AreEqual(1, plan.DestinationRecipes.Count);
        var moved = plan.DestinationRecipes[0];
        Assert.AreEqual(r.Id, moved.Id);
        Assert.AreEqual(Dst, moved.ListId);
        Assert.AreEqual(3, moved.Quantity);
        Assert.AreEqual(10u, moved.RecipeId);
    }

    [TestMethod]
    public void Plan_CollisionInDestination_SumsQuantityKeepingDestinationId()
    {
        var src = Recipe(Src, recipeId: 10, qty: 3);
        var dst = Recipe(Dst, recipeId: 10, qty: 5);
        var plan = CraftingListMovePlanner.Plan(Src, Dst, [src], [dst], [src.Id]);

        Assert.AreEqual(0, plan.SourceRecipes.Count);
        Assert.AreEqual(1, plan.DestinationRecipes.Count);
        var merged = plan.DestinationRecipes[0];
        Assert.AreEqual(dst.Id, merged.Id);   // conserva o Id do destino
        Assert.AreEqual(8, merged.Quantity);  // 5 + 3
        Assert.AreEqual(Dst, merged.ListId);
    }

    [TestMethod]
    public void Plan_PartialMove_LeavesUnselectedInSource()
    {
        var keep = Recipe(Src, recipeId: 10, qty: 1);
        var move = Recipe(Src, recipeId: 20, qty: 2);
        var plan = CraftingListMovePlanner.Plan(Src, Dst, [keep, move], [], [move.Id]);

        Assert.AreEqual(1, plan.SourceRecipes.Count);
        Assert.AreEqual(keep.Id, plan.SourceRecipes[0].Id);
        Assert.AreEqual(1, plan.DestinationRecipes.Count);
        Assert.AreEqual(move.Id, plan.DestinationRecipes[0].Id);
    }

    [TestMethod]
    public void Plan_MissingId_Throws()
    {
        var r = Recipe(Src, 10, 1);
        Assert.ThrowsException<ArgumentException>(() =>
            CraftingListMovePlanner.Plan(Src, Dst, [r], [], [Guid.NewGuid()]));
    }

    [TestMethod]
    public void Plan_DuplicateId_Throws()
    {
        var r = Recipe(Src, 10, 1);
        Assert.ThrowsException<ArgumentException>(() =>
            CraftingListMovePlanner.Plan(Src, Dst, [r], [], [r.Id, r.Id]));
    }

    [TestMethod]
    public void Plan_SameLists_Throws()
    {
        var r = Recipe(Src, 10, 1);
        Assert.ThrowsException<ArgumentException>(() =>
            CraftingListMovePlanner.Plan(Src, Src, [r], [], [r.Id]));
    }

    [TestMethod]
    public void Plan_EmptySelection_Throws() =>
        Assert.ThrowsException<ArgumentException>(() =>
            CraftingListMovePlanner.Plan(Src, Dst, [], [], []));

    [TestMethod]
    public void Plan_DoesNotMutateInputs()
    {
        var src = Recipe(Src, 10, 3);
        var dst = Recipe(Dst, 10, 5);
        var source = new List<CraftingListRecipe> { src };
        var destination = new List<CraftingListRecipe> { dst };
        var ids = new List<Guid> { src.Id };

        CraftingListMovePlanner.Plan(Src, Dst, source, destination, ids);

        Assert.AreEqual(1, source.Count);
        Assert.AreEqual(1, destination.Count);
        Assert.AreEqual(5, destination[0].Quantity);  // destino de entrada inalterado
        Assert.AreEqual(1, ids.Count);
    }

    [TestMethod]
    public void Plan_DestinationHasDuplicateRecipeId_MergesWithFirstWithoutThrowing()
    {
        var src = Recipe(Src, recipeId: 10, qty: 3);
        var dup1 = Recipe(Dst, recipeId: 10, qty: 5);
        var dup2 = Recipe(Dst, recipeId: 10, qty: 1);  // destino com RecipeId duplicado (dado legado)

        var plan = CraftingListMovePlanner.Plan(Src, Dst, [src], [dup1, dup2], [src.Id]);

        Assert.AreEqual(0, plan.SourceRecipes.Count);
        Assert.AreEqual(2, plan.DestinationRecipes.Count);
        Assert.AreEqual(dup1.Id, plan.DestinationRecipes[0].Id);
        Assert.AreEqual(8, plan.DestinationRecipes[0].Quantity);  // soma no primeiro (5+3)
        Assert.AreEqual(dup2.Id, plan.DestinationRecipes[1].Id);
        Assert.AreEqual(1, plan.DestinationRecipes[1].Quantity);  // segundo inalterado
    }
}
