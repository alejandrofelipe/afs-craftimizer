using Artificer.Application.Crafting;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.Application.Crafting;

[TestClass]
public class MacroScoringTests
{
    private static SimulationInput MakeInput(int maxProgress = 3500, int maxQuality = 7200) =>
        new(new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 90, CanUseManipulation = true },
            new RecipeInfo
            {
                ClassJobLevel = 90, MaxDurability = 80, MaxQuality = maxQuality, MaxProgress = maxProgress,
                QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130,
            });

    private static MCTSConfig Config(int maxQuality = 7200) =>
        new(new SolverConfig { MaxStepCount = 30, QualityTargetPercent = 100, ActionPool = [ActionType.BasicSynthesis] },
            MakeInput(maxQuality: maxQuality).Recipe);

    [TestMethod]
    public void ScoreState_IncompleteCraft_ReturnsZero()
    {
        var state = new SimulationState(MakeInput()); // Progress 0 => incompleto
        Assert.AreEqual(0f, MacroScoring.ScoreState(state, Config()), 0.0001f);
    }

    [TestMethod]
    public void ScoreState_CompleteAtTarget_ReturnsNearOne()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Progress = input.Recipe.MaxProgress; // completa
        state.Quality = input.Recipe.MaxQuality;   // no target (100%)
        var score = MacroScoring.ScoreState(state, Config());
        Assert.IsTrue(score > 0.9f, $"esperado ~1.0, obtido {score}");
    }

    [TestMethod]
    public void ScoreActions_NonCompletingSequence_ScoresZero()
    {
        // Uma sequência só de toques não completa a síntese => não-ProgressComplete
        // => CalculateScoreForState retorna null => wrapper devolve 0.
        var input = MakeInput();
        var start = new SimulationState(input);
        var sim = new SimulatorNoRandom();

        var incomplete = MacroScoring.ScoreActions(
            new[] { ActionType.BasicTouch, ActionType.BasicTouch }, sim, start, Config());

        Assert.AreEqual(0f, incomplete, 0.0001f, "sequência que não completa deve pontuar 0");
    }
}
