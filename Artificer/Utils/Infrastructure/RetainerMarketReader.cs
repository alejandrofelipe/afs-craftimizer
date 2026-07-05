using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Artificer.Utils.Infrastructure;

/// <summary>Item à venda no retainer ativo: slot, item, qtd, HQ e preço atual (unitário).</summary>
public readonly record struct RetainerMarketItem(short Slot, uint ItemId, int Quantity, bool IsHq, long CurrentPrice);

/// <summary>
/// Lê as listagens de venda do retainer ATIVO diretamente da memória (container RetainerMarket=12002),
/// sem tocar na UI. Só é válido enquanto um retainer está aberto.
/// </summary>
public static unsafe class RetainerMarketReader
{
    private const int RetainerCount = 10;

    public static bool TryReadListings(out List<RetainerMarketItem> items)
    {
        items = new();
        var inv = InventoryManager.Instance();
        if (inv == null) return false;

        var container = inv->GetInventoryContainer(InventoryType.RetainerMarket);
        if (container == null || !container->IsLoaded) return false;

        for (var i = 0; i < container->Size; i++)
        {
            var slot = container->GetInventorySlot(i);
            if (slot == null || slot->ItemId == 0) continue;
            var price = (long)inv->GetRetainerMarketPrice((short)i);
            items.Add(new RetainerMarketItem(
                (short)i,
                slot->ItemId,
                slot->Quantity,
                slot->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality),
                price));
        }
        return true;
    }

    /// <summary>IDs dos próprios retainers (para não dar undercut em si mesmo). Usa o mesmo padrão do InventoryScanner.</summary>
    public static IReadOnlySet<ulong> OwnRetainerIds()
    {
        var set = new HashSet<ulong>();
        var mgr = RetainerManager.Instance();
        if (mgr == null) return set;
        for (var i = 0u; i < RetainerCount; i++)
        {
            var r = mgr->GetRetainerBySortedIndex(i);
            if (r != null && r->RetainerId != 0)
                set.Add(r->RetainerId);
        }
        return set;
    }
}
