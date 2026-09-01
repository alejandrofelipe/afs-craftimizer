using System.Collections.Generic;
using Artificer.Plugin;

namespace Artificer.Application.Fishing;

public sealed record RequiredFish(uint ItemId, string Name, ushort IconId, int Quantity);
public sealed record CosmicFishingMission(ushort MissionId, string Name, IReadOnlyList<RequiredFish> Fish);

public static class CosmicFishingMissions
{
    private static ushort _cachedId;
    private static CosmicFishingMission? _cached;

    /// <summary>Resolve os peixes exigidos por uma missão cosmic. Cache pela última missão vista (inclui misses).</summary>
    public static CosmicFishingMission? Resolve(ushort missionUnitRowId)
    {
        if (missionUnitRowId == 0)
            return null;
        if (_cachedId == missionUnitRowId)
            return _cached;

        if (!LuminaSheets.WKSMissionUnitSheet.TryGetRow(missionUnitRowId, out var unit))
        {
            _cachedId = missionUnitRowId;
            return _cached = null;
        }

        // Gate: só é missão de Fisher. Sem isso, uma missão crafter residual (ex: troca de job
        // após o Update anterior) abriria a janela mostrando itens craftados como se fossem peixe.
        var isFishingMission = false;
        foreach (var categoryRef in unit.ClassJobCategory)   // até 2 links, per EXDSchema
        {
            if (categoryRef.ValueNullable is not { } category)
                continue;
            if (category.FSH)
            {
                isFishingMission = true;
                break;
            }
        }
        if (!isFishingMission)
        {
            _cachedId = missionUnitRowId;
            return _cached = null;
        }

        var fish = new List<RequiredFish>();
        var seen = new HashSet<uint>();
        foreach (var todoRef in unit.MissionToDo)         // até 3 ToDos (missões-sequência)
        {
            if (todoRef.ValueNullable is not { } todo)
                continue;
            for (var i = 0; i < todo.RequiredItem.Count; i++)
            {
                if (todo.RequiredItem[i].ValueNullable is not { } wksItem)
                    continue;
                if (wksItem.Item.ValueNullable is not { } item || item.RowId == 0)
                    continue;
                if (!seen.Add(item.RowId))                 // dedup entre fases
                    continue;
                var qty = i < todo.RequiredItemQuantity.Count ? todo.RequiredItemQuantity[i] : 0;
                fish.Add(new RequiredFish(item.RowId, item.Name.ExtractText(), item.Icon, qty));
            }
        }

        _cachedId = missionUnitRowId;
        return _cached = fish.Count == 0
            ? null
            : new CosmicFishingMission(missionUnitRowId, unit.Name.ExtractText(), fish);
    }
}
