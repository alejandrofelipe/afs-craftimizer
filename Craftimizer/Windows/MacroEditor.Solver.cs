using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Utils;
using System.Threading;

namespace Craftimizer.Windows;

public sealed partial class MacroEditor
{
    private void CalculateBestMacro()
    {
        SolverTask?.Cancel();
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

        token.ThrowIfCancellationRequested();

        var solver = new Solver.Solver(config, state) { Token = token };
        solver.OnLog += Log.Debug;
        solver.OnWarn += t => Plugin.Plugin.DisplaySolverWarning(t);
        solver.OnNewAction += a => Macro.Enqueue(a);
        solver.OnSuggestSolution += a => Macro.EnqueueEphemeral(a.Actions);
        SolverObject = solver;
        solver.Start();
        var t = solver.GetTask();
        _ = t.ContinueWith(_ => Macro.RemoveEphemeral(), System.Threading.Tasks.TaskContinuationOptions.NotOnCanceled);
        _ = t.ContinueWith(faulted => Log.Error(faulted.Exception!, "Solver task faulted"), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        _ = t.GetAwaiter().GetResult();

        token.ThrowIfCancellationRequested();

        return 0;
    }

    private void RevertPreviousMacro()
    {
        if (SolverStartStepCount is { } stepCount && stepCount < Macro.Count)
            Macro.RemoveRange(stepCount, Macro.Count - stepCount);
    }
}
