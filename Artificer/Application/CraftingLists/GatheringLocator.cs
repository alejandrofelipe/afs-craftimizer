using Artificer.Plugin;
using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Resolves the in-world gathering locations (mining/botany/fishing nodes) for a given
/// item id by walking the gathering point sheets, with per-item caching.
/// </summary>
public sealed class GatheringLocator
{
    private readonly Dictionary<uint, IReadOnlyList<GatheringLocation>> _cache = new();

    public IReadOnlyList<GatheringLocation> GetLocations(uint itemId)
    {
        if (_cache.TryGetValue(itemId, out var cached))
            return cached;
        var result = Resolve(itemId);
        _cache[itemId] = result;
        return result;
    }

    private static List<GatheringLocation> Resolve(uint itemId)
    {
        var results = new List<GatheringLocation>();
        var seenTerritories = new HashSet<uint>();

        // GatheringPointBase.Item is a collection of RowRef -> GatheringItem;
        // GatheringItem.Item is a RowRef whose RowId is the actual Item id.
        foreach (var gpb in LuminaSheets.GatheringPointBaseSheet)
        {
            var matches = false;
            foreach (var giRef in gpb.Item)
            {
                if (giRef.RowId == 0)
                    continue;
                if (giRef.GetValueOrDefault<GatheringItem>() is not { } gi)
                    continue;
                if (gi.Item.RowId == itemId)
                {
                    matches = true;
                    break;
                }
            }
            if (!matches)
                continue;

            // Find a GatheringPoint that references this base to get the TerritoryType.
            GatheringPoint? foundGp = null;
            foreach (var gp in LuminaSheets.GatheringPointSheet)
            {
                if (gp.GatheringPointBase.RowId == gpb.RowId)
                {
                    foundGp = gp;
                    break;
                }
            }
            if (foundGp is not { } gpVal)
                continue;

            if (gpVal.TerritoryType.ValueNullable is not { } territory || territory.RowId == 0)
                continue;
            if (!seenTerritories.Add(territory.RowId))
                continue;

            var aetheryte = territory.Aetheryte.ValueNullable;
            var egp = LuminaSheets.ExportedGatheringPointSheet.GetRowOrDefault(gpb.RowId);
            results.Add(new GatheringLocation(
                TerritoryTypeId: territory.RowId,
                MapId: territory.Map.ValueNullable?.RowId ?? 0,
                ZoneName: territory.PlaceName.ValueNullable?.Name.ExtractText() ?? "?",
                MapX: egp?.X ?? 0f,
                MapY: egp?.Y ?? 0f,
                NearestAetheryteId: aetheryte?.RowId ?? 0,
                NearestAetheryteName: aetheryte?.PlaceName.ValueNullable?.Name.ExtractText() ?? "?",
                Kind: GetKind(gpb.GatheringType.RowId)
            ));
        }
        return results;
    }

    private static GatheringKind GetKind(uint gatheringTypeId) => gatheringTypeId switch
    {
        0 or 1 => GatheringKind.Mining,
        2 or 3 => GatheringKind.Botany,
        4 => GatheringKind.Fishing,
        _ => GatheringKind.Unknown
    };
}
