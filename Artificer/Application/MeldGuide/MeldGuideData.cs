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
/// Meld sets do guia do Teamcraft (https://guides.ffxivteamcraft.com/guide/crafting-melding-guide).
///
/// Materia: Craftsman's Competence = Craftsmanship, Cunning = Control, Command = CP.
/// GuaranteedCount = nº de melds listados que ocupam slots guaranteed (o resto = overmelds).
/// RecommendedItemId = 0 por enquanto (sem ícone / sem aviso de "item difere"); preencher com os
/// item ids reais depois para habilitar ícones. High-Tier: main hand é relic (Alembic of Stars) sem melds → omitido.
///
/// Por patch: re-curar (novos exports do Teamcraft) quando os tiers mudarem.
/// </summary>
public static class MeldGuideData
{
    public static IReadOnlyList<MeldTier> Tiers { get; } = BuildTiers();

    private static IReadOnlyList<MeldTier> BuildTiers()
    {
        static TargetMateria T(CraftStat s, int g) => new(s, g);
        var cms = CraftStat.Craftsmanship;
        var ctrl = CraftStat.Control;
        var cp = CraftStat.CP;
        const string url = "https://guides.ffxivteamcraft.com/guide/crafting-melding-guide";

        // ── High-Tier 7.51 (export do Teamcraft) — 5933 / 5470 / 649 ─────────────────
        // Main hand = relic Alembic of Stars (sem melds) → omitido.
        var high = new MeldTier(
            Id: "high", Name: "High-Tier 7.51", Patch: "7.51",
            Craftsmanship: 5933, Control: 5470, Cp: 649, TeamcraftUrl: url,
            Pieces:
            [
                new(EquipSlot.OffHand, 0, 2, [T(cp,12), T(cp,12), T(ctrl,9),  T(cp,11), T(cp,11)]),   // Gold Thumb's Mortar
                new(EquipSlot.Head,    0, 2, [T(cms,12), T(cms,12), T(cp,12),  T(cms,11), T(cp,11)]),  // Crested Headband
                new(EquipSlot.Body,    0, 2, [T(cms,12), T(cms,12), T(cms,12), T(cms,11), T(cp,11)]),  // Crested Shirt
                new(EquipSlot.Hands,   0, 2, [T(cms,12), T(cms,12), T(cp,12),  T(cp,11), T(cp,11)]),   // Crested Gloves
                new(EquipSlot.Legs,    0, 2, [T(cp,12), T(cp,12), T(ctrl,12),  T(cp,11), T(cp,11)]),   // Crested Culottes
                new(EquipSlot.Feet,    0, 2, [T(cp,12), T(cp,12), T(ctrl,12),  T(cp,11), T(cp,11)]),   // Crested Boots
                new(EquipSlot.Ears,    0, 1, [T(cp,12), T(cp,12), T(cp,11),    T(ctrl,11), T(ctrl,5)]),// Crested Earrings
                new(EquipSlot.Neck,    0, 1, [T(cp,12), T(cp,12), T(cp,11),    T(ctrl,11), T(ctrl,5)]),// Crested Necklace
                new(EquipSlot.Wrists,  0, 1, [T(cp,12), T(cp,12), T(cp,11),    T(ctrl,11), T(ctrl,5)]),// Crested Bracelet
                new(EquipSlot.Ring1,   0, 1, [T(cms,12), T(cms,12), T(cp,11),  T(cp,11), T(ctrl,9)]),  // Crested Ring
                new(EquipSlot.Ring2,   0, 1, [T(cms,12), T(cms,12), T(cp,11),  T(cp,11), T(ctrl,9)]),  // Crested Ring
            ]);

        // ── Mid-Tier 7.51 (export do Teamcraft) — 5812 / 5083 / 649 ─────────────────
        var mid = new MeldTier(
            Id: "mid", Name: "Mid-Tier 7.51", Patch: "7.51",
            Craftsmanship: 5812, Control: 5083, Cp: 649, TeamcraftUrl: url,
            Pieces:
            [
                new(EquipSlot.MainHand, 0, 1, [T(ctrl,6)]),                                 // Star Tech Alembic
                new(EquipSlot.OffHand,  0, 1, [T(ctrl,6)]),                                 // Star Tech Mortar
                new(EquipSlot.Head,   0, 2, [T(cms,12), T(cms,12), T(cp,10), T(cms,9)]),    // Crested Headband
                new(EquipSlot.Body,   0, 2, [T(cms,12), T(cms,12), T(cp,10), T(cms,9)]),    // Crested Shirt
                new(EquipSlot.Hands,  0, 2, [T(cms,12), T(cms,12), T(cp,10), T(cms,9)]),    // Crested Gloves
                new(EquipSlot.Legs,   0, 2, [T(cp,12), T(cp,12), T(ctrl,12), T(cp,9)]),     // Crested Culottes
                new(EquipSlot.Feet,   0, 2, [T(cp,12), T(cp,12), T(ctrl,12), T(cp,9)]),     // Crested Boots
                new(EquipSlot.Ears,   0, 1, [T(cp,12), T(ctrl,12), T(cp,9), T(ctrl,4)]),    // Crested Earrings
                new(EquipSlot.Neck,   0, 1, [T(cp,12), T(ctrl,12), T(cp,9), T(ctrl,4)]),    // Crested Necklace
                new(EquipSlot.Wrists, 0, 1, [T(cp,12), T(ctrl,12), T(cp,9), T(ctrl,4)]),    // Crested Bracelet
                new(EquipSlot.Ring1,  0, 1, [T(cms,12), T(cms,10), T(ctrl,9), T(cms,9)]),   // Crested Ring
                new(EquipSlot.Ring2,  0, 1, [T(cms,12), T(cms,10), T(ctrl,9), T(cms,9)]),   // Crested Ring
            ]);

        // ── Budget 7.51 (export do Teamcraft) — 5635 / 4969 / 583 ───────────────────
        // Set de scrip (Star Tech): tudo grade XII, sem overmelds (todos guaranteed).
        var budget = new MeldTier(
            Id: "budget", Name: "Budget 7.51", Patch: "7.51",
            Craftsmanship: 5635, Control: 4969, Cp: 583, TeamcraftUrl: url,
            Pieces:
            [
                new(EquipSlot.MainHand, 0, 1, [T(cp,12)]),               // Star Tech Alembic
                new(EquipSlot.OffHand,  0, 1, [T(cp,12)]),               // Star Tech Mortar
                new(EquipSlot.Head,   0, 2, [T(cp,12), T(cms,12)]),      // Star Tech Headgear
                new(EquipSlot.Body,   0, 2, [T(cms,12), T(cms,12)]),     // Star Tech Top
                new(EquipSlot.Hands,  0, 2, [T(cms,12), T(cms,12)]),     // Star Tech Armguards
                new(EquipSlot.Legs,   0, 2, [T(cp,12), T(cp,12)]),       // Star Tech Slops
                new(EquipSlot.Feet,   0, 2, [T(cp,12), T(cp,12)]),       // Star Tech Workboots
                new(EquipSlot.Ears,   0, 1, [T(ctrl,12)]),               // Star Tech Ear Cuff
                new(EquipSlot.Neck,   0, 1, [T(ctrl,12)]),               // Star Tech Choker
                new(EquipSlot.Wrists, 0, 1, [T(ctrl,12)]),               // Star Tech Bracelet
                new(EquipSlot.Ring1,  0, 1, [T(cms,12)]),                // Star Tech Ring
                new(EquipSlot.Ring2,  0, 1, [T(cms,12)]),                // Star Tech Ring
            ]);

        return [high, mid, budget];
    }
}
