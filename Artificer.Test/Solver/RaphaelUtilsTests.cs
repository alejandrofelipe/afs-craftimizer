using RaphaelAction = Raphael.Action;

namespace Artificer.Test.Solver;

[TestClass]
public class RaphaelUtilsTests
{
    // ---- ConvertRawAction — all 31 valid mappings ----

    [TestMethod]
    public void ConvertRawAction_AllValidMappings()
    {
        var expected = new (RaphaelAction raw, ActionType converted)[]
        {
            (RaphaelAction.BasicSynthesis,    ActionType.BasicSynthesis),
            (RaphaelAction.BasicTouch,        ActionType.BasicTouch),
            (RaphaelAction.MasterMend,        ActionType.MastersMend),
            (RaphaelAction.Observe,           ActionType.Observe),
            (RaphaelAction.TricksOfTheTrade,  ActionType.TricksOfTheTrade),
            (RaphaelAction.WasteNot,          ActionType.WasteNot),
            (RaphaelAction.Veneration,        ActionType.Veneration),
            (RaphaelAction.StandardTouch,     ActionType.StandardTouch),
            (RaphaelAction.GreatStrides,      ActionType.GreatStrides),
            (RaphaelAction.Innovation,        ActionType.Innovation),
            (RaphaelAction.WasteNot2,         ActionType.WasteNot2),
            (RaphaelAction.ByregotsBlessing,  ActionType.ByregotsBlessing),
            (RaphaelAction.PreciseTouch,      ActionType.PreciseTouch),
            (RaphaelAction.MuscleMemory,      ActionType.MuscleMemory),
            (RaphaelAction.CarefulSynthesis,  ActionType.CarefulSynthesis),
            (RaphaelAction.Manipulation,      ActionType.Manipulation),
            (RaphaelAction.PrudentTouch,      ActionType.PrudentTouch),
            (RaphaelAction.AdvancedTouch,     ActionType.AdvancedTouch),
            (RaphaelAction.Reflect,           ActionType.Reflect),
            (RaphaelAction.PreparatoryTouch,  ActionType.PreparatoryTouch),
            (RaphaelAction.Groundwork,        ActionType.Groundwork),
            (RaphaelAction.DelicateSynthesis, ActionType.DelicateSynthesis),
            (RaphaelAction.IntensiveSynthesis,ActionType.IntensiveSynthesis),
            (RaphaelAction.TrainedEye,        ActionType.TrainedEye),
            (RaphaelAction.HeartAndSoul,      ActionType.HeartAndSoul),
            (RaphaelAction.PrudentSynthesis,  ActionType.PrudentSynthesis),
            (RaphaelAction.TrainedFinesse,    ActionType.TrainedFinesse),
            (RaphaelAction.RefinedTouch,      ActionType.RefinedTouch),
            (RaphaelAction.QuickInnovation,   ActionType.QuickInnovation),
            (RaphaelAction.ImmaculateMend,    ActionType.ImmaculateMend),
            (RaphaelAction.TrainedPerfection, ActionType.TrainedPerfection),
        };

        foreach (var (raw, converted) in expected)
            Assert.AreEqual(converted, RaphaelUtils.ConvertRawAction(raw),
                $"ConvertRawAction({raw}) should return {converted}");
    }

    [TestMethod]
    public void ConvertRawAction_InvalidValue_Throws()
    {
        var invalid = (RaphaelAction)255;
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => RaphaelUtils.ConvertRawAction(invalid));
    }

    // ---- ConvertToRawAction — all valid mappings and null fallback ----

    [TestMethod]
    public void ConvertToRawAction_AllValidMappings()
    {
        var expected = new (ActionType converted, RaphaelAction raw)[]
        {
            (ActionType.BasicSynthesis,    RaphaelAction.BasicSynthesis),
            (ActionType.BasicTouch,        RaphaelAction.BasicTouch),
            (ActionType.MastersMend,       RaphaelAction.MasterMend),
            (ActionType.Observe,           RaphaelAction.Observe),
            (ActionType.TricksOfTheTrade,  RaphaelAction.TricksOfTheTrade),
            (ActionType.WasteNot,          RaphaelAction.WasteNot),
            (ActionType.Veneration,        RaphaelAction.Veneration),
            (ActionType.StandardTouch,     RaphaelAction.StandardTouch),
            (ActionType.GreatStrides,      RaphaelAction.GreatStrides),
            (ActionType.Innovation,        RaphaelAction.Innovation),
            (ActionType.WasteNot2,         RaphaelAction.WasteNot2),
            (ActionType.ByregotsBlessing,  RaphaelAction.ByregotsBlessing),
            (ActionType.PreciseTouch,      RaphaelAction.PreciseTouch),
            (ActionType.MuscleMemory,      RaphaelAction.MuscleMemory),
            (ActionType.CarefulSynthesis,  RaphaelAction.CarefulSynthesis),
            (ActionType.Manipulation,      RaphaelAction.Manipulation),
            (ActionType.PrudentTouch,      RaphaelAction.PrudentTouch),
            (ActionType.AdvancedTouch,     RaphaelAction.AdvancedTouch),
            (ActionType.Reflect,           RaphaelAction.Reflect),
            (ActionType.PreparatoryTouch,  RaphaelAction.PreparatoryTouch),
            (ActionType.Groundwork,        RaphaelAction.Groundwork),
            (ActionType.DelicateSynthesis, RaphaelAction.DelicateSynthesis),
            (ActionType.IntensiveSynthesis,RaphaelAction.IntensiveSynthesis),
            (ActionType.TrainedEye,        RaphaelAction.TrainedEye),
            (ActionType.HeartAndSoul,      RaphaelAction.HeartAndSoul),
            (ActionType.PrudentSynthesis,  RaphaelAction.PrudentSynthesis),
            (ActionType.TrainedFinesse,    RaphaelAction.TrainedFinesse),
            (ActionType.RefinedTouch,      RaphaelAction.RefinedTouch),
            (ActionType.QuickInnovation,   RaphaelAction.QuickInnovation),
            (ActionType.ImmaculateMend,    RaphaelAction.ImmaculateMend),
            (ActionType.TrainedPerfection, RaphaelAction.TrainedPerfection),
        };

        foreach (var (converted, raw) in expected)
            Assert.AreEqual(raw, RaphaelUtils.ConvertToRawAction(converted),
                $"ConvertToRawAction({converted}) should return {raw}");
    }

    [TestMethod]
    public void ConvertToRawAction_UnmappedAction_ReturnsNull()
    {
        // Combo actions have no Raphael equivalent
        Assert.IsNull(RaphaelUtils.ConvertToRawAction(ActionType.StandardTouchCombo));
        Assert.IsNull(RaphaelUtils.ConvertToRawAction(ActionType.AdvancedTouchCombo));
    }

    // ---- ConvertRawActions / ConvertToRawActions arrays ----

    [TestMethod]
    public void ConvertRawActions_ConvertsEntireList()
    {
        var raw = new[] { RaphaelAction.BasicSynthesis, RaphaelAction.BasicTouch };
        var result = RaphaelUtils.ConvertRawActions(raw);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(ActionType.BasicSynthesis, result[0]);
        Assert.AreEqual(ActionType.BasicTouch, result[1]);
    }

    [TestMethod]
    public void ConvertToRawActions_SkipsUnmappedActions()
    {
        var actions = new[]
        {
            ActionType.BasicSynthesis,
            ActionType.StandardTouchCombo, // unmapped
            ActionType.BasicTouch,
        };
        var result = RaphaelUtils.ConvertToRawActions(actions);

        // Combo should be skipped
        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(RaphaelAction.BasicSynthesis, result[0]);
        Assert.AreEqual(RaphaelAction.BasicTouch, result[1]);
    }

    [TestMethod]
    public void ConvertRawActions_EmptyList_ReturnsEmptyArray()
    {
        var result = RaphaelUtils.ConvertRawActions(Array.Empty<RaphaelAction>());
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void ConvertToRawActions_EmptyList_ReturnsEmptyArray()
    {
        var result = RaphaelUtils.ConvertToRawActions(Array.Empty<ActionType>());
        Assert.AreEqual(0, result.Length);
    }
}
