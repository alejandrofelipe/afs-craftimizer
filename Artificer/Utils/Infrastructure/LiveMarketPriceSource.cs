using System;
using System.Linq;
using Artificer.Application.Retainer;
using Artificer.Plugin;
using Dalamud.Game.Network.Structures;

namespace Artificer.Utils.Infrastructure;

/// <summary>
/// Menor preço do home world via as offerings AO VIVO que o jogo emite ao abrir compare-prices
/// (IMarketBoard.OfferingsReceived). Só entrega dado quando o usuário abre o compare-prices de um item.
/// </summary>
public sealed class LiveMarketPriceSource : IDisposable
{
    /// <summary>Disparado quando chega o menor preço de um item: (itemId, menor, éDeRetainerPróprio, isHq).</summary>
    public event Action<uint, long, bool, bool>? LowestReceived;

    public LiveMarketPriceSource()
    {
        Service.MarketBoard.OfferingsReceived += OnOfferings;
    }

    private void OnOfferings(IMarketBoardCurrentOfferings offerings)
    {
        var listings = offerings.ItemListings;
        if (listings == null || listings.Count == 0)
            return;

        var itemId = listings[0].ItemId;
        var own = RetainerMarketReader.OwnRetainerIds();

        // ItemId/PricePerUnit são uint (widening implícito p/ long); RetainerId já é ulong.
        var mapped = listings.Select(l => new MarketOffering(l.PricePerUnit, l.IsHq, l.RetainerId)).ToList();

        // NQ e HQ chegam juntos; resolvemos os dois e disparamos um evento por qualidade
        // (o consumidor casa pela qualidade do item que ele está vendendo).
        foreach (var wantHq in new[] { false, true })
        {
            var r = LowestPriceSelector.SelectLowest(mapped, wantHq, own);
            if (r is { } v)
                LowestReceived?.Invoke(itemId, v.Price, v.IsOwn, wantHq);
        }
    }

    /// <summary>True se o jogador está no home world (offerings ao vivo = home world).</summary>
    public static bool IsAtHomeWorld()
    {
        var lp = Service.Objects.LocalPlayer;
        return lp != null && lp.CurrentWorld.RowId == lp.HomeWorld.RowId;
    }

    public void Dispose() => Service.MarketBoard.OfferingsReceived -= OnOfferings;
}
