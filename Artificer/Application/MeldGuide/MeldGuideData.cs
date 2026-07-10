using System.Collections.Generic;
using Artificer.Utils;   // CraftStat

namespace Artificer.Application.MeldGuide;

public enum EquipSlot { MainHand, OffHand, Head, Body, Hands, Legs, Feet, Ears, Neck, Wrists, Ring1, Ring2 }

public readonly record struct TargetMateria(CraftStat Stat, int Grade);

/// <param name="Slot">Equip slot.</param>
/// <param name="RecommendedItemId">Item id do gear recomendado (0 = desconhecido → sem ícone/aviso de item).</param>
/// <param name="GuaranteedCount">Nº de slots guaranteed; Target[GuaranteedCount..] são overmelds.</param>
/// <param name="Target">Melds recomendados, ordenados (guaranteed primeiro, depois overmelds).</param>
public sealed record MeldPiece(EquipSlot Slot, uint RecommendedItemId, int GuaranteedCount, IReadOnlyList<TargetMateria> Target);

public sealed record MeldTier(string Id, string Name, string Patch, int Craftsmanship, int Control, int Cp, string TeamcraftUrl, IReadOnlyList<MeldPiece> Pieces);

/// <summary>
/// ⚠ DADOS PLACEHOLDER — NÃO são os melds reais do guia do Teamcraft.
///
/// O `example` abaixo é um dataset ILUSTRATIVO só para o scaffold funcionar end-to-end.
/// Substituir por tiers reais (High/Mid/Budget 7.51) a partir de um export de gearset do
/// Teamcraft ou leitura in-game — preencher `RecommendedItemId`, `GuaranteedCount` e `Target`
/// (stat+grade por slot). A estrutura já está pronta; só trocar os valores.
/// Fonte: https://guides.ffxivteamcraft.com/guide/crafting-melding-guide
/// </summary>
public static class MeldGuideData
{
    public static IReadOnlyList<MeldTier> Tiers { get; } = BuildTiers();

    private static IReadOnlyList<MeldTier> BuildTiers()
    {
        static TargetMateria T(CraftStat s, int g) => new(s, g);

        var example = new MeldTier(
            Id: "example",
            Name: "Exemplo (placeholder)",
            Patch: "7.51",
            Craftsmanship: 0, Control: 0, Cp: 0,
            TeamcraftUrl: "https://guides.ffxivteamcraft.com/guide/crafting-melding-guide",
            Pieces:
            [
                // ItemId 0 (sem ícone); estrutura ilustrativa: guaranteed-first + overmelds.
                new(EquipSlot.MainHand, 0, 2, [T(CraftStat.CP, 12), T(CraftStat.CP, 12), T(CraftStat.CP, 10)]),
                new(EquipSlot.Head,     0, 2, [T(CraftStat.Control, 12), T(CraftStat.CP, 12), T(CraftStat.Control, 10), T(CraftStat.CP, 10), T(CraftStat.Control, 8)]),
                new(EquipSlot.Body,     0, 2, [T(CraftStat.Control, 12), T(CraftStat.CP, 12), T(CraftStat.Craftsmanship, 10), T(CraftStat.Control, 10), T(CraftStat.CP, 10)]),
                new(EquipSlot.Ears,     0, 1, [T(CraftStat.CP, 12), T(CraftStat.CP, 10), T(CraftStat.CP, 8), T(CraftStat.CP, 7)]),
            ]);

        return [example];
    }
}
