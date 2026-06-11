namespace Artificer.Test.Solver;

/// <summary>
/// Tests for SimulationNode, MCTSConfig, SolverSolution and RootScores.
/// </summary>
[TestClass]
public class SolverNodeTests
{
    private static SimulationInput MakeInput(int maxProgress = 3500, int maxQuality = 7200) =>
        new(new Artificer.Simulator.CharacterStats
        {
            Craftsmanship = 3304,
            Control = 3374,
            CP = 575,
            Level = 90,
            CanUseManipulation = true,
        },
        new Artificer.Simulator.RecipeInfo
        {
            ClassJobLevel = 90,
            MaxDurability = 80,
            MaxQuality = maxQuality,
            MaxProgress = maxProgress,
            QualityModifier = 80,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130,
        });

    private static MCTSConfig DefaultConfig()
    {
        var solverConfig = new SolverConfig
        {
            MaxStepCount = 30,
            ScoreProgress = 1,
            ScoreQuality = 1,
            ScoreDurability = 0.1f,
            ScoreCP = 0.1f,
            ScoreSteps = 0.1f,
            ActionPool = [ActionType.BasicSynthesis],
        };
        return new MCTSConfig(solverConfig);
    }

    // ---- SimulationNode.GetCompletionState ----

    [TestMethod]
    public void GetCompletionState_NoMoreActionsWhenEmptyAndIncomplete()
    {
        var emptyActions = new ActionSet();
        var result = SimulationNode.GetCompletionState(CompletionState.Incomplete, emptyActions);
        Assert.AreEqual(CompletionState.NoMoreActions, result);
    }

    [TestMethod]
    public void GetCompletionState_ProgressCompleteIgnoresEmptyActions()
    {
        var emptyActions = new ActionSet();
        var result = SimulationNode.GetCompletionState(CompletionState.ProgressComplete, emptyActions);
        Assert.AreEqual(CompletionState.ProgressComplete, result);
    }

    [TestMethod]
    public void GetCompletionState_IncompleteWithActions()
    {
        var actions = new ActionSet();
        actions.AddAction(ActionType.BasicSynthesis);
        var result = SimulationNode.GetCompletionState(CompletionState.Incomplete, actions);
        Assert.AreEqual(CompletionState.Incomplete, result);
    }

    // ---- SimulationNode.IsComplete ----

    [TestMethod]
    public void SimulationNode_IsComplete_TrueOnProgressComplete()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        var node = new SimulationNode(state, null, CompletionState.ProgressComplete, new ActionSet());
        Assert.IsTrue(node.IsComplete);
    }

    [TestMethod]
    public void SimulationNode_IsComplete_FalseWhenIncompleteWithActions()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        var actions = new ActionSet();
        actions.AddAction(ActionType.BasicSynthesis);
        var node = new SimulationNode(state, null, CompletionState.Incomplete, actions);
        Assert.IsFalse(node.IsComplete);
    }

    // ---- SimulationNode.CalculateScoreForState ----

    [TestMethod]
    public void CalculateScore_NullWhenNotProgressComplete()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.Incomplete, DefaultConfig());
        Assert.IsNull(score);
    }

    [TestMethod]
    public void CalculateScore_NullWhenNoDurability()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.NoMoreDurability, DefaultConfig());
        Assert.IsNull(score);
    }

    [TestMethod]
    public void CalculateScore_NonNullOnProgressComplete()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Progress = input.Recipe.MaxProgress;
        state.Quality = input.Recipe.MaxQuality;
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.ProgressComplete, DefaultConfig());
        Assert.IsNotNull(score);
        Assert.IsTrue(score.Value > 0);
    }

    [TestMethod]
    public void CalculateScore_ZeroQualityRecipeIgnoresQuality()
    {
        var input = MakeInput(maxQuality: 0);
        var state = new SimulationState(input);
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.ProgressComplete, DefaultConfig());
        Assert.IsNotNull(score);
    }

    // ---- MCTSConfig normalization ----

    [TestMethod]
    public void MCTSConfig_NormalizesScoreWeights()
    {
        var config = new SolverConfig
        {
            MaxStepCount = 30,
            ScoreProgress = 2,
            ScoreQuality = 2,
            ScoreDurability = 0,
            ScoreCP = 0,
            ScoreSteps = 0,
            ActionPool = [ActionType.BasicSynthesis],
        };
        var mcts = new MCTSConfig(config);
        // Each should be 0.5
        Assert.AreEqual(0.5f, mcts.ScoreProgress, 0.0001f);
        Assert.AreEqual(0.5f, mcts.ScoreQuality, 0.0001f);
        Assert.AreEqual(0f, mcts.ScoreDurability);
    }

    // ---- SolverSolution ----

    [TestMethod]
    public void SolverSolution_StoresActionsAndState()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        var solution = new SolverSolution(
            [ActionType.BasicSynthesis, ActionType.BasicTouch],
            state);

        Assert.AreEqual(2, solution.Actions.Count);
        Assert.AreEqual(ActionType.BasicSynthesis, solution.Actions[0]);
        Assert.AreEqual(ActionType.BasicTouch, solution.Actions[1]);
    }

    [TestMethod]
    public void SolverSolution_Deconstruct()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        var solution = new SolverSolution([ActionType.BasicSynthesis], state);

        var (actions, outState) = solution;
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(state, outState);
    }

    [TestMethod]
    public void SolverSolution_SanitizeCombo_ExpandsCombo()
    {
        var expanded = SolverSolution.SanitizeCombo(ActionType.StandardTouchCombo).ToList();
        // StandardTouchCombo = BasicTouch + StandardTouch
        Assert.AreEqual(2, expanded.Count);
        Assert.AreEqual(ActionType.BasicTouch, expanded[0]);
        Assert.AreEqual(ActionType.StandardTouch, expanded[1]);
    }

    [TestMethod]
    public void SolverSolution_SanitizeCombo_LeafAction()
    {
        var expanded = SolverSolution.SanitizeCombo(ActionType.BasicSynthesis).ToList();
        Assert.AreEqual(1, expanded.Count);
        Assert.AreEqual(ActionType.BasicSynthesis, expanded[0]);
    }

    [TestMethod]
    public void SolverSolution_SanitizeCombo_NestedCombo()
    {
        // AdvancedTouchCombo = StandardTouchCombo + AdvancedTouch = BasicTouch + StandardTouch + AdvancedTouch
        var expanded = SolverSolution.SanitizeCombo(ActionType.AdvancedTouchCombo).ToList();
        Assert.AreEqual(3, expanded.Count);
        Assert.AreEqual(ActionType.BasicTouch, expanded[0]);
        Assert.AreEqual(ActionType.StandardTouch, expanded[1]);
        Assert.AreEqual(ActionType.AdvancedTouch, expanded[2]);
    }

    // ---- RootScores ----

    [TestMethod]
    public void RootScores_TrackMaxScoreAndVisits()
    {
        var scores = new RootScores();
        Assert.AreEqual(0, scores.Visits);
        Assert.AreEqual(0f, scores.MaxScore);

        scores.Visit(0.8f);
        scores.Visit(0.5f);
        scores.Visit(0.9f);

        Assert.AreEqual(3, scores.Visits);
        Assert.AreEqual(0.9f, scores.MaxScore, 0.0001f);
    }
}
