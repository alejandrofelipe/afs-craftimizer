using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Utils;
using System.Threading;

namespace Artificer.Windows;

public sealed partial class MacroEditor
{
    private void CalculateBestMacro()
    {
        _solverCommits.Invalidate(() =>
        {
            SolverTask?.Cancel();
            _run.Cancel();
        });

        // Snapshot inicial na UI thread (loading imediato no 1º frame — preserva o comportamento atual).
        var initialSnapshot = new ProgressBarComponent.ProgressSnapshot(
            Name: _plugin.Configuration.EditorSolverConfig.Algorithm.ToString(),
            CurrentValue: 0, MaxValue: 1,
            State: ProgressBarComponent.ProgressState.Indeterminate);
        var state = State;
        var generation = _solverCommits.Begin(
            reset: () =>
            {
                Macro.ClearQueue();
                RevertPreviousMacro();

                if (_plugin.Configuration.ConditionRandomness)
                {
                    _plugin.Configuration.ConditionRandomness = false;
                    _plugin.Configuration.Save();
                    RecalculateState();
                }

                SolverStartStepCount = Macro.Count;
                state = State;
            },
            beginGeneration: () => _run.Begin(initialSnapshot));

        SolverTask = new(token => CalculateBestMacroTask(state, generation, token, Gearsets.HasDelineations()));
        SolverTask.Start();
    }

    private int CalculateBestMacroTask(SimulationState state, long generation, CancellationToken token, bool hasDelineations)
    {
        var config = _plugin.Configuration.EditorSolverConfig
            .ForDelineations(_plugin.Configuration.CheckDelineations, hasDelineations);

        try
        {
            _run.Run(config, state, generation, token,
                onNewAction: a => _solverCommits.TryCommit(generation, () =>
                {
                    Macro.Enqueue(a);
                    return true;
                }),
                onSuggestSolution: a => _solverCommits.TryCommit(
                    generation,
                    () => Macro.EnqueueEphemeral(a.Actions)),
                onFaulted: ex => _solverCommits.TryCommit(
                    generation,
                    () => Log.Error(ex, "Solver task faulted")));
        }
        finally
        {
            // Remove ephemeral em conclusão OU falha (não em cancelamento) — espelha o antigo
            // ContinueWith(..., TaskContinuationOptions.NotOnCanceled).
            if (!token.IsCancellationRequested)
                _solverCommits.TryCommit(generation, Macro.RemoveEphemeral);
        }

        return 0;
    }

    private void RevertPreviousMacro()
    {
        if (SolverStartStepCount is { } stepCount && stepCount < Macro.Count)
            Macro.RemoveRange(stepCount, Macro.Count - stepCount);
    }
}
