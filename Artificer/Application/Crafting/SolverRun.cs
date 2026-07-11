using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Artificer.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SolverEngine = Artificer.Solver.Solver;

namespace Artificer.Application.Crafting;

/// <summary>
/// Encapsula o ciclo de vida de uma execução "streaming" do solver + os snapshots de progresso.
/// Compartilhado por MacroEditor e SynthHelper (CraftingSession). O método Run é bloqueante e
/// deve ser chamado de dentro de um BackgroundTask do host. Leitura de <see cref="Snapshots"/> é
/// thread-safe (para o render ImGui).
/// </summary>
public sealed class SolverRun : IDisposable
{
    private readonly List<ProgressBarComponent.ProgressSnapshot> _snapshots = [];
    private readonly Lock _gate = new();
    private CancellationTokenSource? _cts;
    private string _algo = "";

    /// <summary>O solver da execução corrente (para SolverProgressBar.FromSolver / cancelamento). Null se nunca rodou.</summary>
    public SolverEngine? Current { get; private set; }

    /// <summary>Cópia independente dos snapshots — segura para enumerar no render.</summary>
    public IReadOnlyList<ProgressBarComponent.ProgressSnapshot> Snapshots
    {
        get { lock (_gate) return _snapshots.ToArray(); }
    }

    private void SetSnapshot(ProgressBarComponent.ProgressSnapshot snap)
    {
        lock (_gate) { _snapshots.Clear(); _snapshots.Add(snap); }
    }

    /// <summary>
    /// Define o snapshot inicial de forma síncrona (UI thread, ANTES de iniciar o BackgroundTask que
    /// chama Run) — garante loading imediato no 1º frame. null limpa a lista (caso do SynthHelper).
    /// </summary>
    public void SetInitialSnapshot(ProgressBarComponent.ProgressSnapshot? snap)
    {
        if (snap is { } s) SetSnapshot(s);
        else lock (_gate) _snapshots.Clear();
    }

    /// <summary>
    /// Roda o solver streaming até completar, parar cedo (onNewAction retornando false) ou cancelar.
    /// onNewAction: retorne true para continuar, false para early-stop (grava Completed antes de parar).
    /// NÃO mexe no snapshot inicial — use SetInitialSnapshot na UI thread antes de chamar Run.
    /// </summary>
    public void Run(
        SolverConfig config,
        SimulationState state,
        CancellationToken token,
        Func<ActionType, bool> onNewAction,
        Action<SolverSolution>? onSuggestSolution = null,
        Action<Exception>? onFaulted = null)
    {
        _algo = config.Algorithm.ToString();

        token.ThrowIfCancellationRequested();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var runToken = _cts.Token;

        var solver = new SolverEngine(config, state) { Token = runToken };
        solver.OnLog += Log.Debug;
        solver.OnWarn += t => Plugin.Plugin.DisplaySolverWarning(t);
        solver.OnNewAction += a => { if (!onNewAction(a)) EarlyStop(); };
        if (onSuggestSolution is not null)
            solver.OnSuggestSolution += s => onSuggestSolution(s);
        Current = solver;

        using var pollerCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        var pollerTask = Task.Run(() => PollSnapshots(solver, pollerCts.Token), pollerCts.Token);

        try
        {
            solver.Start();
            var t = solver.GetTask();
            _ = t.ContinueWith(f => onFaulted?.Invoke(f.Exception!), TaskContinuationOptions.OnlyOnFaulted);
            _ = t.GetAwaiter().GetResult();

            // Completou naturalmente → snapshot Completed.
            SetSnapshot(SolverProgressBar.FromSolver(solver, _algo) with
            {
                State = ProgressBarComponent.ProgressState.Completed
            });
        }
        catch (OperationCanceledException)
        {
            // Early-stop (Completed já gravado) ou Cancel do usuário (Cancelled já gravado): não sobrescreve.
        }
        finally
        {
            pollerCts.Cancel();
            try { pollerTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
        }

        token.ThrowIfCancellationRequested();
    }

    private void EarlyStop()
    {
        if (Current is { } s)
            SetSnapshot(SolverProgressBar.FromSolver(s, _algo) with
            {
                State = ProgressBarComponent.ProgressState.Completed
            });
        _cts?.Cancel();
    }

    /// <summary>Cancela a execução. markCancelled=true grava um snapshot Cancelled (usado pelo SynthHelper).</summary>
    public void Cancel(bool markCancelled = false)
    {
        if (markCancelled && Current is { } s && _cts is { IsCancellationRequested: false })
            SetSnapshot(SolverProgressBar.FromSolver(s, _algo) with
            {
                State = ProgressBarComponent.ProgressState.Cancelled
            });
        _cts?.Cancel();
    }

    private async Task PollSnapshots(SolverEngine solver, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var snapshot = SolverProgressBar.FromSolver(solver, _algo);
                SetSnapshot(snapshot);
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
