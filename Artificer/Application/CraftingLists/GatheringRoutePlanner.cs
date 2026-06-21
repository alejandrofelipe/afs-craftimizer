using System;
using System.Collections.Generic;
using System.Linq;

namespace Artificer.Application.CraftingLists;

/// <summary>Entrada para o planejador: um material faltante com sua location representativa e preço.</summary>
public sealed record RouteMaterialInput(
    uint ItemId, string ItemName, int Needed, int Collected,
    GatheringLocation? Location, int? UnitPrice);

/// <summary>Um material dentro da rota (quantidade que falta).</summary>
public sealed record RouteMaterial(
    uint ItemId, string ItemName, int Missing,
    GatheringLocation? Location, int? UnitPrice);

/// <summary>Uma zona da rota com seus materiais.</summary>
public sealed record GatheringRouteZone(
    uint TerritoryId, string ZoneName, uint AetheryteId, string AetheryteName,
    IReadOnlyList<RouteMaterial> Materials);

/// <summary>
/// Plano de rota: zonas ordenadas + grupo "Outros" + custo total de compra.
/// <see cref="MissingMaterialCount"/> conta TODOS os materiais faltantes (zonas + Outros).
/// </summary>
public sealed record GatheringRoute(
    IReadOnlyList<GatheringRouteZone> Zones,
    IReadOnlyList<RouteMaterial> Others,
    long TotalBuyCost,
    int MissingMaterialCount,
    int ZoneCount);

/// <summary>Função pura: agrupa materiais faltantes por zona, ordena e soma o custo de compra.</summary>
public static class GatheringRoutePlanner
{
    public static GatheringRoute Plan(IReadOnlyList<RouteMaterialInput> inputs)
    {
        static bool HasZone(RouteMaterialInput m) => m.Location is { TerritoryTypeId: not 0 };

        var missing = inputs.Where(m => m.Needed - m.Collected > 0).ToList();

        var zones = missing
            .Where(HasZone)
            .GroupBy(m => m.Location!.TerritoryTypeId)
            .Select(g =>
            {
                var loc = g.First().Location!;
                return new GatheringRouteZone(
                    loc.TerritoryTypeId, loc.ZoneName,
                    loc.NearestAetheryteId, loc.NearestAetheryteName,
                    g.Select(ToRouteMaterial).ToList());
            })
            .OrderByDescending(z => z.Materials.Count)
            .ThenBy(z => z.ZoneName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var others = missing
            .Where(m => !HasZone(m))
            .Select(ToRouteMaterial)
            .ToList();

        var totalBuyCost = missing
            .Where(m => m.UnitPrice is > 0)
            .Sum(m => (long)(m.Needed - m.Collected) * m.UnitPrice!.Value);

        return new GatheringRoute(zones, others, totalBuyCost, missing.Count, zones.Count);
    }

    private static RouteMaterial ToRouteMaterial(RouteMaterialInput m) =>
        new(m.ItemId, m.ItemName, m.Needed - m.Collected, m.Location, m.UnitPrice);
}
