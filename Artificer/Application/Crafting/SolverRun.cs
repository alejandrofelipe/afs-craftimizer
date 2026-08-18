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
    private readonly Func<SolverEngine, string, ProgressBarComponent.ProgressSnapshot> _pollSnapshotFactory;
    private readonly Action? _beforeRegistration;
    private ActiveRun? _active;
    private long? _currentGeneration;
    private bool _disposed;

    public SolverRun()
        : this(SolverProgressBar.FromSolver, null)
    {
    }

    internal SolverRun(Func<SolverEngine, string, ProgressBarComponent.ProgressSnapshot> pollSnapshotFactory)
        : this(pollSnapshotFactory, null)
    {
    }

    internal SolverRun(Action beforeRegistration)
        : this(SolverProgressBar.FromSolver, beforeRegistration)
    {
    }

    private SolverRun(
        Func<SolverEngine, string, ProgressBarComponent.ProgressSnapshot> pollSnapshotFactory,
        Action? beforeRegistration)
    {
        _pollSnapshotFactory = pollSnapshotFactory;
        _beforeRegistration = beforeRegistration;
    }

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
        lock (_stateGate)
        {
            if (!IsCurrentGenerationLocked(generation))
                return false;

            lock (_gate)
            {
                _snapshots.Clear();
                _snapshots.Add(snap);
            }

            return true;
        }
    }

    private bool TrySetTerminalSnapshot(long generation, ProgressBarComponent.ProgressSnapshot snap)
    {
        lock (_stateGate)
            return TrySetTerminalSnapshotLocked(generation, snap);
    }

    private bool TrySetTerminalSnapshotLocked(long generation, ProgressBarComponent.ProgressSnapshot snap)
    {
        if (!IsCurrentGenerationLocked(generation))
            return false;

        lock (_gate)
        {
            _snapshots.Clear();
            _snapshots.Add(snap);
        }

        _currentGeneration = null;
        _generation.TryInvalidate(generation);
        return true;
    }

    private bool IsCurrentGeneration(long generation)
    {
        lock (_stateGate)
            return IsCurrentGenerationLocked(generation);
    }

    private bool IsCurrentGenerationLocked(long generation) =>
        !_disposed
     && _currentGeneration == generation
     && _generation.IsCurrent(generation);

    /// <summary>
    /// Inicia uma nova geração e define seu snapshot inicial de forma síncrona (UI thread, ANTES de
    /// iniciar o BackgroundTask que chama Run). null limpa a lista (caso do SynthHelper).
    /// </summary>
    public long Begin(ProgressBarComponent.ProgressSnapshot? snap)
    {
        ActiveRun? previous;
        long generation;
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            generation = _generation.Next();
            _currentGeneration = generation;
            previous = _active;
            _active = null;

            lock (_gate)
            {
                _snapshots.Clear();
                if (snap is { } initial)
                    _snapshots.Add(initial);
            }
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
        lock (_stateGate)
            ObjectDisposedException.ThrowIf(_disposed, this);

        token.ThrowIfCancellationRequested();

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var runToken = cancellation.Token;
        var algorithm = config.Algorithm.ToString();

        using var solver = new SolverEngine(config, state) { Token = runToken };
        solver.OnLog += text =>
        {
            if (IsCurrentGeneration(generation))
                Log.Debug(text);
            else
                cancellation.Cancel();
        };
        solver.OnWarn += text =>
        {
            if (IsCurrentGeneration(generation))
                Plugin.Plugin.DisplaySolverWarning(text);
            else
                cancellation.Cancel();
        };
        solver.OnNewAction += action =>
        {
            if (!IsCurrentGeneration(generation))
            {
                cancellation.Cancel();
                return;
            }

            if (!onNewAction(action))
            {
                TrySetTerminalSnapshot(generation, SolverProgressBar.FromSolver(solver, algorithm) with
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
                if (IsCurrentGeneration(generation))
                    onSuggestSolution(solution);
                else
                    cancellation.Cancel();
            };
        }

        var active = new ActiveRun(generation, solver, algorithm, cancellation);
        _beforeRegistration?.Invoke();
        ActiveRun? previous = null;
        var registered = false;
        lock (_stateGate)
        {
            if (IsCurrentGenerationLocked(generation))
            {
                previous = _active;
                _active = active;
                registered = true;
            }
        }

        if (!registered)
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
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
            if (IsCurrentGeneration(generation))
                onFaulted?.Invoke(exception);
            else
                cancellation.Cancel();
            throw;
        }
        finally
        {
            pollerCts.Cancel();
            try
            {
                try { pollerTask.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            finally
            {
                // Poller já parado → seguro gravar o snapshot final sem clobber.
                if (completedNaturally)
                    TrySetTerminalSnapshot(generation, SolverProgressBar.FromSolver(solver, algorithm) with
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
        }

        token.ThrowIfCancellationRequested();
    }

    /// <summary>Cancela a execução. markCancelled=true grava um snapshot Cancelled (usado pelo SynthHelper).</summary>
    public void Cancel(bool markCancelled = false)
    {
        ActiveRun? active;
        lock (_stateGate)
        {
            if (_disposed)
                return;

            active = _active;

            if (markCancelled && active is not null)
            {
                TrySetTerminalSnapshotLocked(
                    active.Generation,
                    SolverProgressBar.FromSolver(active.Solver, active.Algorithm) with
                    {
                        State = ProgressBarComponent.ProgressState.Cancelled
                    });
            }
            else
            {
                if (_currentGeneration is { } generation)
                {
                    _currentGeneration = null;
                    _generation.TryInvalidate(generation);
                }
            }
        }

        if (active is not null)
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
                var snapshot = _pollSnapshotFactory(solver, algorithm);
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

    public void Dispose()
    {
        ActiveRun? active;
        lock (_stateGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            active = _active;
            _active = null;

            if (_currentGeneration is { } generation)
            {
                _currentGeneration = null;
                _generation.TryInvalidate(generation);
            }
        }

        if (active is not null)
            TryCancel(active.Cancellation);
    }
}
