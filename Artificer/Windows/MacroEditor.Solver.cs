using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Artificer.Windows;

public sealed partial class MacroEditor
{
    private void CalculateBestMacro()
    {
        SolverTask?.Cancel();
        _snapshotUpdateCts?.Cancel();
        _snapshotUpdateCts?.Dispose();
        _snapshotUpdateCts = null;
        lock (_solverSnapshots) _solverSnapshots.Clear();
        Macro.ClearQueue();

        RevertPreviousMacro();

        if (_plugin.Configuration.ConditionRandomness)
        {
            _plugin.Configuration.ConditionRandomness = false;
            _plugin.Configuration.Save();
            RecalculateState();
        }

        SolverStartStepCount = Macro.Count;

        var state = State;
        SolverTask = new(token => CalculateBestMacroTask(state, token, Gearsets.HasDelineations()));
        SolverTask.Start();
    }

    private int CalculateBestMacroTask(SimulationState state, CancellationToken token, bool hasDelineations)
    {
        var config = _plugin.Configuration.EditorSolverConfig;
        var canUseDelineations = !_plugin.Configuration.CheckDelineations || hasDelineations;
        if (!canUseDelineations)
            config = config.FilterSpecialistActions();

        if (config.QualityTargetToMaxCollectability && RecipeData.IsCollectable)
            config = config.WithResolvedQualityTarget(RecipeData.RecipeInfo.MaxQuality, RecipeData.CollectableThresholds);

        token.ThrowIfCancellationRequested();

        var solver = new Solver.Solver(config, state) { Token = token };
        solver.OnLog += Log.Debug;
        solver.OnWarn += t => Plugin.Plugin.DisplaySolverWarning(t);
        solver.OnNewAction += a => Macro.Enqueue(a);
        solver.OnSuggestSolution += a => Macro.EnqueueEphemeral(a.Actions);
        SolverObject = solver;

        _snapshotUpdateCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var snapshotTask = Task.Run(() => UpdateSnapshotsPeriodically(solver, config, _snapshotUpdateCts.Token), _snapshotUpdateCts.Token);

        solver.Start();
        var t = solver.GetTask();
        _ = t.ContinueWith(_ => Macro.RemoveEphemeral(), TaskContinuationOptions.NotOnCanceled);
        _ = t.ContinueWith(faulted => Log.Error(faulted.Exception!, "Solver task faulted"), TaskContinuationOptions.OnlyOnFaulted);
        _ = t.GetAwaiter().GetResult();

        _snapshotUpdateCts?.Cancel();
        try { snapshotTask.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }

        lock (_solverSnapshots)
        {
            _solverSnapshots.Clear();
            _solverSnapshots.Add(SolverProgressBar.FromSolver(solver, config.Algorithm.ToString()) with
            {
                State = ProgressBarComponent.ProgressState.Completed
            });
        }

        token.ThrowIfCancellationRequested();

        return 0;
    }

    private async Task UpdateSnapshotsPeriodically(Solver.Solver solver, Solver.SolverConfig config, CancellationToken token)
    {
        var algorithmName = config.Algorithm.ToString();
        while (!token.IsCancellationRequested)
        {
            try
            {
                var snapshot = SolverProgressBar.FromSolver(solver, algorithmName);
                lock (_solverSnapshots)
                {
                    _solverSnapshots.Clear();
                    _solverSnapshots.Add(snapshot);
                }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private void RevertPreviousMacro()
    {
        if (SolverStartStepCount is { } stepCount && stepCount < Macro.Count)
            Macro.RemoveRange(stepCount, Macro.Count - stepCount);
    }
}
