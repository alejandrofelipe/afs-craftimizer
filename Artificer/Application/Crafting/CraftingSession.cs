using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sim = Artificer.Simulator.Simulator;
using SimNoRandom = Artificer.Simulator.SimulatorNoRandom;

namespace Artificer.Application.Crafting;

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
    public Solver.Solver? SolverObject => _run.Current;
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
    /// - <b>Durante execução</b>: Atualiza a cada 100ms via poller interno do <see cref="SolverRun"/>
    /// - <b>Conclusão</b>: Snapshot final com State=Completed adicionado
    /// - <b>Cancelamento</b>: Snapshot com State=Cancelled preservado
    /// </para>
    /// <para>
    /// Use com <see cref="ProgressBarComponent.DrawAggregated"/> para exibir progresso proporcional.
    /// Atualmente contém apenas um snapshot (single-solver), mas arquitetura suporta múltiplos.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProgressBarComponent.ProgressSnapshot> SolverSnapshots => _run.Snapshots;

    public ActionType? NextAction => Macro.Count > 0 ? Macro[0].Action : null;

    // ── Private session state ──────────────────────────────────────────────────

    private BackgroundTask<int>? SolverTask { get; set; }
    private bool SolverComparisonPending { get; set; }
    internal ActionStates CurrentActionStates { get; private set; }
    private SimulationState _currentState;
    private List<ActionType> PlayedActions { get; } = [];
    private bool CraftAutoSaved { get; set; }
    private readonly SolverRun _run = new();

    private readonly global::Artificer.Plugin.Plugin _plugin;

    // ── Constructor ────────────────────────────────────────────────────────────

    public CraftingSession(global::Artificer.Plugin.Plugin plugin)
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

        var cfg = CurrentMctsConfig();
        var newScore = MacroScoring.ScoreState(_currentState, cfg);

        var recipeId = RecipeData.RecipeId;
        var itemName = RecipeData.Recipe.ItemResult.ValueNullable?.Name.ExtractText() ?? $"Recipe {recipeId}";
        var actions = PlayedActions.ToArray();
        var hash = CharacterStats != null ? CharacterStats.ComputeHash(CharacterStats) : (int?)null;

        // Auto-save só mira a macro Auto da receita; nunca toca uma macro User (feita/importada).
        var existing = _plugin.MacroRepository.SnapshotMacros()
            .FirstOrDefault(m => m.RecipeId == recipeId && m.Source == MacroSource.Auto);
        var outcome = MacroSelection.DecideAutoSave(existing?.SavedScore, newScore);
        if (outcome == MacroSelection.AutoSaveOutcome.Skip)
        {
            CraftAutoSaved = true;
            return;
        }

        try
        {
            if (outcome == MacroSelection.AutoSaveOutcome.Insert)
            {
                var macro = new Macro
                {
                    Name = itemName,
                    RecipeId = recipeId,
                    SavedScore = newScore,
                    Source = MacroSource.Auto,
                };
                macro.Actions = actions;
                _plugin.MacroRepository.Add(macro, hash);
                global::Artificer.Plugin.Plugin.DisplayNotification(new()
                {
                    Content = $"Macro saved for \"{itemName}\".",
                    MinimizedText = "Craft macro saved",
                    Title = "Artificer",
                    Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
                });
            }
            else // Overwrite
            {
                var oldPct = existing!.SavedScore * 100;
                existing.SavedScore = newScore;
                existing.Actions = actions;
                existing.CharacterStatsHash = hash;
                _plugin.MacroRepository.Update(existing);
                global::Artificer.Plugin.Plugin.DisplayNotification(new()
                {
                    Content = $"Better result found! Macro updated for \"{itemName}\" ({oldPct:F0}% → {newScore * 100:F0}%).",
                    MinimizedText = "Craft macro updated",
                    Title = "Artificer",
                    Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
                });
            }
            CraftAutoSaved = true; // só após o write persistir (S3)
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "Auto-save de macro falhou; permitindo retry");
            // NÃO seta CraftAutoSaved: o próximo frame pode tentar de novo.
        }
    }

    /// <summary>Cancels any running solver task.</summary>
    public void CancelSolver()
    {
        _run.Cancel(markCancelled: SolverRunning);
        SolverTask?.Cancel();
    }

    /// <summary>Triggers a new solver run (same as starting a fresh calculation).</summary>
    public void RequestSolve() => CalculateBestMacro();

    public void Dispose()
    {
        SolverTask?.Cancel();
        _run.Dispose();
    }

    // ── Private solver/state logic ─────────────────────────────────────────────

    private void CalculateBestMacro()
    {
        SolverTask?.Cancel();
        _run.Cancel();
        _run.SetInitialSnapshot(null); // limpa os snapshots (equivalente ao Clear() da lista antiga)
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
        var config = _plugin.Configuration.SynthHelperSolverConfig
            .ForDelineations(_plugin.Configuration.CheckDelineations, hasDelineations);

        _run.Run(config, state, token,
            onNewAction: a =>
            {
                var newSize = Macro.Enqueue(a, _plugin.Configuration.SynthHelperMaxDisplayCount);
                var shouldContinue = newSize < _plugin.Configuration.SynthHelperStepCount
                    && newSize < _plugin.Configuration.SynthHelperMaxDisplayCount;
                if (!shouldContinue)
                    SolverTask?.Cancel(); // restaura o comportamento antigo: cancela o BackgroundTask (SolverCancelling=true, task Canceled)
                return shouldContinue;
            });

        return 0;
    }

    private Artificer.Solver.MCTSConfig CurrentMctsConfig() =>
        new(_plugin.Configuration.SynthHelperSolverConfig, RecipeData!.RecipeInfo);

    private void TryUseBetterSavedMacro()
    {
        if (RecipeData == null || SimulationInput == null) return;

        var cfg = CurrentMctsConfig();
        var sim = new SimNoRandom();
        var initial = Macro.InitialState;
        var best = MacroSelection.SelectBestForRecipe(
            _plugin.MacroRepository.SnapshotMacros(), RecipeData.RecipeId,
            m => MacroScoring.ScoreActions(m.Actions, sim, initial, cfg));
        if (best == null) return;

        var solverScore = MacroScoring.ScoreState(Macro.State, cfg);
        var savedScore = MacroScoring.ScoreActions(best.Actions, sim, initial, cfg);

        if (savedScore > solverScore + 0.001f)
        {
            Macro.Clear();
            Macro.ClearQueue();
            foreach (var action in best.Actions)
                Macro.Enqueue(action, _plugin.Configuration.SynthHelperMaxDisplayCount);
            Macro.FlushQueue();
        }
    }

    private void ReSyncSavedMacroScore(ushort recipeId, SimulationInput input)
    {
        var cfg = new Artificer.Solver.MCTSConfig(_plugin.Configuration.SynthHelperSolverConfig, input.Recipe);
        var sim = new SimNoRandom();
        var start = new SimulationState(input);

        foreach (var macro in _plugin.MacroRepository.SnapshotMacros())
        {
            if (macro.RecipeId != recipeId || macro.Actions.Count == 0)
                continue;
            var newScore = MacroScoring.ScoreActions(macro.Actions, sim, start, cfg);
            if (MathF.Abs(newScore - macro.SavedScore) > 0.001f)
            {
                macro.SavedScore = newScore;
                _plugin.MacroRepository.Update(macro);
            }
        }
    }

    private Sim CreateSim(in SimulationState state) =>
        _plugin.Configuration.ConditionRandomness ? new Sim() { State = state } : new SimNoRandom() { State = state };
}
