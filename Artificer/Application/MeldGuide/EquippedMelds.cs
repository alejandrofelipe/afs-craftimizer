using System.Collections.Generic;
using Artificer.Plugin;
using Artificer.Utils;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Artificer.Application.MeldGuide;

public static class EquippedMelds
{
    public readonly record struct EquippedPiece(uint ItemId, IReadOnlyList<(CraftStat Stat, int Grade)> Melds);

    /// <summary>
    /// Ordem dos slots no container InventoryType.EquippedItems, que segue
    /// RaptureGearsetModule.GearsetItemIndex (MainHand=0 .. SoulStone=13).
    /// Belt (5) e SoulCrystal (13) não têm EquipSlot correspondente (não meldáveis) → null.
    /// </summary>
    private static readonly EquipSlot?[] SlotOrder =
    [
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head, EquipSlot.Body,
        EquipSlot.Hands, null /* Belt */, EquipSlot.Legs, EquipSlot.Feet,
        EquipSlot.Ears, EquipSlot.Neck, EquipSlot.Wrists, EquipSlot.Ring1, EquipSlot.Ring2,
        null /* SoulCrystal */,
    ];

    public static unsafe IReadOnlyDictionary<EquipSlot, EquippedPiece> Read()
    {
        var result = new Dictionary<EquipSlot, EquippedPiece>();
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return result;

        var items = Gearsets.GetGearsetItems(container);
        for (var i = 0; i < items.Length && i < SlotOrder.Length; i++)
        {
            if (SlotOrder[i] is not { } slot)
                continue;

            var it = items[i];
            if (it.ItemId == 0)
                continue;

            var melds = new List<(CraftStat, int)>();
            foreach (var m in it.Materia)
            {
                if (m.Type == 0)
                    continue;
                if (LuminaSheets.MateriaSheet.GetRowOrDefault(m.Type) is not { } materia)
                    continue;

                CraftStat? stat = materia.BaseParam.RowId switch
                {
                    Gearsets.ParamCraftsmanship => CraftStat.Craftsmanship,
                    Gearsets.ParamControl       => CraftStat.Control,
                    Gearsets.ParamCP            => CraftStat.CP,
                    _                           => null,
                };
                if (stat is { } s)
                    melds.Add((s, m.Grade + 1)); // 0-based no jogo → 1-based
            }
            result[slot] = new EquippedPiece(it.ItemId, melds);
        }
        return result;
    }
}
