namespace Artificer.Test.Simulator;

[TestClass]
public class UtilsTests
{
    // ---- ActionCategoryUtils ----

    [TestMethod]
    public void ActionCategory_GetActions_ReturnsAllCategories()
    {
        foreach (var category in Enum.GetValues<ActionCategory>())
        {
            // Combo category has no direct entries but should still resolve
            if (category == ActionCategory.Combo)
                continue;

            var actions = category.GetActions();
            Assert.IsNotNull(actions);
            Assert.IsTrue(actions.Count > 0, $"Category {category} should have at least one action");
        }
    }

    [TestMethod]
    public void ActionCategory_GetActions_ActionsAreSortedByLevel()
    {
        foreach (var category in Enum.GetValues<ActionCategory>())
        {
            if (category == ActionCategory.Combo)
                continue;

            var actions = category.GetActions();
            for (var i = 1; i < actions.Count; i++)
                Assert.IsTrue(actions[i].Level() >= actions[i - 1].Level(),
                    $"Actions in category {category} should be sorted by level");
        }
    }

    [TestMethod]
    public void ActionCategory_GetActions_UnknownCategoryThrows()
    {
        var invalid = (ActionCategory)255;
        Assert.ThrowsException<ArgumentException>(() => invalid.GetActions());
    }

    [TestMethod]
    public void ActionCategory_GetDisplayName_KnownCategories()
    {
        Assert.AreEqual("First Turn", ActionCategory.FirstTurn.GetDisplayName());
        Assert.AreEqual("Synthesis", ActionCategory.Synthesis.GetDisplayName());
        Assert.AreEqual("Quality", ActionCategory.Quality.GetDisplayName());
        Assert.AreEqual("Durability", ActionCategory.Durability.GetDisplayName());
        Assert.AreEqual("Buffs", ActionCategory.Buffs.GetDisplayName());
        Assert.AreEqual("Other", ActionCategory.Other.GetDisplayName());
    }

    [TestMethod]
    public void ActionCategory_GetDisplayName_UnknownFallsBackToString()
    {
        var combo = ActionCategory.Combo;
        // Combo is not handled specially; falls through to default
        Assert.AreEqual(combo.ToString(), combo.GetDisplayName());
    }

    [TestMethod]
    public void ActionCategory_AllActionTypesCovered()
    {
        // Every non-Combo ActionType should appear in exactly one category
        var allCategorized = Enum.GetValues<ActionCategory>()
            .Where(c => c != ActionCategory.Combo)
            .SelectMany(c => c.GetActions())
            .ToHashSet();

        foreach (var action in Enum.GetValues<ActionType>())
        {
            // Combo virtual actions are not in categories
            if (action is ActionType.StandardTouchCombo or ActionType.AdvancedTouchCombo
                       or ActionType.ObservedAdvancedTouchCombo or ActionType.RefinedTouchCombo)
                continue;

            Assert.IsTrue(allCategorized.Contains(action),
                $"ActionType.{action} should appear in a category");
        }
    }

    // ---- ConditionUtils ----

    [TestMethod]
    public void ConditionUtils_GetPossibleConditions_NormalOnly()
    {
        // Bit 0 = Normal
        var conditions = ConditionUtils.GetPossibleConditions(0b0000_0001);
        CollectionAssert.AreEquivalent(new[] { Condition.Normal }, conditions);
    }

    [TestMethod]
    public void ConditionUtils_GetPossibleConditions_Multiple()
    {
        // Normal + Good + Excellent
        var conditions = ConditionUtils.GetPossibleConditions(0b0000_0111);
        CollectionAssert.AreEquivalent(
            new[] { Condition.Normal, Condition.Good, Condition.Excellent },
            conditions);
    }

    [TestMethod]
    public void ConditionUtils_GetPossibleConditions_Expert()
    {
        // Malleable + Primed (bits 7 and 8)
        var conditions = ConditionUtils.GetPossibleConditions(0b0001_1000_0000);
        CollectionAssert.AreEquivalent(
            new[] { Condition.Malleable, Condition.Primed },
            conditions);
    }

    [TestMethod]
    public void ConditionUtils_GetPossibleConditions_None()
    {
        var conditions = ConditionUtils.GetPossibleConditions(0);
        Assert.AreEqual(0, conditions.Length);
    }

    [TestMethod]
    public void ConditionUtils_GetPossibleConditions_AllConditions()
    {
        // All 10 condition bits set
        ushort allBits = 0b0000_0011_1111_1111;
        var conditions = ConditionUtils.GetPossibleConditions(allBits);
        Assert.AreEqual(10, conditions.Length);
    }

    // ---- SimulationInput AvailableConditions ----

    [TestMethod]
    public void SimulationInput_AvailableConditions_UsesConditionsFlag()
    {
        var recipe = new RecipeInfo
        {
            ClassJobLevel = 90,
            MaxDurability = 80,
            MaxQuality = 5000,
            MaxProgress = 3000,
            QualityModifier = 100,
            QualityDivider = 115,
            ProgressModifier = 100,
            ProgressDivider = 130,
            ConditionsFlag = 0b0000_0111, // Normal + Good + Excellent
        };
        var stats = new CharacterStats { Craftsmanship = 3000, Control = 3000, CP = 600, Level = 90 };
        var input = new SimulationInput(stats, recipe);

        CollectionAssert.AreEquivalent(
            new[] { Condition.Normal, Condition.Good, Condition.Excellent },
            input.AvailableConditions);
    }
}
