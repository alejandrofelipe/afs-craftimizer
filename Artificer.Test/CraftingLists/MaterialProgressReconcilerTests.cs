using System;
using System.Collections.Generic;
using System.Linq;
using Artificer.Application.CraftingLists;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class MaterialProgressReconcilerTests
{
    private static readonly Guid List = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UnixEpoch;

    private static ResolvedIngredient Ing(uint itemId, int qty, IngredientKind kind = IngredientKind.BaseMaterial) =>
        new(itemId, $"item{itemId}", qty, kind, [], kind == IngredientKind.PreCraft ? 999u : null);

    private static ResolvedIngredientTree Tree(
        IReadOnlyList<ResolvedIngredient>? bases = null,
        IReadOnlyList<ResolvedIngredient>? crystals = null,
        IReadOnlyList<ResolvedIngredient>? preCrafts = null) =>
        new(bases ?? [], crystals ?? [], preCrafts ?? []);

    private static InventorySnapshot Snapshot(
        Dictionary<uint, int>? bags = null,
        Dictionary<uint, int>? crystals = null,
        Dictionary<uint, int>? saddle = null,
        IReadOnlyList<RetainerSnapshot>? retainers = null) =>
        new(bags ?? [], crystals ?? [], saddle ?? [], retainers ?? []);

    [TestMethod]
    public void Reconcile_QuantityNeeded_ComesFromTree()
    {
        var result = MaterialProgressReconciler.Reconcile(
            List, Tree(bases: [Ing(100, 7)]), InventorySnapshot.Empty, false, [], Now);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(100u, result[0].ItemId);
        Assert.AreEqual(7, result[0].QuantityNeeded);
        Assert.AreEqual(0, result[0].QuantityCollected);
        Assert.AreEqual(List, result[0].ListId);
    }

    [TestMethod]
    public void Reconcile_SharedItemAcrossCategories_GroupsAndSumsNeed()
    {
        // Mesmo ItemId como material base E ingrediente de pre-craft → agrupado, necessidades somadas.
        var tree = Tree(bases: [Ing(100, 3)], preCrafts: [Ing(100, 5, IngredientKind.PreCraft)]);
        var result = MaterialProgressReconciler.Reconcile(List, tree, InventorySnapshot.Empty, false, [], Now);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(100u, result[0].ItemId);
        Assert.AreEqual(8, result[0].QuantityNeeded);  // 3 + 5
    }

    [TestMethod]
    public void Reconcile_ExistingId_IsPreserved()
    {
        var existingId = Guid.NewGuid();
        var existing = new List<MaterialProgress> { new(existingId, List, 100, 999, 999, DateTime.UnixEpoch) };
        var result = MaterialProgressReconciler.Reconcile(
            List, Tree(bases: [Ing(100, 4)]), InventorySnapshot.Empty, false, existing, Now);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(existingId, result[0].Id);     // Id preservado
        Assert.AreEqual(4, result[0].QuantityNeeded);  // mas need recalculado pela árvore
    }

    [TestMethod]
    public void Reconcile_RemovedIngredient_NotInOutput()
    {
        // existing tem item 200, mas a árvore só tem 100 → 200 (órfão) é descartado.
        var existing = new List<MaterialProgress> { new(Guid.NewGuid(), List, 200, 5, 5, DateTime.UnixEpoch) };
        var result = MaterialProgressReconciler.Reconcile(
            List, Tree(bases: [Ing(100, 2)]), InventorySnapshot.Empty, false, existing, Now);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(100u, result[0].ItemId);
        Assert.IsFalse(result.Any(p => p.ItemId == 200));
    }

    [TestMethod]
    public void Reconcile_CollectedWithoutRetainers_UsesBagsCrystalsSaddleOnly()
    {
        var snapshot = Snapshot(
            bags: new() { [100] = 2 },
            crystals: new() { [100] = 1 },
            saddle: new() { [100] = 1 },
            retainers: [new RetainerSnapshot("R", new Dictionary<uint, int> { [100] = 50 }, Loaded: true)]);

        var result = MaterialProgressReconciler.Reconcile(
            List, Tree(bases: [Ing(100, 10)]), snapshot, includeRetainers: false, [], Now);
        Assert.AreEqual(4, result[0].QuantityCollected);  // 2+1+1; retainer ignorado
    }

    [TestMethod]
    public void Reconcile_CollectedWithRetainers_IncludesLoadedRetainersOnly()
    {
        var snapshot = Snapshot(
            bags: new() { [100] = 2 },
            retainers:
            [
                new RetainerSnapshot("Loaded", new Dictionary<uint, int> { [100] = 3 }, Loaded: true),
                new RetainerSnapshot("Unloaded", new Dictionary<uint, int> { [100] = 100 }, Loaded: false),
            ]);

        var result = MaterialProgressReconciler.Reconcile(
            List, Tree(bases: [Ing(100, 50)]), snapshot, includeRetainers: true, [], Now);
        Assert.AreEqual(5, result[0].QuantityCollected);  // 2 + 3 (loaded); unloaded ignorado
    }

    [TestMethod]
    public void Reconcile_Collected_CappedAtNeeded()
    {
        var snapshot = Snapshot(bags: new() { [100] = 999 });
        var result = MaterialProgressReconciler.Reconcile(
            List, Tree(bases: [Ing(100, 4)]), snapshot, false, [], Now);

        Assert.AreEqual(4, result[0].QuantityCollected);  // limitado a needed
        Assert.AreEqual(4, result[0].QuantityNeeded);
    }
}
