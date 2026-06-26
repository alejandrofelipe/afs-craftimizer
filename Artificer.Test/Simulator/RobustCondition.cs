namespace Artificer.Test.Simulator;

/// <summary>
/// Tests para a condição Robust (FFXIV 7.41): condição "telegrafada" que vira Sturdy no passo
/// seguinte e reduz o custo de durabilidade pela metade (igual a Sturdy).
/// Paridade com o upstream Craftimizer (PR #61 / commit 99ca21e).
/// </summary>
[TestClass]
public class RobustConditionTests
{
    private static SimulationInput MakeInput() =>
        new(new CharacterStats
        {
            Craftsmanship = 3304,
            Control = 3374,
            CP = 575,
            Level = 90,
            CanUseManipulation = true,
        },
        new RecipeInfo
        {
            ClassJobLevel = 90,
            MaxDurability = 80,
            MaxQuality = 7200,
            MaxProgress = 3500,
            QualityModifier = 80,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130,
        });

    [TestMethod]
    public void Robust_HalvesDurabilityCost_LikeSturdy()
    {
        var state = new SimulationState(MakeInput());
        state.Condition = Condition.Robust;
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.BasicSynthesis);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        // Robust reduz a durabilidade pela metade → ceil(10/2) = 5 → 80 - 5 = 75
        Assert.AreEqual(75, outState.Durability);
    }

    [TestMethod]
    public void Robust_StepsToSturdy()
    {
        var state = new SimulationState(MakeInput());
        state.Condition = Condition.Robust;
        var sim = new SimulatorNoRandom { State = state };
        sim.StepCondition();
        // Robust é telegrafada: a condição do passo seguinte é deterministicamente Sturdy.
        Assert.AreEqual(Condition.Sturdy, sim.Condition);
    }

    [TestMethod]
    public void GetPossibleConditions_IncludesRobust()
    {
        // Robust ocupa o bit 10 (0x0400) no condition mask.
        var conditions = ConditionUtils.GetPossibleConditions((ushort)(1 << 10));
        CollectionAssert.AreEquivalent(new[] { Condition.Robust }, conditions);
    }
}
