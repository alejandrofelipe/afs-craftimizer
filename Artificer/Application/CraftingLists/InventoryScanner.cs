using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Reads the player's live in-game inventory: main bags, crystals, saddlebag, and retainers.
/// </summary>
public static unsafe class InventoryScanner
{
    private const int RetainerCount = 10;

    public static InventorySnapshot Read()
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
            return InventorySnapshot.Empty;

        var mainBags = new Dictionary<uint, int>();
        ReadContainer(manager, InventoryType.Inventory1, mainBags);
        ReadContainer(manager, InventoryType.Inventory2, mainBags);
        ReadContainer(manager, InventoryType.Inventory3, mainBags);
        ReadContainer(manager, InventoryType.Inventory4, mainBags);

        var crystals = new Dictionary<uint, int>();
        ReadContainer(manager, InventoryType.Crystals, crystals);

        var saddleBag = new Dictionary<uint, int>();
        ReadContainer(manager, InventoryType.SaddleBag1, saddleBag);
        ReadContainer(manager, InventoryType.SaddleBag2, saddleBag);
        ReadContainer(manager, InventoryType.PremiumSaddleBag1, saddleBag);
        ReadContainer(manager, InventoryType.PremiumSaddleBag2, saddleBag);

        var retainers = ReadRetainers(manager);

        return new InventorySnapshot(mainBags, crystals, saddleBag, retainers);
    }

    private static List<RetainerSnapshot> ReadRetainers(InventoryManager* manager)
    {
        var result = new List<RetainerSnapshot>();

        var retainerManager = RetainerManager.Instance();
        if (retainerManager == null)
            return result;

        for (var i = 0u; i < RetainerCount; i++)
        {
            var retainer = retainerManager->GetRetainerBySortedIndex(i);
            if (retainer == null || retainer->RetainerId == 0)
                continue;

            var name = retainer->NameString;

            if (!retainer->Available)
            {
                result.Add(new RetainerSnapshot(name, new Dictionary<uint, int>(), Loaded: false));
                continue;
            }

            var items = new Dictionary<uint, int>();
            ReadContainer(manager, InventoryType.RetainerPage1, items);
            ReadContainer(manager, InventoryType.RetainerPage2, items);
            ReadContainer(manager, InventoryType.RetainerPage3, items);
            ReadContainer(manager, InventoryType.RetainerPage4, items);
            ReadContainer(manager, InventoryType.RetainerPage5, items);
            ReadContainer(manager, InventoryType.RetainerPage6, items);
            ReadContainer(manager, InventoryType.RetainerPage7, items);
            ReadContainer(manager, InventoryType.RetainerCrystals, items);

            result.Add(new RetainerSnapshot(name, items, Loaded: true));
        }

        return result;
    }

    private static void ReadContainer(InventoryManager* manager, InventoryType type, Dictionary<uint, int> accumulator)
    {
        var container = manager->GetInventoryContainer(type);
        if (container == null || !container->IsLoaded)
            return;

        for (var i = 0; i < container->Size; i++)
        {
            var slot = container->GetInventorySlot(i);
            if (slot == null)
                continue;

            var itemId = slot->ItemId;
            if (itemId == 0)
                continue;

            accumulator[itemId] = accumulator.GetValueOrDefault(itemId) + slot->Quantity;
        }
    }
}
