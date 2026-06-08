using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sim = Craftimizer.Simulator.Simulator;
using SimNoRandom = Craftimizer.Simulator.SimulatorNoRandom;

namespace Craftimizer.Application.Crafting;

/// <summary>
/// Encapsulates the state and logic of a single crafting assist session.
/// Tracks the current recipe, character stats, solver task, and suggested macro.
/// UI concerns (game addon reads, window drawing) live in SynthHelper.
/// </summary>
public sealed class CraftingSession : IDisposable
{
    // ── Public state (read by SynthHelper Draw) ────────────────────────────────

    public RecipeData? RecipeData { get; private set; }
    public CharacterStats? CharacterStats { get; private set; }
    public SimulationInput? SimulationInput { get; private set; }
    internal SimulatedMacro Macro { get; }
    public Solver.Solver? SolverObject { get; private set; }
    public bool SolverRunning => !(SolverTask?.Completed ?? true);
    public bool SolverCancelling => SolverTask?.Cancelling ?? false;
    public int CurrentActionCount { get; private set; }
    public bool IsRecalculateQueued { get; private set; }

    /// <summary>
    /// Snapshots de progresso do solver para renderização na UI.
    /// Atualizado periodicamente (100ms) durante execução do solver.
    /// Lista vazia quando nenhum solver está ativo.
    /// Thread-safe para leitura durante rendering ImGui.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ciclo de vida:
    /// - <b>Início do solver</b>: Lista limpa em CalculateBestMacro()
    /// - <b>Durante execução</b>: Atualiza a cada 100ms via UpdateSnapshotsPeriodically()
    /// - <b>Conclusão</b>: Snapshot final com State=Completed adicionado
    /// - <b>Cancelamento</b>: Snapshot com State=Cancelled preservado
    /// </para>
    /// <para>
    /// Use com <see cref="ProgressBarComponent.DrawAggregated"/> para exibir progresso proporcional.
    /// Atualmente contém apenas um snapshot (single-solver), mas arquitetura suporta múltiplos.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProgressBarComponent.ProgressSnapshot> SolverSnapshots => _solverSnapshots;

    public ActionType? NextAction => Macro.Count > 0 ? Macro[0].Action : null;

    // ── Private session state ──────────────────────────────────────────────────

    private BackgroundTask<int>? SolverTask { get; set; }
    private bool SolverComparisonPending { get; set; }
    internal ActionStates CurrentActionStates { get; private set; }
    private SimulationState _currentState;
    private List<ActionType> PlayedActions { get; } = [];
    private bool CraftAutoSaved { get; set; }
    private readonly List<ProgressBarComponent.ProgressSnapshot> _solverSnapshots = [];
    private CancellationTokenSource? _snapshotUpdateCts;

    private readonly global::Craftimizer.Plugin.Plugin _plugin;

    // ── Constructor ────────────────────────────────────────────────────────────

    public CraftingSession(global::Craftimizer.Plugin.Plugin plugin)
    {
        _plugin = plugin;
        Macro = new(_plugin.Configuration);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the session for a new recipe. Pass the <see cref="RecipeData"/> and
    /// character stats computed by the caller (SynthHelper reads these from game
    /// memory). Does NOT start the solver — call
    /// <see cref="SetCurrentState"/> afterwards to trigger recalculation.
    /// </summary>
    public void StartCrafting(RecipeData recipeData, CharacterStats characterStats)
    {
        var shouldUpdateInput = false;

        if (recipeData.RecipeId != RecipeData?.RecipeId)
        {
            RecipeData = recipeData;
            shouldUpdateInput = true;
        }

        if (characterStats != CharacterStats)
        {
            CharacterStats = characterStats;
            shouldUpdateInput = true;
        }

        if (shouldUpdateInput)
        {
            SimulationInput = new(CharacterStats!, RecipeData!.RecipeInfo);
            ReSyncSavedMacroScore(RecipeData.RecipeId, SimulationInput);
        }

        CurrentActionCount = 0;
        CurrentActionStates = new();
        PlayedActions.Clear();
        CraftAutoSaved = false;
    }

    /// <summary>Clears the active recipe (craft ended or addon closed).</summary>
    public void ClearRecipe() => RecipeData = null;

    /// <summary>
    /// Updates the simulation state and optionally triggers solver recalculation.
    /// Pass <paramref name="shouldCalculate"/> = true when the helper is open and
    /// not collapsed.
    /// </summary>
    public void SetCurrentState(SimulationState state, bool shouldCalculate)
    {
        _currentState = state;

        if (!shouldCalculate)
        {
            IsRecalculateQueued = true;
            return;
        }

        IsRecalculateQueued = false;
        Macro.Clear();
        Macro.InitialState = _currentState;
        CalculateBestMacro();
    }

    /// <summary>
    /// Called by SynthHelper when an in-game crafting action is executed.
    /// Updates the simulation state by re-executing on top of the latest game state.
    /// </summary>
    public void RegisterActionUsed(ActionType action, SimulationState gameState)
    {
        (_, _currentState) = new SimNoRandom().Execute(gameState, action);
        CurrentActionCount = _currentState.ActionCount;
        CurrentActionStates = _currentState.ActionStates;
        PlayedActions.Add(action);
    }

    /// <summary>Flushes queued solver actions into the macro.</summary>
    public void FlushMacroQueue() => Macro.FlushQueue();

    /// <summary>
    /// After the solver finishes, compares its result against the saved macro and
    /// uses whichever scores higher. Call once per frame when the window is open.
    /// Returns true when the comparison was performed.
    /// </summary>
    public bool TryFinalizeSolverComparison()
    {
        if (!SolverComparisonPending || SolverTask?.Completed == false)
            return false;

        SolverComparisonPending = false;
        TryUseBetterSavedMacro();
        return true;
    }

    /// <summary>
    /// Auto-saves the played actions as a macro if the craft completed successfully
    /// and the result is better than the existing saved macro for this recipe.
    /// </summary>
    public void TryAutoSaveMacro()
    {
        if (CraftAutoSaved) return;
        if (!_plugin.Configuration.AutoSaveCraftMacro) return;
        if (PlayedActions.Count == 0) return;
        if (SimulationInput == null || RecipeData == null) return;

        if (_currentState.Progress < SimulationInput.Recipe.MaxProgress) return;

        CraftAutoSaved = true;

        var newScore = SimulationInput.Recipe.MaxQuality > 0
            ? (float)_currentState.Quality / SimulationInput.Recipe.MaxQuality
            : 1f;

        var recipeId = RecipeData.RecipeId;
        var itemName = RecipeData.Recipe.ItemResult.ValueNullable?.Name.ExtractText() ?? $"Recipe {recipeId}";
        var actions = PlayedActions.ToArray();

        var existing = _plugin.MacroRepository.Macros.FirstOrDefault(m => m.RecipeId == recipeId);

        if (existing == null)
        {
            var macro = new Macro
            {
                Name = itemName,
                RecipeId = recipeId,
                SavedScore = newScore,
            };
            macro.Actions = actions;
            _plugin.MacroRepository.Add(macro);
            global::Craftimizer.Plugin.Plugin.DisplayNotification(new()
            {
                Content = $"Macro saved for \"{itemName}\".",
                MinimizedText = "Craft macro saved",
                Title = "Craftimizer",
                Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
            });
        }
        else if (newScore > existing.SavedScore + 0.001f)
        {
            existing.SavedScore = newScore;
            existing.Actions = actions;
            _plugin.MacroRepository.Update(existing);
            global::Craftimizer.Plugin.Plugin.DisplayNotification(new()
            {
                Content = $"Better result found! Macro updated for \"{itemName}\" ({existing.SavedScore * 100:F0}% → {newScore * 100:F0}%).",
                MinimizedText = "Craft macro updated",
                Title = "Craftimizer",
                Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
            });
        }
    }

    /// <summary>Cancels any running solver task.</summary>
    public void CancelSolver()
    {
        if (SolverObject != null && SolverTask?.Completed == false)
        {
            // Preserve current progress in snapshot before cancelling
            var algorithmName = _plugin.Configuration.SynthHelperSolverConfig.Algorithm.ToString();
            var snapshot = ProgressBarComponent.FromSolver(SolverObject, algorithmName) with
            {
                State = ProgressBarComponent.ProgressState.Cancelled
            };

            lock (_solverSnapshots)
            {
                _solverSnapshots.Clear();
                _solverSnapshots.Add(snapshot);
            }
        }

        SolverTask?.Cancel();
        _snapshotUpdateCts?.Cancel();
    }

    /// <summary>Triggers a new solver run (same as starting a fresh calculation).</summary>
    public void RequestSolve() => CalculateBestMacro();

    public void Dispose()
    {
        SolverTask?.Cancel();
        _snapshotUpdateCts?.Cancel();
        _snapshotUpdateCts?.Dispose();
    }

    // ── Private solver/state logic ─────────────────────────────────────────────

    private void CalculateBestMacro()
    {
        SolverTask?.Cancel();
        _snapshotUpdateCts?.Cancel();
        _snapshotUpdateCts?.Dispose();
        _snapshotUpdateCts = null;
        _solverSnapshots.Clear();
        Macro.ClearQueue();
        Macro.Clear();

        if (_plugin.Configuration.ConditionRandomness)
        {
            _plugin.Configuration.ConditionRandomness = false;
            Macro.RecalculateState();
        }

        SolverComparisonPending = true;
        var state = _currentState;
        SolverTask = new(token => CalculateBestMacroTask(state, token, Gearsets.HasDelineations()));
        SolverTask.Start();
    }

    private int CalculateBestMacroTask(SimulationState state, CancellationToken token, bool hasDelineations)
    {
        var config = _plugin.Configuration.SynthHelperSolverConfig;
        var canUseDelineations = !_plugin.Configuration.CheckDelineations || hasDelineations;
        if (!canUseDelineations)
            config = config.FilterSpecialistActions();

        if (config.QualityTargetToMaxCollectability && RecipeData != null)
            config = config.WithResolvedQualityTarget(RecipeData.RecipeInfo.MaxQuality, RecipeData.CollectableThresholds);

        token.ThrowIfCancellationRequested();

        var solver = new Solver.Solver(config, state) { Token = token };
        solver.OnLog += Log.Debug;
        solver.OnWarn += t => global::Craftimizer.Plugin.Plugin.DisplaySolverWarning(t);
        solver.OnNewAction += EnqueueAction;
        SolverObject = solver;

        // Start periodic snapshot updates
        _snapshotUpdateCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var snapshotTask = Task.Run(() => UpdateSnapshotsPeriodically(solver, _snapshotUpdateCts.Token), _snapshotUpdateCts.Token);

        solver.Start();
        _ = solver.GetTask().GetAwaiter().GetResult();

        // Stop snapshot updates and create final snapshot
        _snapshotUpdateCts?.Cancel();
        try
        {
            snapshotTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected when we cancel
        }

        // Create final completed snapshot
        lock (_solverSnapshots)
        {
            _solverSnapshots.Clear();
            var algorithmName = config.Algorithm.ToString();
            _solverSnapshots.Add(ProgressBarComponent.FromSolver(solver, algorithmName) with
            {
                State = ProgressBarComponent.ProgressState.Completed
            });
        }

        token.ThrowIfCancellationRequested();

        return 0;
    }

    private void EnqueueAction(ActionType action)
    {
        var newSize = Macro.Enqueue(action, _plugin.Configuration.SynthHelperMaxDisplayCount);
        if (newSize >= _plugin.Configuration.SynthHelperStepCount || newSize >= _plugin.Configuration.SynthHelperMaxDisplayCount)
            SolverTask?.Cancel();
    }

    private void TryUseBetterSavedMacro()
    {
        if (RecipeData == null || SimulationInput == null) return;

        var existing = _plugin.MacroRepository.Macros.FirstOrDefault(m => m.RecipeId == RecipeData.RecipeId);
        if (existing == null || existing.Actions.Count == 0) return;

        var solverScore = CalculateMacroScore(Macro.State);
        var sim = new SimNoRandom();
        var (_, savedFinalState, _) = sim.ExecuteMultiple(Macro.InitialState, existing.Actions);
        var savedScore = CalculateMacroScore(savedFinalState);

        if (savedScore > solverScore + 0.001f)
        {
            Macro.Clear();
            Macro.ClearQueue();
            foreach (var action in existing.Actions)
                Macro.Enqueue(action, _plugin.Configuration.SynthHelperMaxDisplayCount);
            Macro.FlushQueue();
        }
    }

    private void ReSyncSavedMacroScore(ushort recipeId, SimulationInput input)
    {
        var existing = _plugin.MacroRepository.Macros.FirstOrDefault(m => m.RecipeId == recipeId);
        if (existing == null || existing.Actions.Count == 0) return;

        var sim = new SimNoRandom();
        var (_, finalState, _) = sim.ExecuteMultiple(new SimulationState(input), existing.Actions);
        var newScore = input.Recipe.MaxQuality > 0
            ? (float)finalState.Quality / input.Recipe.MaxQuality
            : (finalState.Progress >= input.Recipe.MaxProgress ? 1f : 0f);

        if (MathF.Abs(newScore - existing.SavedScore) > 0.001f)
        {
            existing.SavedScore = newScore;
            _plugin.MacroRepository.Update(existing);
        }
    }

    private float CalculateMacroScore(in SimulationState state)
    {
        if (SimulationInput == null) return 0f;
        if (state.Progress < SimulationInput.Recipe.MaxProgress) return 0f;
        return SimulationInput.Recipe.MaxQuality > 0
            ? (float)state.Quality / SimulationInput.Recipe.MaxQuality
            : 1f;
    }

    private Sim CreateSim(in SimulationState state) =>
        _plugin.Configuration.ConditionRandomness ? new Sim() { State = state } : new SimNoRandom() { State = state };

    /// <summary>
    /// Periodically polls the solver for progress and updates the snapshots list.
    /// Runs in a background task until cancelled.
    /// </summary>
    /// <summary>
    /// Atualiza snapshots periodicamente durante execução do solver.
    /// Roda em background task separada, polling a cada 100ms.
    /// Thread-safe via lock() na lista de snapshots.
    /// </summary>
    /// <param name="solver">Instância do solver sendo monitorado</param>
    /// <param name="token">Token de cancelamento para parar polling</param>
    private async Task UpdateSnapshotsPeriodically(Solver.Solver solver, CancellationToken token)
    {
        var algorithmName = _plugin.Configuration.SynthHelperSolverConfig.Algorithm.ToString();
        
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Update snapshot from current solver state
                var snapshot = ProgressBarComponent.FromSolver(solver, algorithmName);
                
                lock (_solverSnapshots)
                {
                    _solverSnapshots.Clear();
                    _solverSnapshots.Add(snapshot);
                }

                // Update every 100ms for smooth progress
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
