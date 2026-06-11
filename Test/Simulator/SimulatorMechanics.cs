namespace Artificer.Test.Simulator;

/// <summary>
/// Tests for SimulationState properties and Simulator mechanics
/// (CompletionState, FinalAppraisal, condition modifiers, CP/durability math, etc.)
/// </summary>
[TestClass]
public class SimulatorMechanicsTests
{
    private static SimulationInput MakeInput(
        int level = 90,
        int craftsmanship = 3304,
        int control = 3374,
        int cp = 575,
        int maxProgress = 3500,
        int maxQuality = 7200,
        int maxDurability = 80,
        bool isExpert = false) =>
        new(new CharacterStats
        {
            Craftsmanship = craftsmanship,
            Control = control,
            CP = cp,
            Level = level,
            CanUseManipulation = true,
        },
        new RecipeInfo
        {
            IsExpert = isExpert,
            ClassJobLevel = level,
            MaxDurability = maxDurability,
            MaxQuality = maxQuality,
            MaxProgress = maxProgress,
            QualityModifier = 80,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130,
        });

    // ---- CompletionState ----

    [TestMethod]
    public void CompletionState_IncompleteOnStart()
    {
        var sim = new SimulatorNoRandom { State = new SimulationState(MakeInput()) };
        Assert.AreEqual(CompletionState.Incomplete, sim.CompletionState);
        Assert.IsFalse(sim.IsComplete);
    }

    [TestMethod]
    public void CompletionState_ProgressComplete()
    {
        // Use a tiny recipe so we can complete it quickly
        var input = MakeInput(maxProgress: 1);
        var sim = new SimulatorNoRandom();
        var (resp, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.BasicSynthesis]);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.AreEqual(CompletionState.ProgressComplete, new SimulatorNoRandom { State = state }.CompletionState);
    }

    [TestMethod]
    public void CompletionState_NoMoreDurability()
    {
        var input = MakeInput(maxDurability: 10);
        var sim = new SimulatorNoRandom { State = new SimulationState(input) };
        var state = new SimulationState(input);
        state.Durability = 0;
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.AreEqual(CompletionState.NoMoreDurability, sim2.CompletionState);
    }

    [TestMethod]
    public void CompletionState_ActionAfterCompleteReturnsSimulationComplete()
    {
        var input = MakeInput(maxProgress: 1);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.BasicSynthesis]);
        var (resp, _, _) = new SimulatorNoRandom().ExecuteMultiple(state, [ActionType.BasicSynthesis]);
        Assert.AreEqual(ActionResponse.SimulationComplete, resp);
    }

    // ---- FinalAppraisal ----

    [TestMethod]
    public void FinalAppraisal_PreventsProgressCompletion()
    {
        var input = MakeInput(maxProgress: 1);
        var sim = new SimulatorNoRandom();
        // FinalAppraisal ensures progress caps at MaxProgress - 1
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input),
        [
            ActionType.FinalAppraisal,
            ActionType.BasicSynthesis,
        ]);
        Assert.AreEqual(0, state.Progress); // MaxProgress-1 = 0 since MaxProgress=1
        Assert.AreEqual(CompletionState.Incomplete, new SimulatorNoRandom { State = state }.CompletionState);
    }

    [TestMethod]
    public void FinalAppraisal_IsRemovedWhenTriggered()
    {
        var input = MakeInput(maxProgress: 1);
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input),
        [
            ActionType.FinalAppraisal,
            ActionType.BasicSynthesis,
        ]);
        Assert.IsFalse(state.ActiveEffects.HasEffect(EffectType.FinalAppraisal));
    }

    // ---- SimulationState: HQPercent and Collectability ----

    [TestMethod]
    public void SimulationState_HQPercent_ZeroQualityIsMinimum()
    {
        var state = new SimulationState(MakeInput());
        Assert.AreEqual(1, state.HQPercent);
    }

    [TestMethod]
    public void SimulationState_HQPercent_MaxQualityIs100()
    {
        var input = MakeInput(maxQuality: 1000);
        var state = new SimulationState(input);
        state.Quality = 1000;
        Assert.AreEqual(100, state.HQPercent);
    }

    [TestMethod]
    public void SimulationState_Collectability_IsQualityDividedBy10()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Quality = 500;
        Assert.AreEqual(50, state.Collectability);
    }

    [TestMethod]
    public void SimulationState_Collectability_MinimumIsOne()
    {
        var state = new SimulationState(MakeInput());
        Assert.AreEqual(1, state.Collectability); // quality=0 → max(0/10,1) = 1
    }

    [TestMethod]
    public void SimulationState_MaxCollectability()
    {
        var input = MakeInput(maxQuality: 7200);
        var state = new SimulationState(input);
        Assert.AreEqual(720, state.MaxCollectability);
    }

    // ---- Condition modifiers ----

    [TestMethod]
    public void Condition_PliantReducesCPCost()
    {
        var input = MakeInput(cp: 575);
        var state = new SimulationState(input);
        state.Condition = Condition.Pliant;
        // BasicSynthesis costs 0 CP anyway; use Innovation (18 CP) to observe Pliant
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.Innovation);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        // Pliant halves CP cost → ceil(18/2) = 9
        Assert.AreEqual(575 - 9, outState.CP);
    }

    [TestMethod]
    public void Condition_SturdyReducesDurabilityCost()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Condition = Condition.Sturdy;
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.BasicSynthesis);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        // Sturdy halves durability cost → ceil(10/2) = 5
        Assert.AreEqual(75, outState.Durability); // 80 - 5 = 75
    }

    [TestMethod]
    public void Condition_CenteredBoostsSuccessRate()
    {
        // CalculateSuccessRate should add 25 under Centered
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Condition = Condition.Centered;
        var sim = new SimulatorNoRandom { State = state };
        // Centered: 50 base + 25 = 75; clamped to [0,100]
        Assert.AreEqual(75, sim.CalculateSuccessRate(50));
    }

    [TestMethod]
    public void Condition_ExcellentBoostsQualityGain()
    {
        var input = MakeInput();
        var normalState = new SimulationState(input);
        var excellentState = new SimulationState(input);
        excellentState.Condition = Condition.Excellent;

        var normalSim = new SimulatorNoRandom { State = normalState };
        var excellentSim = new SimulatorNoRandom { State = excellentState };

        var normalGain = normalSim.CalculateQualityGain(100);
        var excellentGain = excellentSim.CalculateQualityGain(100);
        Assert.IsTrue(excellentGain > normalGain, "Excellent should boost quality gain");
        Assert.AreEqual(excellentGain, normalGain * 4, "Excellent = 400% modifier");
    }

    [TestMethod]
    public void Condition_PoorHalvesQualityGain()
    {
        var input = MakeInput();
        var normalState = new SimulationState(input);
        var poorState = new SimulationState(input);
        poorState.Condition = Condition.Poor;

        var normalGain = new SimulatorNoRandom { State = normalState }.CalculateQualityGain(100);
        var poorGain = new SimulatorNoRandom { State = poorState }.CalculateQualityGain(100);
        Assert.AreEqual(normalGain / 2, poorGain);
    }

    [TestMethod]
    public void Condition_MalleableBoostsProgressGain()
    {
        var input = MakeInput();
        var normalState = new SimulationState(input);
        var malleableState = new SimulationState(input);
        malleableState.Condition = Condition.Malleable;

        var normalGain = new SimulatorNoRandom { State = normalState }.CalculateProgressGain(100);
        var malleableGain = new SimulatorNoRandom { State = malleableState }.CalculateProgressGain(100);
        Assert.IsTrue(malleableGain > normalGain, "Malleable should boost progress gain");
    }

    // ---- ActionResponse edge cases ----

    [TestMethod]
    public void Execute_ActionNotUnlocked_LowLevel()
    {
        // TrainedPerfection requires level 100
        var input = MakeInput(level: 50);
        var sim = new SimulatorNoRandom();
        var (resp, _, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.TrainedPerfection]);
        Assert.AreEqual(ActionResponse.ActionNotUnlocked, resp);
    }

    [TestMethod]
    public void Execute_ActionNotUnlocked_SpecialistRequired()
    {
        // HeartAndSoul requires specialist
        var input = MakeInput(level: 90);
        var sim = new SimulatorNoRandom();
        var (resp, _, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.HeartAndSoul]);
        Assert.AreEqual(ActionResponse.ActionNotUnlocked, resp);
    }

    [TestMethod]
    public void Execute_NotEnoughCP()
    {
        var input = new SimulationInput(
            new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 10, Level = 90, CanUseManipulation = true },
            new RecipeInfo { ClassJobLevel = 90, MaxDurability = 80, MaxQuality = 7200, MaxProgress = 3500, QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130 });
        var sim = new SimulatorNoRandom();
        // Innovation costs 18 CP but we only have 10
        var (resp, _, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.Innovation]);
        Assert.AreEqual(ActionResponse.NotEnoughCP, resp);
    }

    [TestMethod]
    public void Execute_CannotUseAction_ByregotsWithoutInnerQuiet()
    {
        // ByregotsBlessing requires InnerQuiet > 0
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var (resp, _, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.ByregotsBlessing]);
        Assert.AreEqual(ActionResponse.CannotUseAction, resp);
    }

    // ---- RestoreDurability clamp ----

    [TestMethod]
    public void RestoreDurability_ClampsToMax()
    {
        var input = MakeInput(maxDurability: 80);
        var state = new SimulationState(input); // starts at 80
        var sim = new SimulatorNoRandom { State = state };
        sim.RestoreDurability(50);
        Assert.AreEqual(80, sim.Durability);
    }

    // ---- RestoreCP clamp ----

    [TestMethod]
    public void RestoreCP_ClampsToMax()
    {
        var input = MakeInput(cp: 100);
        var state = new SimulationState(input);
        var sim = new SimulatorNoRandom { State = state };
        sim.RestoreCP(9999);
        Assert.AreEqual(100, sim.CP);
    }

    // ---- TrainedPerfection zeroes durability cost ----

    [TestMethod]
    public void TrainedPerfection_ZeroesDurabilityCost()
    {
        var input = MakeInput(level: 100);
        var sim = new SimulatorNoRandom();
        var (_, stateAfterTP, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.TrainedPerfection]);
        // Next action (BasicSynthesis) should cost 0 durability
        var durBefore = stateAfterTP.Durability;
        var (resp, outState) = new SimulatorNoRandom { State = stateAfterTP }.Execute(stateAfterTP, ActionType.BasicSynthesis);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.AreEqual(durBefore, outState.Durability, "TrainedPerfection should prevent durability loss");
    }

    // ---- Primed extends buff duration ----

    [TestMethod]
    public void Condition_Primed_ExtendsBuff()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Condition = Condition.Primed;
        // Innovation normally adds 4 steps; Primed adds 2 more → 6
        var (resp, outState) = new SimulatorNoRandom { State = state }.Execute(state, ActionType.Innovation);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.AreEqual(6, outState.ActiveEffects.GetDuration(EffectType.Innovation));
    }
}
