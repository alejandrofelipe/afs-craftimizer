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
    private sealed record ActiveRun(
        long Generation,
        SolverEngine Solver,
        string Algorithm,
        CancellationTokenSource Cancellation);

    private readonly List<ProgressBarComponent.ProgressSnapshot> _snapshots = [];
    private readonly Lock _gate = new();
    private readonly ExecutionGeneration _generation = new();
    private readonly Lock _stateGate = new();
    private ActiveRun? _active;

    /// <summary>O solver da execução corrente (para SolverProgressBar.FromSolver / cancelamento). Null se nunca rodou.</summary>
    public SolverEngine? Current
    {
        get { lock (_stateGate) return _active?.Solver; }
    }

    /// <summary>Cópia independente dos snapshots — segura para enumerar no render.</summary>
    public IReadOnlyList<ProgressBarComponent.ProgressSnapshot> Snapshots
    {
        get { lock (_gate) return _snapshots.ToArray(); }
    }

    private bool TrySetSnapshot(long generation, ProgressBarComponent.ProgressSnapshot snap)
    {
        lock (_gate)
        {
            if (!_generation.IsCurrent(generation))
                return false;

            _snapshots.Clear();
            _snapshots.Add(snap);
            return true;
        }
    }

    /// <summary>
    /// Inicia uma nova geração e define seu snapshot inicial de forma síncrona (UI thread, ANTES de
    /// iniciar o BackgroundTask que chama Run). null limpa a lista (caso do SynthHelper).
    /// </summary>
    public long Begin(ProgressBarComponent.ProgressSnapshot? snap)
    {
        long generation;
        lock (_gate)
        {
            generation = _generation.Next();
            _snapshots.Clear();
            if (snap is { } initial)
                _snapshots.Add(initial);
        }

        ActiveRun? previous;
        lock (_stateGate)
        {
            previous = _active;
            _active = null;
        }

        if (previous is not null)
            TryCancel(previous.Cancellation);

        return generation;
    }

    /// <summary>
    /// Roda o solver streaming até completar, parar cedo (onNewAction retornando false) ou cancelar.
    /// onNewAction: retorne true para continuar, false para early-stop (grava Completed antes de parar).
    /// NÃO mexe no snapshot inicial — use Begin na UI thread antes de chamar Run.
    /// </summary>
    public void Run(
        SolverConfig config,
        SimulationState state,
        long generation,
        CancellationToken token,
        Func<ActionType, bool> onNewAction,
        Action<SolverSolution>? onSuggestSolution = null,
        Action<Exception>? onFaulted = null)
    {
        token.ThrowIfCancellationRequested();

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var runToken = cancellation.Token;
        var algorithm = config.Algorithm.ToString();

        using var solver = new SolverEngine(config, state) { Token = runToken };
        solver.OnLog += text =>
        {
            if (_generation.IsCurrent(generation))
                Log.Debug(text);
            else
                cancellation.Cancel();
        };
        solver.OnWarn += text =>
        {
            if (_generation.IsCurrent(generation))
                Plugin.Plugin.DisplaySolverWarning(text);
            else
                cancellation.Cancel();
        };
        solver.OnNewAction += action =>
        {
            if (!_generation.IsCurrent(generation))
            {
                cancellation.Cancel();
                return;
            }

            if (!onNewAction(action))
            {
                TrySetSnapshot(generation, SolverProgressBar.FromSolver(solver, algorithm) with
                {
                    State = ProgressBarComponent.ProgressState.Completed
                });
                cancellation.Cancel();
            }
        };
        if (onSuggestSolution is not null)
        {
            solver.OnSuggestSolution += solution =>
            {
                if (_generation.IsCurrent(generation))
                    onSuggestSolution(solution);
                else
                    cancellation.Cancel();
            };
        }

        var active = new ActiveRun(generation, solver, algorithm, cancellation);
        ActiveRun? previous = null;
        var registered = false;
        lock (_stateGate)
        {
            if (_generation.IsCurrent(generation))
            {
                previous = _active;
                _active = active;
                registered = true;
            }
        }

        if (!registered)
        {
            cancellation.Cancel();
            return;
        }

        if (previous is not null)
            TryCancel(previous.Cancellation);

        using var pollerCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        var pollerTask = Task.Run(
            () => PollSnapshots(solver, algorithm, generation, cancellation, pollerCts.Token),
            pollerCts.Token);

        var completedNaturally = false;

        try
        {
            solver.Start();
            var t = solver.GetTask();
            _ = t.GetAwaiter().GetResult();

            completedNaturally = true;
        }
        catch (OperationCanceledException)
        {
            // Early-stop (Completed já gravado) ou Cancel do usuário (Cancelled já gravado): não sobrescreve.
        }
        catch (Exception exception)
        {
            if (_generation.IsCurrent(generation))
                onFaulted?.Invoke(exception);
            else
                cancellation.Cancel();
            throw;
        }
        finally
        {
            pollerCts.Cancel();
            try { pollerTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }

            // Poller já parado → seguro gravar o snapshot final sem clobber.
            if (completedNaturally)
                TrySetSnapshot(generation, SolverProgressBar.FromSolver(solver, algorithm) with
                {
                    State = ProgressBarComponent.ProgressState.Completed
                });

            lock (_stateGate)
            {
                if (ReferenceEquals(_active, active))
                    _active = null;
            }

            cancellation.Cancel();
        }

        token.ThrowIfCancellationRequested();
    }

    /// <summary>Cancela a execução. markCancelled=true grava um snapshot Cancelled (usado pelo SynthHelper).</summary>
    public void Cancel(bool markCancelled = false)
    {
        ActiveRun? active;
        lock (_stateGate)
            active = _active;

        if (active is null)
            return;

        if (markCancelled && !active.Cancellation.IsCancellationRequested)
            TrySetSnapshot(active.Generation, SolverProgressBar.FromSolver(active.Solver, active.Algorithm) with
            {
                State = ProgressBarComponent.ProgressState.Cancelled
            });

        _generation.TryInvalidate(active.Generation);
        TryCancel(active.Cancellation);
    }

    private async Task PollSnapshots(
        SolverEngine solver,
        string algorithm,
        long generation,
        CancellationTokenSource cancellation,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var snapshot = SolverProgressBar.FromSolver(solver, algorithm);
                if (!TrySetSnapshot(generation, snapshot))
                {
                    cancellation.Cancel();
                    break;
                }

                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void TryCancel(CancellationTokenSource cancellation)
    {
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose() => Cancel();
}
