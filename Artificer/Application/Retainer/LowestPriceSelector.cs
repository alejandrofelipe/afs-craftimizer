using System.Collections.Generic;

namespace Artificer.Application.Retainer;

/// <summary>Seleção pura do menor preço casando qualidade; marca se a menor oferta é de retainer próprio.</summary>
public static class LowestPriceSelector
{
    public static (long Price, bool IsOwn)? SelectLowest(IEnumerable<MarketOffering> offerings, bool wantHq, IReadOnlySet<ulong> ownRetainerIds)
    {
        (long Price, bool IsOwn)? best = null;
        foreach (var o in offerings)
        {
            if (o.IsHq != wantHq)
                continue;
            if (best is null || o.PricePerUnit < best.Value.Price)
                best = (o.PricePerUnit, ownRetainerIds.Contains(o.RetainerId));
        }
        return best;
    }
}
