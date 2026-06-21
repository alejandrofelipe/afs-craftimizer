using Artificer.Application.CraftingLists;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class GatheringRoutePlannerTests
{
    private static GatheringLocation Loc(uint territory, string zone) =>
        new(territory, MapId: territory + 100, ZoneName: zone, MapX: 21f, MapY: 18f,
            NearestAetheryteId: territory + 200, NearestAetheryteName: zone + " Aetheryte",
            Kind: GatheringKind.Mining);

    private static RouteMaterialInput Mat(uint id, string name, int needed, int collected,
        GatheringLocation? loc, int? price) =>
        new(id, name, needed, collected, loc, price);

    [TestMethod]
    public void GroupsByZone_OrderedByMaterialCountDesc()
    {
        var zoneA = Loc(1, "Alpha");
        var zoneB = Loc(2, "Beta");
        var inputs = new List<RouteMaterialInput>
        {
            Mat(10, "a1", 5, 0, zoneA, null),
            Mat(11, "b1", 5, 0, zoneB, null),
            Mat(12, "b2", 5, 0, zoneB, null),
        };

        var route = GatheringRoutePlanner.Plan(inputs);

        Assert.AreEqual(2, route.Zones.Count);
        Assert.AreEqual(2u, route.Zones[0].TerritoryId); // Beta primeiro (2 materiais)
        Assert.AreEqual(1u, route.Zones[1].TerritoryId);
        Assert.AreEqual(2, route.Zones[0].Materials.Count);
    }

    [TestMethod]
    public void MaterialsWithoutLocation_GoToOthers()
    {
        var inputs = new List<RouteMaterialInput>
        {
            Mat(10, "gather", 5, 0, Loc(1, "Alpha"), null),
            Mat(20, "vendor", 3, 0, null, 100),
        };

        var route = GatheringRoutePlanner.Plan(inputs);

        Assert.AreEqual(1, route.Zones.Count);
        Assert.AreEqual(1, route.Others.Count);
        Assert.AreEqual(20u, route.Others[0].ItemId);
    }

    [TestMethod]
    public void TotalBuyCost_SumsMissingTimesUnitPrice_IgnoringNullAndZero()
    {
        var inputs = new List<RouteMaterialInput>
        {
            Mat(10, "priced", 10, 2, Loc(1, "Alpha"), 50),  // missing 8 × 50 = 400
            Mat(11, "noprice", 5, 0, Loc(1, "Alpha"), null), // ignorado
            Mat(12, "zero", 5, 0, null, 0),                  // ignorado
        };

        var route = GatheringRoutePlanner.Plan(inputs);

        Assert.AreEqual(400, route.TotalBuyCost);
    }

    [TestMethod]
    public void ExcludesFullyCollected()
    {
        var inputs = new List<RouteMaterialInput>
        {
            Mat(10, "done", 5, 5, Loc(1, "Alpha"), null),
            Mat(11, "left", 5, 1, Loc(1, "Alpha"), null),
        };

        var route = GatheringRoutePlanner.Plan(inputs);

        Assert.AreEqual(1, route.MissingMaterialCount);
        Assert.AreEqual(1, route.Zones[0].Materials.Count);
        Assert.AreEqual(4, route.Zones[0].Materials[0].Missing);
    }

    [TestMethod]
    public void EmptyInput_ReturnsEmptyRoute()
    {
        var route = GatheringRoutePlanner.Plan(new List<RouteMaterialInput>());

        Assert.AreEqual(0, route.Zones.Count);
        Assert.AreEqual(0, route.Others.Count);
        Assert.AreEqual(0, route.TotalBuyCost);
        Assert.AreEqual(0, route.MissingMaterialCount);
    }

    [TestMethod]
    public void ZonesWithEqualMaterialCount_OrderedByNameAsc()
    {
        var inputs = new List<RouteMaterialInput>
        {
            Mat(10, "z1", 5, 0, Loc(2, "Zeta"), null),
            Mat(11, "a1", 5, 0, Loc(1, "Alpha"), null),
        };

        var route = GatheringRoutePlanner.Plan(inputs);

        Assert.AreEqual("Alpha", route.Zones[0].ZoneName);
        Assert.AreEqual("Zeta", route.Zones[1].ZoneName);
    }
}
