using System.Linq;
using Artificer.Application.Fishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.Application.Fishing;

[TestClass]
public sealed class CosmicFishCatchDataTests
{
    [TestMethod]
    public void Entries_CoverAllThreeMoons() // piso: Sinus tinha ~90 missões (451–544), Oizys ~50 (1650–1699)
        => Assert.IsTrue(CosmicFishCatchData.Entries.Count >= 60,
            $"Esperado >= 60 peixes cosmic, obtido {CosmicFishCatchData.Entries.Count}");

    [TestMethod]
    public void EveryEntry_HasDefinedMechanics() // BaitItemId 0 = sentinela "isca desconhecida" (decisão 2026-08-31)
    {
        foreach (var (id, e) in CosmicFishCatchData.Entries)
        {
            Assert.IsTrue(e.BaitItemId != 0 || e.MoochChain.Length == 0,
                $"Peixe {id}: mooch chain com bait sentinela"); // isca desconhecida só ocorre sem mooch
            Assert.IsTrue(System.Enum.IsDefined(e.Tug), $"Peixe {id}: tug indefinido");
            Assert.IsTrue(System.Enum.IsDefined(e.Hookset), $"Peixe {id}: hookset indefinido");
            Assert.IsTrue(e.MultiHook <= 5, $"Peixe {id}: MultiHook {e.MultiHook} inválido"); // fonte tem 1/4/5 reais
        }
    }

    [TestMethod]
    public void MoochChains_ReferenceKnownFish()
    {
        foreach (var (id, e) in CosmicFishCatchData.Entries)
            foreach (var mooch in e.MoochChain)
                Assert.IsTrue(CosmicFishCatchData.Entries.ContainsKey(mooch),
                    $"Peixe {id}: mooch {mooch} sem entry própria");
    }

    [TestMethod]
    public void Predators_ReferenceKnownFish()
    {
        foreach (var (id, e) in CosmicFishCatchData.Entries)
            foreach (var (fishId, count) in e.Predators ?? [])
            {
                Assert.IsTrue(count > 0, $"Peixe {id}: predator count {count}");
                Assert.IsTrue(CosmicFishCatchData.Entries.ContainsKey(fishId),
                    $"Peixe {id}: predator {fishId} sem entry própria");
            }
    }

    [TestMethod]
    public void MoochChains_AreFullyFlattened()
    {
        // Cadeia flattened: o 1º elo não pode ter cadeia própria (é pescado direto na isca),
        // e a cadeia própria de cada elo seguinte deve ser exatamente o prefixo anterior.
        foreach (var (id, e) in CosmicFishCatchData.Entries)
        {
            for (var i = 0; i < e.MoochChain.Length; i++)
            {
                Assert.IsTrue(CosmicFishCatchData.Entries.TryGetValue(e.MoochChain[i], out var link),
                    $"Peixe {id}: mooch {e.MoochChain[i]} sem entry própria");
                CollectionAssert.AreEqual(e.MoochChain[..i], link!.MoochChain,
                    $"Peixe {id}: cadeia não flattened no elo {e.MoochChain[i]}");
            }
        }
    }
}
