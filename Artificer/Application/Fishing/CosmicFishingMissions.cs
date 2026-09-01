using System.Collections.Generic;
using Artificer.Plugin;

namespace Artificer.Application.Fishing;

public sealed record RequiredFish(uint ItemId, string Name, ushort IconId, int Quantity);
public sealed record CosmicFishingMission(ushort MissionId, string Name, IReadOnlyList<RequiredFish> Fish);

public static class CosmicFishingMissions
{
    private static CosmicFishingMission? _cached;

    /// <summary>Resolve os peixes exigidos por uma missão cosmic. Cache pela última missão vista.</summary>
    public static CosmicFishingMission? Resolve(ushort missionUnitRowId)
    {
        if (missionUnitRowId == 0)
            return null;
        if (_cached?.MissionId == missionUnitRowId)
            return _cached;

        if (!LuminaSheets.WKSMissionUnitSheet.TryGetRow(missionUnitRowId, out var unit))
            return _cached = null;

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

        return _cached = fish.Count == 0
            ? null
            : new CosmicFishingMission(missionUnitRowId, unit.Name.ExtractText(), fish);
    }
}
