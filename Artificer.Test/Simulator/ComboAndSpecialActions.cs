namespace Artificer.Test.Simulator;

/// <summary>
/// Tests for combo actions, TrainedEye, and CarefulObservation behaviour.
/// </summary>
[TestClass]
public class ComboAndSpecialActionsTests
{
    private static SimulationInput MakeInput(
        int level = 90,
        int craftsmanship = 3304,
        int control = 3374,
        int cp = 575,
        int maxProgress = 3500,
        int maxQuality = 7200,
        int maxDurability = 80,
        bool isSpecialist = false,
        bool isExpert = false) =>
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
            MaxDurability = maxDurability,
            MaxQuality = maxQuality,
            MaxProgress = maxProgress,
            QualityModifier = 80,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130,
        });

    // ---- TrainedEye ----

    [TestMethod]
    public void TrainedEye_MaximisesQualityOnFirstStep()
    {
        // Requires player level >= recipeLevel + 10
        // Use level 100, recipe level 80
        var input = new SimulationInput(
            new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 100, CanUseManipulation = true },
            new RecipeInfo { ClassJobLevel = 80, MaxDurability = 80, MaxQuality = 7200, MaxProgress = 3500, QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130 });
        var sim = new SimulatorNoRandom();
        var (resp, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.TrainedEye]);
        Assert.AreEqual(ActionResponse.UsedAction, resp);
        Assert.AreEqual(7200, state.Quality);
    }

    [TestMethod]
    public void TrainedEye_CannotUseWhenLevelTooLow()
    {
        // Same level as recipe → not unlocked
        var input = MakeInput(level: 90);
        var sim = new SimulatorNoRandom { State = new SimulationState(input) };
        Assert.IsFalse(ActionType.TrainedEye.Base().IsPossible(sim));
    }

    [TestMethod]
    public void TrainedEye_CannotUseOnExpertRecipe()
    {
        var input = new SimulationInput(
            new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 100, CanUseManipulation = true },
            new RecipeInfo { IsExpert = true, ClassJobLevel = 80, MaxDurability = 80, MaxQuality = 7200, MaxProgress = 3500, QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130 });
        var sim = new SimulatorNoRandom { State = new SimulationState(input) };
        Assert.IsFalse(ActionType.TrainedEye.Base().IsPossible(sim));
    }

    [TestMethod]
    public void TrainedEye_CannotUseAfterFirstStep()
    {
        var input = new SimulationInput(
            new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 100, CanUseManipulation = true },
            new RecipeInfo { ClassJobLevel = 80, MaxDurability = 80, MaxQuality = 7200, MaxProgress = 3500, QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130 });
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.BasicTouch]);
        // Now it's step 1
        var sim2 = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.TrainedEye.Base().CouldUse(sim2));
    }

    // ---- StandardTouchCombo ----

    [TestMethod]
    public void StandardTouchCombo_ExecutesBothActions()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var initial = new SimulationState(input);

        // Execute combo in one call vs two separate actions
        var (_, stateCombo, _) = sim.ExecuteMultiple(initial, [ActionType.StandardTouchCombo]);
        var (_, stateSeparate, _) = sim.ExecuteMultiple(initial,
        [
            ActionType.BasicTouch,
            ActionType.StandardTouch,
        ]);

        Assert.AreEqual(stateSeparate.Quality, stateCombo.Quality);
        Assert.AreEqual(stateSeparate.Progress, stateCombo.Progress);
        Assert.AreEqual(stateSeparate.Durability, stateCombo.Durability);
    }

    [TestMethod]
    public void StandardTouchCombo_CannotUseWithInsufficientDurability()
    {
        var input = MakeInput(maxDurability: 15);
        var state = new SimulationState(input);
        state.Durability = 10; // Less than first action cost (10) → combo fails VerifyDurability2
        var sim = new SimulatorNoRandom { State = state };
        Assert.IsFalse(ActionType.StandardTouchCombo.Base().CouldUse(sim));
    }

    // ---- AdvancedTouchCombo ----

    [TestMethod]
    public void AdvancedTouchCombo_ExecutesThreeActions()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var initial = new SimulationState(input);

        var (_, stateCombo, _) = sim.ExecuteMultiple(initial, [ActionType.AdvancedTouchCombo]);
        var (_, stateSeparate, _) = sim.ExecuteMultiple(initial,
        [
            ActionType.BasicTouch,
            ActionType.StandardTouch,
            ActionType.AdvancedTouch,
        ]);

        Assert.AreEqual(stateSeparate.Quality, stateCombo.Quality);
        Assert.AreEqual(stateSeparate.Durability, stateCombo.Durability);
    }

    // ---- ObservedAdvancedTouchCombo ----

    [TestMethod]
    public void ObservedAdvancedTouchCombo_ExecutesObserveThenAdvancedTouch()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var initial = new SimulationState(input);

        var (_, stateCombo, _) = sim.ExecuteMultiple(initial, [ActionType.ObservedAdvancedTouchCombo]);
        var (_, stateSeparate, _) = sim.ExecuteMultiple(initial,
        [
            ActionType.Observe,
            ActionType.AdvancedTouch,
        ]);

        Assert.AreEqual(stateSeparate.Quality, stateCombo.Quality);
        Assert.AreEqual(stateSeparate.Durability, stateCombo.Durability);
    }

    // ---- RefinedTouchCombo ----

    [TestMethod]
    public void RefinedTouchCombo_ExecutesBasicTouchThenRefinedTouch()
    {
        var input = MakeInput(level: 92);
        var sim = new SimulatorNoRandom();
        var initial = new SimulationState(input);

        var (_, stateCombo, _) = sim.ExecuteMultiple(initial, [ActionType.RefinedTouchCombo]);
        var (_, stateSeparate, _) = sim.ExecuteMultiple(initial,
        [
            ActionType.BasicTouch,
            ActionType.RefinedTouch,
        ]);

        Assert.AreEqual(stateSeparate.Quality, stateCombo.Quality);
        Assert.AreEqual(stateSeparate.ActiveEffects.InnerQuiet, stateCombo.ActiveEffects.InnerQuiet);
    }

    // ---- BaseComboAction.VerifyDurability3 (static) ----

    [TestMethod]
    public void VerifyDurability3_FalseWhenDurabilityZeroAfterFirst()
    {
        // durabilityA=10, durabilityB=10, durability=10, no effects
        var result = BaseComboAction.VerifyDurability3(10, 10, 10, new Effects());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void VerifyDurability3_TrueWithEnoughDurability()
    {
        // durabilityA=10, durabilityB=10, durability=25 → after first=15, after manip=15, after second=5 > 0
        var result = BaseComboAction.VerifyDurability3(10, 10, 25, new Effects());
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyDurability3_TrueWithWasteNot()
    {
        var effects = new Effects();
        effects.SetDuration(EffectType.WasteNot, 4);
        // WasteNot halves costs: ceil(10/2)=5 each; 15 - 5 = 10, 10 - 5 = 5 > 0
        var result = BaseComboAction.VerifyDurability3(10, 10, 15, effects);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyDurability3_TrueWithManipulation()
    {
        var effects = new Effects();
        effects.SetDuration(EffectType.Manipulation, 4);
        // After first (10 dur): 10 - 10 = 0, but manip adds 5 → 5; after second: 5 - 10 = -5 ≤ 0
        // Actually: 10 - 10 = 0, ≤ 0 returns false before manip
        // Wait: the code checks durability after first then adds manip if present
        // durability=20, first cost=10: 20-10=10 > 0 → ok; manip: 10+5=15; second cost=10: 15-10=5 > 0 → true
        var result = BaseComboAction.VerifyDurability3(10, 10, 20, effects);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void VerifyDurability3_FalseWithTrainedPerfectionSkipsFirst()
    {
        var effects = new Effects();
        effects.SetDuration(EffectType.TrainedPerfection, 1);
        // TrainedPerfection skips first action's durability cost entirely
        // But second still costs: durability=5, second cost=10, wasteNots=0 (decremented from 0)
        // 5 - 10 = -5 ≤ 0 → false
        var result = BaseComboAction.VerifyDurability3(10, 10, 5, effects);
        Assert.IsFalse(result);
    }

    // ---- ActionStates combo tracking ----

    [TestMethod]
    public void ActionStates_ComboState_BasicTouchSetsUsedBasicTouch()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.BasicTouch]);
        Assert.AreEqual(ActionProc.UsedBasicTouch, state.ActionStates.Combo);
    }

    [TestMethod]
    public void ActionStates_ComboState_ObserveSetsAdvancedTouch()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input), [ActionType.Observe]);
        Assert.AreEqual(ActionProc.AdvancedTouch, state.ActionStates.Combo);
    }

    [TestMethod]
    public void ActionStates_ComboState_BasicTouchThenStandardSetsAdvancedTouch()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input),
        [
            ActionType.BasicTouch,
            ActionType.StandardTouch,
        ]);
        Assert.AreEqual(ActionProc.AdvancedTouch, state.ActionStates.Combo);
    }

    [TestMethod]
    public void ActionStates_ComboState_NonComboActionResetsToNone()
    {
        var input = MakeInput();
        var sim = new SimulatorNoRandom();
        var (_, state, _) = sim.ExecuteMultiple(new SimulationState(input),
        [
            ActionType.BasicTouch,
            ActionType.BasicSynthesis, // not a combo action
        ]);
        Assert.AreEqual(ActionProc.None, state.ActionStates.Combo);
    }
}
