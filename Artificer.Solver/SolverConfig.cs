using Artificer.Simulator;
using Artificer.Simulator.Actions;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Artificer.Solver;

public enum SolverAlgorithm
{
    Oneshot,
    OneshotForked,
    Stepwise,
    StepwiseForked,
    StepwiseGenetic,
    Raphael,
    // Evaluates each candidate next action independently via forked MCTS and picks the best.
    // Designed for Synthesis Helper: faster response, better condition adaptation.
    NextActionForked,
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct SolverConfig
{
    // MCTS configuration
    public int Iterations { get; init; }
    public int MaxIterations { get; init; }
    public float MaxScoreWeightingConstant { get; init; }
    public float ExplorationConstant { get; init; }
    public int MaxStepCount { get; init; }
    public int MaxRolloutStepCount { get; init; }
    public int ForkCount { get; init; }
    public int FurcatedActionCount { get; init; }
    public bool StrictActions { get; init; }

    // Raphael/A* configuration
    public bool Adversarial { get; init; }
    public bool BackloadProgress { get; init; }

    // Quality target settings
    // Percentual (1–100) da MaxQuality que o solver mira; ao atingir, para de gastar esforço em quality.
    // JSON renomeado: configs antigas tinham este campo como float (0.0–1.0); o nome novo faz o valor
    // antigo ser ignorado na desserialização (migrado em Configuration.MigrateSolverConfigs).
    [JsonPropertyName("QualityTargetPct")]
    public int QualityTargetPercent { get; init; }
    // Se true, capa a quality no maior tier de collectability (via RecipeInfo.CollectableTargetQuality)
    // em vez de MaxQuality. Sem efeito em Cosmic Exploration (CollectableTargetQuality = null).
    public bool QualityTargetToMaxCollectability { get; init; }

    // Wall-clock budget in milliseconds for NextActionForked algorithm.
    // 0 = use Iterations-based budget (existing behavior for all other algorithms).
    public int MaxTimeMs { get; init; }

    public int MaxThreadCount { get; init; }
    public ActionType[] ActionPool { get; init; }
    public SolverAlgorithm Algorithm { get; init; }

    public SolverConfig()
    {
        Iterations = 100_000;
        MaxIterations = 1_500_000;
        MaxScoreWeightingConstant = 0.1f;
        ExplorationConstant = 4;
        MaxStepCount = 30;
        MaxRolloutStepCount = 99;
        // Use 75% of all cores if less than 12 cores are available, otherwise use all but 4 cores. Keep at least 1 core.
        MaxThreadCount = Math.Max(1, Math.Max(Environment.ProcessorCount - 4, (int)MathF.Floor(Environment.ProcessorCount * 0.75f)));
        // Use 32 forks at minimum, or the number of cores, whichever is higher.
        ForkCount = Math.Max(Environment.ProcessorCount, 32);
        FurcatedActionCount = ForkCount / 2;
        StrictActions = true;

        QualityTargetPercent = 100;
        QualityTargetToMaxCollectability = true;

        ActionPool = DeterministicActionPool;
        Algorithm = SolverAlgorithm.StepwiseGenetic;
    }

    public static ActionType[] OptimizeActionPool(IEnumerable<ActionType> actions) =>
        [.. actions.Order()];

    public SolverConfig FilterSpecialistActions() =>
        this with { ActionPool = ActionPool.Where(action => !SpecialistActions.Contains(action)).ToArray() };

    /// <summary>
    /// Resolve o QualityTarget absoluto (em pontos de quality) a partir do percent + recipe.
    /// Aplica o cap de collectability quando <see cref="QualityTargetToMaxCollectability"/> e
    /// <see cref="RecipeInfo.CollectableTargetQuality"/> têm valor (null = sem cap, ex.: Cosmic).
    /// </summary>
    public int ResolveQualityTarget(in RecipeInfo recipe)
    {
        var maxQuality = recipe.MaxQuality;
        if (maxQuality <= 0)
            return 0;

        var target = maxQuality * QualityTargetPercent / 100;
        if (QualityTargetToMaxCollectability && recipe.CollectableTargetQuality is { } maxCollectableQuality)
            target = Math.Min(target, maxCollectableQuality);

        return Math.Min(target, maxQuality);
    }

    public static readonly ActionType[] DeterministicActionPool = OptimizeActionPool(new[]
    {
        ActionType.MuscleMemory,
        ActionType.Reflect,
        ActionType.TrainedEye,

        ActionType.BasicSynthesis,
        ActionType.CarefulSynthesis,
        ActionType.Groundwork,
        ActionType.DelicateSynthesis,
        ActionType.PrudentSynthesis,

        ActionType.BasicTouch,
        ActionType.StandardTouch,
        ActionType.ByregotsBlessing,
        ActionType.PrudentTouch,
        ActionType.AdvancedTouch,
        ActionType.PreparatoryTouch,
        ActionType.TrainedFinesse,
        ActionType.RefinedTouch,

        ActionType.MastersMend,
        ActionType.WasteNot,
        ActionType.WasteNot2,
        ActionType.Manipulation,
        ActionType.ImmaculateMend,
        ActionType.TrainedPerfection,

        ActionType.Veneration,
        ActionType.GreatStrides,
        ActionType.Innovation,
        ActionType.QuickInnovation,

        ActionType.Observe,
        ActionType.HeartAndSoul,

        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.ObservedAdvancedTouchCombo,
        ActionType.RefinedTouchCombo,
    });

    // Same as deterministic, but with condition-specific actions added
    public static readonly ActionType[] RandomizedActionPool = OptimizeActionPool(new[]
    {
        ActionType.MuscleMemory,
        ActionType.Reflect,
        ActionType.TrainedEye,

        ActionType.BasicSynthesis,
        ActionType.CarefulSynthesis,
        ActionType.Groundwork,
        ActionType.DelicateSynthesis,
        ActionType.IntensiveSynthesis,
        ActionType.PrudentSynthesis,

        ActionType.BasicTouch,
        ActionType.StandardTouch,
        ActionType.ByregotsBlessing,
        ActionType.PreciseTouch,
        ActionType.PrudentTouch,
        ActionType.AdvancedTouch,
        ActionType.PreparatoryTouch,
        ActionType.TrainedFinesse,
        ActionType.RefinedTouch,

        ActionType.MastersMend,
        ActionType.WasteNot,
        ActionType.WasteNot2,
        ActionType.Manipulation,
        ActionType.ImmaculateMend,
        ActionType.TrainedPerfection,

        ActionType.Veneration,
        ActionType.GreatStrides,
        ActionType.Innovation,
        ActionType.QuickInnovation,

        ActionType.Observe,
        ActionType.HeartAndSoul,
        ActionType.TricksOfTheTrade,

        ActionType.StandardTouchCombo,
        ActionType.AdvancedTouchCombo,
        ActionType.ObservedAdvancedTouchCombo,
        ActionType.RefinedTouchCombo,
    });

    public static readonly FrozenSet<ActionType> InefficientActions =
        new[]
        {
            ActionType.CarefulObservation,
            ActionType.FinalAppraisal
        }.ToFrozenSet();

    public static readonly FrozenSet<ActionType> RiskyActions =
        new[]
        {
            ActionType.RapidSynthesis,
            ActionType.HastyTouch,
            ActionType.DaringTouch,
        }.ToFrozenSet();

    public static readonly FrozenSet<ActionType> SpecialistActions =
        new[]
        {
            ActionType.CarefulObservation,
            ActionType.HeartAndSoul,
            ActionType.QuickInnovation,
        }.ToFrozenSet();

    public static readonly SolverConfig RecipeNoteDefault = new SolverConfig() with
    {

    };

    public static readonly SolverConfig EditorDefault = new SolverConfig() with
    {
        Algorithm = SolverAlgorithm.Raphael,
        Adversarial = true
    };

    public static readonly SolverConfig SynthHelperDefault = new SolverConfig() with
    {
        ActionPool = RandomizedActionPool
    };
}
