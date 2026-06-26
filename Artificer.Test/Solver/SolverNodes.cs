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

    private static MCTSConfig DefaultConfig(int maxQuality = 7200, int qualityTargetPercent = 100)
    {
        var solverConfig = new SolverConfig
        {
            MaxStepCount = 30,
            QualityTargetPercent = qualityTargetPercent,
            ActionPool = [ActionType.BasicSynthesis],
        };
        return new MCTSConfig(solverConfig, MakeInput(maxQuality: maxQuality).Recipe);
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

    // ---- SimulationNode.CalculateScoreForState (objetivo lexicográfico) ----

    [TestMethod]
    public void CalculateScore_NullWhenNotProgressComplete()
    {
        var state = new SimulationState(MakeInput());
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.Incomplete, DefaultConfig());
        Assert.IsNull(score);
    }

    [TestMethod]
    public void CalculateScore_NullWhenNoDurability()
    {
        var state = new SimulationState(MakeInput());
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.NoMoreDurability, DefaultConfig());
        Assert.IsNull(score);
    }

    [TestMethod]
    public void CalculateScore_NonNullOnProgressComplete()
    {
        var input = MakeInput();
        var state = new SimulationState(input);
        state.Quality = input.Recipe.MaxQuality;
        var score = SimulationNode.CalculateScoreForState(state, CompletionState.ProgressComplete, DefaultConfig());
        Assert.IsNotNull(score);
        Assert.IsTrue(score.Value > 0);
    }

    [TestMethod]
    public void CalculateScore_ZeroQualityRecipe_OnlyScoresSteps()
    {
        // Sem quality (target 0): o único objetivo é menos passos.
        var input = MakeInput(maxQuality: 0);
        var config = DefaultConfig(maxQuality: 0);

        var few = new SimulationState(input); // ActionCount = 0
        var many = new SimulationState(input);
        many.ActionCount = 10;

        var fewScore = SimulationNode.CalculateScoreForState(few, CompletionState.ProgressComplete, config);
        var manyScore = SimulationNode.CalculateScoreForState(many, CompletionState.ProgressComplete, config);

        Assert.IsNotNull(fewScore);
        Assert.IsNotNull(manyScore);
        Assert.IsTrue(fewScore.Value > manyScore.Value, "menos passos deve pontuar mais");
    }

    [TestMethod]
    public void CalculateScore_QualityStrictlyDominatesSteps()
    {
        // Um ponto a mais de quality vale mais que qualquer ganho de passos: um craft com +quality e
        // MAIS passos deve pontuar acima de um com -quality e MENOS passos.
        var input = MakeInput();
        var config = DefaultConfig();

        var moreQualityMoreSteps = new SimulationState(input);
        moreQualityMoreSteps.Quality = input.Recipe.MaxQuality;
        moreQualityMoreSteps.ActionCount = 20;

        var lessQualityFewerSteps = new SimulationState(input);
        lessQualityFewerSteps.Quality = input.Recipe.MaxQuality - 1;
        lessQualityFewerSteps.ActionCount = 0;

        var a = SimulationNode.CalculateScoreForState(moreQualityMoreSteps, CompletionState.ProgressComplete, config);
        var b = SimulationNode.CalculateScoreForState(lessQualityFewerSteps, CompletionState.ProgressComplete, config);

        Assert.IsTrue(a!.Value > b!.Value, "quality deve dominar estritamente o número de passos");
    }

    [TestMethod]
    public void CalculateScore_QualityClampsAtTarget()
    {
        // Quality além do target não aumenta o score (qualityFrac satura em 1).
        var input = MakeInput();
        var config = DefaultConfig(qualityTargetPercent: 50); // target = 50% de MaxQuality

        var atTarget = new SimulationState(input);
        atTarget.Quality = input.Recipe.MaxQuality / 2;
        var beyondTarget = new SimulationState(input);
        beyondTarget.Quality = input.Recipe.MaxQuality; // bem acima do target de 50%

        var atScore = SimulationNode.CalculateScoreForState(atTarget, CompletionState.ProgressComplete, config);
        var beyondScore = SimulationNode.CalculateScoreForState(beyondTarget, CompletionState.ProgressComplete, config);

        Assert.AreEqual(atScore!.Value, beyondScore!.Value, 0.0001f, "quality acima do target não deve aumentar o score");
    }

    [TestMethod]
    public void CalculateScore_DurabilityAndCpDoNotAffectScore()
    {
        // Durabilidade e CP foram removidos do objetivo: estados idênticos exceto dur/CP pontuam igual.
        var input = MakeInput();
        var config = DefaultConfig();

        var lowDurCp = new SimulationState(input);
        lowDurCp.Quality = input.Recipe.MaxQuality;
        lowDurCp.Durability = 1;
        lowDurCp.CP = 0;

        var highDurCp = new SimulationState(input);
        highDurCp.Quality = input.Recipe.MaxQuality;
        highDurCp.Durability = input.Recipe.MaxDurability;
        highDurCp.CP = input.Stats.CP;

        var lowScore = SimulationNode.CalculateScoreForState(lowDurCp, CompletionState.ProgressComplete, config);
        var highScore = SimulationNode.CalculateScoreForState(highDurCp, CompletionState.ProgressComplete, config);

        Assert.AreEqual(lowScore!.Value, highScore!.Value, 0.0001f, "durabilidade/CP não devem influenciar o score");
    }

    // ---- SolverConfig.ResolveQualityTarget ----

    [TestMethod]
    public void ResolveQualityTarget_PercentOfMaxQuality()
    {
        var config = new SolverConfig { QualityTargetPercent = 50, QualityTargetToMaxCollectability = false };
        var recipe = MakeInput(maxQuality: 1000).Recipe;
        Assert.AreEqual(500, config.ResolveQualityTarget(recipe));
    }

    [TestMethod]
    public void ResolveQualityTarget_ZeroQualityRecipe_ReturnsZero()
    {
        var config = new SolverConfig { QualityTargetPercent = 100 };
        var recipe = MakeInput(maxQuality: 0).Recipe;
        Assert.AreEqual(0, config.ResolveQualityTarget(recipe));
    }

    [TestMethod]
    public void ResolveQualityTarget_CapsToCollectableTarget()
    {
        var config = new SolverConfig { QualityTargetPercent = 100, QualityTargetToMaxCollectability = true };
        var recipe = MakeInput(maxQuality: 1000).Recipe with { CollectableTargetQuality = 300 };
        Assert.AreEqual(300, config.ResolveQualityTarget(recipe));
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
