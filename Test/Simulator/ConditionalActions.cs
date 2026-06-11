namespace Artificer.Test.Simulator;

/// <summary>
/// Tests for actions that require special conditions (Good/Excellent/HeartAndSoul)
/// and for specialist-only actions.
/// </summary>
[TestClass]
public class ConditionalActionsTests
{
    // A standard high-level input used by most tests
    private static SimulationInput MakeInput(
        bool isSpecialist = false,
        bool isExpert = false,
        int level = 90,
        int craftsmanship = 3304,
        int control = 3374,
        int cp = 575) =>
        new(new CharacterStats
        {
            Craftsmanship = craftsmanship,
            Control = control,
            CP = cp,
            Level = level,
            CanUseManipulation = true,
            IsSpecialist = isSpecialist,
        },
        new RecipeInfo
        {
            IsExpert = isExpert,
            ClassJobLevel = level,
            MaxDurability = 80,
            MaxQuality = 7200,
            MaxProgress = 3500,
            QualityModifier = 80,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130,
        });

    private static SimulatorNoRandom MakeSim(SimulationInput input) =>
        new() { State = new SimulationState(input) };

    private static SimulationState WithCondition(SimulationInput input, Condition condition)
    {
        var s = new SimulationState(input);
        s.Condition = condition;
        return s;
    }

    // ---- TricksOfTheTrade ----

    [TestMethod]
    public void TricksOfTheTrade_CannotUseOnNormal()
    {
        var sim = MakeSim(MakeInput());
        Assert.IsFalse(ActionType.TricksOfTheTrade.Base().CanUse(sim));
    }

    [TestMethod]
    public void TricksOfTheTrade_CanUseOnGood()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom { State = WithCondition(input, Condition.Good) };
        Assert.IsTrue(ActionType.TricksOfTheTrade.Base().CanUse(sim));
    }

    [TestMethod]
    public void TricksOfTheTrade_CanUseOnExcellent()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom { State = WithCondition(input, Condition.Excellent) };
        Assert.IsTrue(ActionType.TricksOfTheTrade.Base().CanUse(sim));
    }

    [TestMethod]
    public void TricksOfTheTrade_RestoresCPOnGood()
    {
        var input = MakeInput(cp: 575);
        var state = WithCondition(input, Condition.Good);
        state.CP = 500; // spend some CP first
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.TricksOfTheTrade);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.AreEqual(520, outState.CP); // +20, no CP cost
    }

    [TestMethod]
    public void TricksOfTheTrade_WithHeartAndSoul_CanUseOnNormal()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.HeartAndSoul]);
        // HeartAndSoul is active → TricksOfTheTrade should be usable
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsTrue(ActionType.TricksOfTheTrade.Base().CanUse(sim2));
    }

    [TestMethod]
    public void TricksOfTheTrade_WithHeartAndSoul_ConsumesEffect()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var (_, stateAfterHAS, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.HeartAndSoul]);
        var (resp, outState) = new SimulatorNoRandom { State = stateAfterHAS }.Execute(stateAfterHAS, ActionType.TricksOfTheTrade);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        // HeartAndSoul buff should be consumed
        Assert.IsFalse(outState.ActiveEffects.HasEffect(EffectType.HeartAndSoul));
    }

    // ---- PreciseTouch ----

    [TestMethod]
    public void PreciseTouch_CannotUseOnNormal()
    {
        var sim = MakeSim(MakeInput());
        Assert.IsFalse(ActionType.PreciseTouch.Base().CanUse(sim));
    }

    [TestMethod]
    public void PreciseTouch_CanUseOnGood()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom { State = WithCondition(input, Condition.Good) };
        Assert.IsTrue(ActionType.PreciseTouch.Base().CanUse(sim));
    }

    [TestMethod]
    public void PreciseTouch_IncreasesInnerQuiet()
    {
        var input = MakeInput();
        var state = WithCondition(input, Condition.Good);
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.PreciseTouch);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        // PreciseTouch calls base (which adds IQ from quality) + StrengthenEffect → 2 stacks
        Assert.AreEqual(2, outState.ActiveEffects.InnerQuiet);
    }

    [TestMethod]
    public void PreciseTouch_WithHeartAndSoul_ConsumesEffect()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var (_, stateAfterHAS, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.HeartAndSoul]);
        var (resp, outState) = new SimulatorNoRandom { State = stateAfterHAS }.Execute(stateAfterHAS, ActionType.PreciseTouch);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.IsFalse(outState.ActiveEffects.HasEffect(EffectType.HeartAndSoul));
    }

    // ---- IntensiveSynthesis ----

    [TestMethod]
    public void IntensiveSynthesis_CannotUseOnNormal()
    {
        var sim = MakeSim(MakeInput());
        Assert.IsFalse(ActionType.IntensiveSynthesis.Base().CanUse(sim));
    }

    [TestMethod]
    public void IntensiveSynthesis_CanUseOnGood()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom { State = WithCondition(input, Condition.Good) };
        Assert.IsTrue(ActionType.IntensiveSynthesis.Base().CanUse(sim));
    }

    [TestMethod]
    public void IntensiveSynthesis_IncreasesProgress()
    {
        var input = MakeInput();
        var state = WithCondition(input, Condition.Excellent);
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.IntensiveSynthesis);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.IsTrue(outState.Progress > 0);
    }

    [TestMethod]
    public void IntensiveSynthesis_WithHeartAndSoul_ConsumesEffect()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var (_, stateAfterHAS, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.HeartAndSoul]);
        var (resp, outState) = new SimulatorNoRandom { State = stateAfterHAS }.Execute(stateAfterHAS, ActionType.IntensiveSynthesis);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.IsFalse(outState.ActiveEffects.HasEffect(EffectType.HeartAndSoul));
    }

    // ---- HeartAndSoul (specialist) ----

    [TestMethod]
    public void HeartAndSoul_CannotUseAsNonSpecialist()
    {
        var sim = MakeSim(MakeInput(isSpecialist: false));
        Assert.IsFalse(ActionType.HeartAndSoul.Base().CanUse(sim));
    }

    [TestMethod]
    public void HeartAndSoul_CanUseAsSpecialist()
    {
        var sim = MakeSim(MakeInput(isSpecialist: true));
        Assert.IsTrue(ActionType.HeartAndSoul.Base().CanUse(sim));
    }

    [TestMethod]
    public void HeartAndSoul_CannotUseMoreThanOnce()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.HeartAndSoul]);
        // After using once, UsedHeartAndSoul = true
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.HeartAndSoul.Base().CouldUse(sim2));
    }

    // ---- CarefulObservation (specialist) ----

    [TestMethod]
    public void CarefulObservation_CannotUseAsNonSpecialist()
    {
        var sim = MakeSim(MakeInput(isSpecialist: false));
        Assert.IsFalse(ActionType.CarefulObservation.Base().IsPossible(sim));
    }

    [TestMethod]
    public void CarefulObservation_CanUseAsSpecialist()
    {
        var sim = MakeSim(MakeInput(isSpecialist: true));
        Assert.IsTrue(ActionType.CarefulObservation.Base().CanUse(sim));
    }

    [TestMethod]
    public void CarefulObservation_MaxThreeUses()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input),
        [
            ActionType.CarefulObservation,
            ActionType.CarefulObservation,
            ActionType.CarefulObservation,
        ]);
        Assert.AreEqual(3, state.ActionStates.CarefulObservationCount);
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.CarefulObservation.Base().CanUse(sim2));
    }

    [TestMethod]
    public void CarefulObservation_DoesNotConsumeStep()
    {
        var input = MakeInput(isSpecialist: true);
        var sim = new SimulatorNoRandom();
        var initial = new SimulationState(input);
        var (_, state, _) = sim.ExecuteMultiple(initial, [ActionType.CarefulObservation]);
        // increasesStepCount = false → StepCount unchanged
        Assert.AreEqual(initial.StepCount, state.StepCount);
    }

    // ---- QuickInnovation (specialist) ----

    [TestMethod]
    public void QuickInnovation_CannotUseAsNonSpecialist()
    {
        var sim = MakeSim(MakeInput(isSpecialist: false, level: 96));
        Assert.IsFalse(ActionType.QuickInnovation.Base().IsPossible(sim));
    }

    [TestMethod]
    public void QuickInnovation_CanUseAsSpecialist()
    {
        var sim = MakeSim(MakeInput(isSpecialist: true, level: 100));
        Assert.IsTrue(ActionType.QuickInnovation.Base().CanUse(sim));
    }

    [TestMethod]
    public void QuickInnovation_CannotUseMoreThanOnce()
    {
        var input = MakeInput(isSpecialist: true, level: 100);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.QuickInnovation]);
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.QuickInnovation.Base().CouldUse(sim2));
    }

    // ---- TrainedPerfection ----

    [TestMethod]
    public void TrainedPerfection_CannotUseTwice()
    {
        var input = MakeInput(level: 100);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.TrainedPerfection]);
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.TrainedPerfection.Base().CanUse(sim2));
    }

    // ---- DaringTouch (requires Expedience from HastyTouch at level 96+) ----

    [TestMethod]
    public void DaringTouch_CannotUseWithoutExpedience()
    {
        var sim = MakeSim(MakeInput(level: 100));
        Assert.IsFalse(ActionType.DaringTouch.Base().CanUse(sim));
    }

    [TestMethod]
    public void DaringTouch_CanUseAfterHastyTouch()
    {
        // HastyTouch has 60% success rate → fails in SimulatorNoRandom.
        // Simulate a successful HastyTouch by setting Expedience manually.
        var input = MakeInput(level: 100);
        var state = new SimulationState(input);
        state.ActiveEffects.SetDuration(EffectType.Expedience, 1);
        var sim = new SimulatorNoRandom { State = state };
        Assert.IsTrue(ActionType.DaringTouch.Base().CanUse(sim));
    }

    // ---- PrudentSynthesis / PrudentTouch (blocked by WasteNot) ----

    [TestMethod]
    public void PrudentSynthesis_CannotUseUnderWasteNot()
    {
        var input = MakeInput(level: 88);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.WasteNot]);
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.PrudentSynthesis.Base().CanUse(sim2));
    }

    [TestMethod]
    public void PrudentTouch_CannotUseUnderWasteNot2()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.WasteNot2]);
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.PrudentTouch.Base().CanUse(sim2));
    }

    // ---- RefinedTouch combo bonus ----

    [TestMethod]
    public void RefinedTouch_WithComboGivesExtraIQ()
    {
        var input = MakeInput(level: 92);
        // BasicTouch → RefinedTouch should trigger combo bonus
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input),
        [
            ActionType.BasicTouch,
            ActionType.RefinedTouch,
        ]);
        // BasicTouch adds 1 IQ, RefinedTouch base adds 1 IQ, combo bonus adds 1 more = 3
        Assert.AreEqual(3, state.ActiveEffects.InnerQuiet);
    }

    [TestMethod]
    public void RefinedTouch_WithoutComboNoBonus()
    {
        var input = MakeInput(level: 92);
        // Just RefinedTouch without prior BasicTouch
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.RefinedTouch]);
        Assert.AreEqual(1, state.ActiveEffects.InnerQuiet);
    }

    // ---- Groundwork half-efficiency when low durability ----

    [TestMethod]
    public void Groundwork_HalfEfficiencyOnLowDurability()
    {
        var input = MakeInput(level: 86);
        // Start with only 10 durability (< 20 cost)
        var state = new SimulationState(input);
        state.Durability = 10;
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.Groundwork);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        // Efficiency is halved: 360/2 = 180 instead of 360
        var fullState = new SimulationState(input); // full durability
        fullState.Durability = 80;
        var (resp2, outState2) = new SimulatorNoRandom { State = fullState }.Execute(fullState, ActionType.Groundwork);
        Assert.IsTrue(outState2.Progress > outState.Progress, "Full durability should yield more progress than halved");
    }
}
