namespace Artificer.Test.Solver;

[TestClass]
public class MCTSSolverTests
{
    private static SimulationInput EasyInput() =>
        new(new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 90, CanUseManipulation = true },
            new RecipeInfo
            {
                ClassJobLevel = 90, MaxDurability = 60, MaxQuality = 0, MaxProgress = 50,
                QualityModifier = 0, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130,
            });

    private static SolverConfig FastConfig(SolverAlgorithm algo = SolverAlgorithm.Oneshot) => new SolverConfig
    {
        MaxStepCount = 5,
        Iterations = 64,
        MaxIterations = 64,
        MaxRolloutStepCount = 5,
        ForkCount = 2,
        FurcatedActionCount = 1,
        MaxThreadCount = 2,
        StrictActions = false,
        ScoreProgress = 10,
        ScoreQuality = 1,
        ScoreDurability = 0,
        ScoreCP = 0,
        ScoreSteps = 1,
        Algorithm = algo,
        ActionPool = [ActionType.BasicSynthesis, ActionType.MuscleMemory],
    };

    private static MCTSConfig FastMCTSConfig() => new MCTSConfig(FastConfig());

    // ---- MCTS ----

    [TestMethod]
    public void MCTS_Search_CompletesSolution()
    {
        var state = new SimulationState(EasyInput());
        var mcts = new MCTS(FastMCTSConfig(), state);
        var progress = 0;
        mcts.Search(64, 64, ref progress, CancellationToken.None);

        var solution = mcts.Solution();
        Assert.IsNotNull(solution);
        Assert.IsTrue(solution.Actions.Count > 0);
    }

    [TestMethod]
    public void MCTS_MaxScore_NonZeroAfterSearch()
    {
        var state = new SimulationState(EasyInput());
        var mcts = new MCTS(FastMCTSConfig(), state);
        var progress = 0;
        mcts.Search(64, 64, ref progress, CancellationToken.None);

        Assert.IsTrue(mcts.MaxScore > 0);
    }

    [TestMethod]
    public void MCTS_Search_UpdatesProgress()
    {
        var state = new SimulationState(EasyInput());
        var mcts = new MCTS(FastMCTSConfig(), state);
        var progress = 0;
        mcts.Search(64, 64, ref progress, CancellationToken.None);

        Assert.IsTrue(progress > 0);
    }

    [TestMethod]
    public void MCTS_Search_CancellationThrows()
    {
        var state = new SimulationState(EasyInput());
        var mcts = new MCTS(FastMCTSConfig(), state);
        var progress = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() =>
            mcts.Search(100_000, 100_000, ref progress, cts.Token));
    }

    // ---- MCTSConfig.MaxThreadCount ----

    [TestMethod]
    public void MCTSConfig_MaxThreadCount_DefaultsToZero()
    {
        // MaxThreadCount is not copied from SolverConfig in MCTSConfig constructor
        var config = new MCTSConfig(FastConfig());
        Assert.AreEqual(0, config.MaxThreadCount);
    }

    // ---- SolverConfig.FilterSpecialistActions ----

    [TestMethod]
    public void SolverConfig_FilterSpecialistActions_RemovesSpecialists()
    {
        var config = new SolverConfig
        {
            ActionPool = [ActionType.BasicSynthesis, ActionType.HeartAndSoul, ActionType.QuickInnovation, ActionType.CarefulObservation],
        };
        var filtered = config.FilterSpecialistActions();

        CollectionAssert.Contains(filtered.ActionPool, ActionType.BasicSynthesis);
        CollectionAssert.DoesNotContain(filtered.ActionPool, ActionType.HeartAndSoul);
        CollectionAssert.DoesNotContain(filtered.ActionPool, ActionType.QuickInnovation);
        CollectionAssert.DoesNotContain(filtered.ActionPool, ActionType.CarefulObservation);
    }

    [TestMethod]
    public void SolverConfig_FilterSpecialistActions_NoSpecialists_Unchanged()
    {
        var config = new SolverConfig { ActionPool = [ActionType.BasicSynthesis, ActionType.BasicTouch] };
        var filtered = config.FilterSpecialistActions();
        Assert.AreEqual(2, filtered.ActionPool.Length);
    }

    // ---- SimulationNode.CalculateScore instance method ----

    [TestMethod]
    public void SimulationNode_CalculateScore_NullWhenIncomplete()
    {
        var state = new SimulationState(EasyInput());
        var node = new SimulationNode(state, null, CompletionState.Incomplete, new ActionSet());
        var config = FastMCTSConfig();
        Assert.IsNull(node.CalculateScore(config));
    }

    [TestMethod]
    public void SimulationNode_CalculateScore_NonNullWhenProgressComplete()
    {
        var input = EasyInput();
        var state = new SimulationState(input);
        state.Progress = input.Recipe.MaxProgress;
        var node = new SimulationNode(state, null, CompletionState.ProgressComplete, new ActionSet());
        var config = FastMCTSConfig();
        Assert.IsNotNull(node.CalculateScore(config));
        Assert.IsTrue(node.CalculateScore(config)!.Value > 0);
    }

    // ---- Solver algorithms ----

    [TestMethod]
    public async Task Solver_Oneshot_Completes()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.Oneshot), state);
        solver.Start();
        var solution = await solver.GetSafeTask();
        Assert.IsNotNull(solution);
        Assert.IsTrue(solution.Value.Actions.Count > 0);
    }

    [TestMethod]
    public async Task Solver_OneshotForked_Completes()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.OneshotForked), state);
        solver.Start();
        var solution = await solver.GetSafeTask();
        Assert.IsNotNull(solution);
    }

    [TestMethod]
    public async Task Solver_Stepwise_Completes()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.Stepwise), state);
        solver.Start();
        var solution = await solver.GetSafeTask();
        Assert.IsNotNull(solution);
        Assert.IsTrue(solution.Value.Actions.Count > 0);
    }

    [TestMethod]
    public async Task Solver_StepwiseForked_Completes()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.StepwiseForked), state);
        solver.Start();
        var solution = await solver.GetSafeTask();
        Assert.IsNotNull(solution);
    }

    [TestMethod]
    public async Task Solver_StepwiseGenetic_Completes()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.StepwiseGenetic), state);
        solver.Start();
        var solution = await solver.GetSafeTask();
        Assert.IsNotNull(solution);
        Assert.IsTrue(solution.Value.Actions.Count > 0);
    }

    [TestMethod]
    public async Task Solver_Raphael_FallbackWhenActionsPresent()
    {
        // When ActionCount > 0, Raphael falls back to StepwiseGenetic
        var input = EasyInput();
        var state = new SimulationState(input);
        state.ActionCount = 1;

        var config = FastConfig(SolverAlgorithm.Raphael);
        using var solver = new Artificer.Solver.Solver(config, state);

        var warned = false;
        solver.OnWarn += _ => warned = true;
        solver.Start();

        var solution = await solver.GetSafeTask();
        Assert.IsTrue(warned, "Should have warned about falling back");
        Assert.IsNotNull(solution);
    }

    // ---- Solver lifecycle ----

    [TestMethod]
    public void Solver_StartTwice_Throws()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(), state);
        solver.Start();
        Assert.ThrowsException<InvalidOperationException>(() => solver.Start());
    }

    [TestMethod]
    public async Task Solver_GetTaskBeforeStart_Throws()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(), state);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => solver.GetTask());
    }

    [TestMethod]
    public async Task Solver_Cancellation_GetSafeTaskReturnsNull()
    {
        var state = new SimulationState(EasyInput());
        using var cts = new CancellationTokenSource();
        using var solver = new Artificer.Solver.Solver(
            FastConfig(SolverAlgorithm.StepwiseGenetic) with { Iterations = 100_000, MaxIterations = 100_000, ForkCount = 32, FurcatedActionCount = 16 },
            state) { Token = cts.Token };

        solver.Start();
        cts.Cancel();
        var solution = await solver.GetSafeTask();
        Assert.IsNull(solution);
    }

    // ---- Solver events ----

    [TestMethod]
    public async Task Solver_OnLog_Fires()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(), state);

        var logged = false;
        solver.OnLog += _ => logged = true;
        solver.Start();
        await solver.GetSafeTask();

        Assert.IsTrue(logged, "OnLog should fire at least once");
    }

    [TestMethod]
    public async Task Solver_OnNewAction_Fires()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(), state);

        var actions = new List<ActionType>();
        solver.OnNewAction += a => actions.Add(a);
        solver.Start();
        await solver.GetSafeTask();

        Assert.IsTrue(actions.Count > 0, "OnNewAction should fire at least once");
    }

    // ---- Solver progress/state properties ----

    [TestMethod]
    public void Solver_Oneshot_ProgressStage_IsNull()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.Oneshot), state);
        Assert.IsNull(solver.ProgressStage);
    }

    [TestMethod]
    public void Solver_Stepwise_ProgressStage_IsNotNull()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.Stepwise), state);
        Assert.IsNotNull(solver.ProgressStage);
        Assert.AreEqual(0, solver.ProgressStage);
    }

    [TestMethod]
    public void Solver_IsIndeterminate_BeforeStart()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(SolverAlgorithm.Stepwise), state);
        Assert.IsTrue(solver.IsIndeterminate);
    }

    [TestMethod]
    public void Solver_IsStarted_FalseBeforeStart()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(), state);
        Assert.IsFalse(solver.IsStarted);
    }

    [TestMethod]
    public async Task Solver_IsCompletedSuccessfully_TrueAfterCompletion()
    {
        var state = new SimulationState(EasyInput());
        using var solver = new Artificer.Solver.Solver(FastConfig(), state);
        solver.Start();
        await solver.GetSafeTask();
        Assert.IsTrue(solver.IsCompletedSuccessfully);
    }
}
