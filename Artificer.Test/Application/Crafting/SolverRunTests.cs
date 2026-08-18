using Artificer.Application.Crafting;
using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Artificer.Utils;
using Dalamud.Plugin.Services;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;

namespace Artificer.Test.Application.Crafting;

internal static class DalamudAssemblyResolver
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var dalamudDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", "addon", "Hooks", "dev");

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var assemblyPath = Path.Combine(dalamudDirectory, $"{name.Name}.dll");
            return File.Exists(assemblyPath) ? context.LoadFromAssemblyPath(assemblyPath) : null;
        };
    }
}

[TestClass]
public class SolverRunTests
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(5);

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        var pluginLog = DispatchProxy.Create<IPluginLog, NullPluginLogProxy>();
        var backingField = typeof(Service).GetField(
            "<PluginLog>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField);
        backingField.SetValue(null, pluginLog);
    }

    [TestMethod]
    public void Run_StaleGeneration_DoesNotReplaceCurrentSnapshot()
    {
        using var run = new SolverRun();
        var staleGeneration = run.Begin(Indeterminate("first"));
        _ = run.Begin(Indeterminate("second"));

        run.Run(FastConfig(), new SimulationState(EasyInput()), staleGeneration,
            CancellationToken.None, _ => true);

        Assert.AreEqual("second", run.Snapshots.Single().Name);
    }

    [TestMethod]
    public void Run_OverlappingGenerations_FirstEarlyStopDoesNotCancelSecond()
    {
        using var run = new SolverRun();
        using var firstCallbackEntered = new ManualResetEventSlim();
        using var secondCallbackEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        Task? firstTask = null;
        Task? secondTask = null;

        try
        {
            var firstGeneration = run.Begin(Indeterminate("first"));
            firstTask = Task.Run(() => run.Run(
                FastConfig(),
                new SimulationState(EasyInput()),
                firstGeneration,
                CancellationToken.None,
                _ =>
                {
                    firstCallbackEntered.Set();
                    Assert.IsTrue(releaseFirst.Wait(GateTimeout));
                    return false;
                }));

            Assert.IsTrue(firstCallbackEntered.Wait(GateTimeout));

            var secondGeneration = run.Begin(Indeterminate("second"));
            secondTask = Task.Run(() => run.Run(
                FastConfig(),
                new SimulationState(EasyInput()),
                secondGeneration,
                CancellationToken.None,
                _ =>
                {
                    secondCallbackEntered.Set();
                    Assert.IsTrue(releaseSecond.Wait(GateTimeout));
                    return false;
                }));

            Assert.IsTrue(secondCallbackEntered.Wait(GateTimeout));
            var secondSolver = run.Current;
            Assert.IsNotNull(secondSolver);

            releaseFirst.Set();
            firstTask.GetAwaiter().GetResult();

            Assert.AreSame(secondSolver, run.Current);
            Assert.IsFalse(secondSolver.Token.IsCancellationRequested);
            Assert.IsFalse(secondTask.IsCompleted);

            releaseSecond.Set();
            secondTask.GetAwaiter().GetResult();
        }
        finally
        {
            releaseFirst.Set();
            releaseSecond.Set();
            WaitForCleanup(firstTask);
            WaitForCleanup(secondTask);
        }
    }

    [TestMethod]
    public void Run_StaleFinalization_DoesNotReplaceNewGenerationSnapshots()
    {
        using var run = new SolverRun();
        var firstGeneration = run.Begin(Indeterminate("first"));
        var secondGeneration = 0L;

        run.Run(FastConfig(), new SimulationState(EasyInput()), firstGeneration,
            CancellationToken.None,
            _ =>
            {
                if (secondGeneration == 0)
                    secondGeneration = run.Begin(Indeterminate("second"));
                return true;
            });

        Assert.AreEqual("second", run.Snapshots.Single().Name);

        run.Run(FastConfig(SolverAlgorithm.Stepwise), new SimulationState(EasyInput()), secondGeneration,
            CancellationToken.None, _ => false);

        var finalSnapshot = run.Snapshots.Single();
        Assert.AreEqual(nameof(SolverAlgorithm.Stepwise), finalSnapshot.Name);
        Assert.AreEqual(ProgressBarComponent.ProgressState.Completed, finalSnapshot.State);
    }

    [TestMethod]
    public void Run_ReplacedGeneration_DoesNotDeliverStaleExternalCallbacks()
    {
        using var run = new SolverRun();
        var newActionCallbacks = 0;
        var suggestSolutionCallbacks = 0;
        var firstGeneration = run.Begin(Indeterminate("first"));

        run.Run(FastConfig(), new SimulationState(EasyInput(maxProgress: 10_000)), firstGeneration,
            CancellationToken.None,
            _ =>
            {
                if (Interlocked.Increment(ref newActionCallbacks) == 1)
                    run.Begin(Indeterminate("second"));
                return true;
            });

        var suggestionGeneration = run.Begin(Indeterminate("suggestion"));
        run.Run(FastConfig(SolverAlgorithm.NextActionForked), new SimulationState(EasyInput()), suggestionGeneration,
            CancellationToken.None,
            _ =>
            {
                run.Begin(Indeterminate("current"));
                return true;
            },
            _ => Interlocked.Increment(ref suggestSolutionCallbacks));

        Assert.AreEqual(1, Volatile.Read(ref newActionCallbacks));
        Assert.AreEqual(0, Volatile.Read(ref suggestSolutionCallbacks));
    }

    [TestMethod]
    public void Cancel_MarkCancelled_PreservesCancelledSnapshotAfterRunFinishes()
    {
        using var run = new SolverRun();
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        Task? task = null;

        try
        {
            var generation = run.Begin(Indeterminate("running"));
            task = Task.Run(() => run.Run(
                FastConfig(),
                new SimulationState(EasyInput()),
                generation,
                CancellationToken.None,
                _ =>
                {
                    callbackEntered.Set();
                    Assert.IsTrue(releaseCallback.Wait(GateTimeout));
                    return true;
                }));

            Assert.IsTrue(callbackEntered.Wait(GateTimeout));
            run.Cancel(markCancelled: true);
            Assert.AreEqual(ProgressBarComponent.ProgressState.Cancelled, run.Snapshots.Single().State);

            releaseCallback.Set();
            task.GetAwaiter().GetResult();

            Assert.AreEqual(ProgressBarComponent.ProgressState.Cancelled, run.Snapshots.Single().State);
        }
        finally
        {
            releaseCallback.Set();
            WaitForCleanup(task);
        }
    }

    [TestMethod]
    public void Run_CurrentEarlyStop_ProducesCompletedSnapshot()
    {
        using var run = new SolverRun();
        var generation = run.Begin(Indeterminate("running"));

        run.Run(FastConfig(), new SimulationState(EasyInput()), generation,
            CancellationToken.None, _ => false);

        Assert.AreEqual(ProgressBarComponent.ProgressState.Completed, run.Snapshots.Single().State);
    }

    [TestMethod]
    public void Run_EarlyStopSealsTerminalSnapshotBeforePausedPollerPublishes()
    {
        using var pollerSnapshotComputed = new ManualResetEventSlim();
        using var releasePollerSnapshot = new ManualResetEventSlim();
        using var run = new SolverRun(pollSnapshotFactory: (_, _) =>
        {
            var snapshot = new ProgressBarComponent.ProgressSnapshot(
                "stale poller", 1, 2, ProgressBarComponent.ProgressState.InProgress);
            pollerSnapshotComputed.Set();
            Assert.IsTrue(releasePollerSnapshot.Wait(GateTimeout));
            return snapshot;
        });
        Task? task = null;

        try
        {
            var generation = run.Begin(Indeterminate("running"));
            task = Task.Run(() => run.Run(
                FastConfig(SolverAlgorithm.Stepwise),
                new SimulationState(EasyInput()),
                generation,
                CancellationToken.None,
                _ =>
                {
                    Assert.IsTrue(pollerSnapshotComputed.Wait(GateTimeout));
                    return false;
                }));

            Assert.IsTrue(pollerSnapshotComputed.Wait(GateTimeout));
            Assert.IsTrue(SpinWait.SpinUntil(
                () => run.Snapshots.Single().State == ProgressBarComponent.ProgressState.Completed,
                GateTimeout));

            releasePollerSnapshot.Set();
            task.GetAwaiter().GetResult();

            Assert.AreEqual(ProgressBarComponent.ProgressState.Completed, run.Snapshots.Single().State);
        }
        finally
        {
            releasePollerSnapshot.Set();
            WaitForCleanup(task);
        }
    }

    [TestMethod]
    public void Run_PollerFault_StillClearsActiveRunAndCancelsLocalToken()
    {
        using var pollerEntered = new ManualResetEventSlim();
        using var releasePoller = new ManualResetEventSlim();
        using var run = new SolverRun(pollSnapshotFactory: (_, _) =>
        {
            pollerEntered.Set();
            Assert.IsTrue(releasePoller.Wait(GateTimeout));
            throw new InvalidOperationException("poller failed");
        });
        Task? task = null;

        try
        {
            var generation = run.Begin(Indeterminate("running"));
            task = Task.Run(() => run.Run(
                FastConfig(),
                new SimulationState(EasyInput()),
                generation,
                CancellationToken.None,
                _ => true));

            Assert.IsTrue(pollerEntered.Wait(GateTimeout));
            var activeSolver = run.Current;
            Assert.IsNotNull(activeSolver);

            releasePoller.Set();
            var exception = Assert.ThrowsException<InvalidOperationException>(task.GetAwaiter().GetResult);

            Assert.AreEqual("poller failed", exception.Message);
            Assert.IsNull(run.Current);
            Assert.IsTrue(activeSolver.Token.IsCancellationRequested);
        }
        finally
        {
            releasePoller.Set();
            WaitForCleanup(task);
        }
    }

    [TestMethod]
    public void Run_StaleRegistration_ObservesExternalCancellationDuringSetup()
    {
        using var tokenSource = new CancellationTokenSource();
        using var run = new SolverRun(beforeRegistration: tokenSource.Cancel);
        var staleGeneration = run.Begin(Indeterminate("first"));
        _ = run.Begin(Indeterminate("second"));

        Assert.ThrowsException<OperationCanceledException>(() =>
            run.Run(FastConfig(), new SimulationState(EasyInput()), staleGeneration,
                tokenSource.Token, _ => true));
    }

    private static ProgressBarComponent.ProgressSnapshot Indeterminate(string name) =>
        new(name, 0, 1, ProgressBarComponent.ProgressState.Indeterminate);

    private static SimulationInput EasyInput(int maxProgress = 50) =>
        new(new CharacterStats
        {
            Craftsmanship = 3304,
            Control = 3374,
            CP = 575,
            Level = 90,
            CanUseManipulation = true,
        }, new RecipeInfo
        {
            ClassJobLevel = 90,
            MaxDurability = 60,
            MaxQuality = 0,
            MaxProgress = maxProgress,
            QualityModifier = 0,
            QualityDivider = 115,
            ProgressModifier = 90,
            ProgressDivider = 130,
        });

    private static SolverConfig FastConfig(SolverAlgorithm algorithm = SolverAlgorithm.Oneshot) => new()
    {
        MaxStepCount = 5,
        Iterations = 64,
        MaxIterations = 64,
        MaxRolloutStepCount = 5,
        ForkCount = 2,
        FurcatedActionCount = 1,
        MaxThreadCount = 2,
        StrictActions = false,
        Algorithm = algorithm,
        ActionPool = [ActionType.BasicSynthesis, ActionType.MuscleMemory],
    };

    private static void WaitForCleanup(Task? task)
    {
        if (task is null || task.IsCompleted)
            return;

        try { _ = task.Wait(GateTimeout); }
        catch (AggregateException) { }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy requires a non-sealed proxy type.")]
    private class NullPluginLogProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(void))
                return null;

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
