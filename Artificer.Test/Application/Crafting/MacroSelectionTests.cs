using Artificer.Application.Crafting;
using Artificer.Plugin;
using Artificer.Simulator.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Artificer.Test.Application.Crafting;

[TestClass]
public class MacroSelectionTests
{
    private static Macro M(ushort recipeId, params ActionType[] actions)
    {
        var m = new Macro { RecipeId = recipeId };
        m.Actions = actions;
        return m;
    }

    [TestMethod]
    public void DecideAutoSave_NoExisting_Inserts()
        => Assert.AreEqual(MacroSelection.AutoSaveOutcome.Insert, MacroSelection.DecideAutoSave(null, 0.5f));

    [TestMethod]
    public void DecideAutoSave_Better_Overwrites()
        => Assert.AreEqual(MacroSelection.AutoSaveOutcome.Overwrite, MacroSelection.DecideAutoSave(0.50f, 0.80f));

    [TestMethod]
    public void DecideAutoSave_NotBetterEnough_Skips()
        => Assert.AreEqual(MacroSelection.AutoSaveOutcome.Skip, MacroSelection.DecideAutoSave(0.80f, 0.8005f));

    [TestMethod]
    public void SelectBestForRecipe_PicksHighestScoringMatch()
    {
        var a = M(10, ActionType.BasicSynthesis);
        var b = M(10, ActionType.CarefulSynthesis);
        var other = M(99, ActionType.BasicSynthesis);
        var macros = new List<Macro> { a, other, b };

        // score: 'b' vale mais
        float Score(Macro m) => m == b ? 0.9f : 0.5f;
        var best = MacroSelection.SelectBestForRecipe(macros, 10, Score);
        Assert.AreSame(b, best);
    }

    [TestMethod]
    public void SelectBestForRecipe_NoCompletingMacro_ReturnsNull()
    {
        var a = M(10, ActionType.BasicSynthesis);
        var best = MacroSelection.SelectBestForRecipe(new[] { a }, 10, _ => 0f);
        Assert.IsNull(best);
    }

    [TestMethod]
    public void SelectBestForRecipe_IgnoresOtherRecipesAndEmptyMacros()
    {
        var empty = new Macro { RecipeId = 10 }; // sem ações
        var other = M(11, ActionType.BasicSynthesis);
        var best = MacroSelection.SelectBestForRecipe(new[] { empty, other }, 10, _ => 1f);
        Assert.IsNull(best);
    }
}
