using Artificer.Simulator;
using Artificer.Simulator.Actions;
using System.Runtime.InteropServices;

namespace Artificer.Solver;

[StructLayout(LayoutKind.Auto)]
public readonly record struct MCTSConfig
{
    public int MaxThreadCount { get; init; }

    public int MaxStepCount { get; init; }
    public int MaxRolloutStepCount { get; init; }
    public bool StrictActions { get; init; }

    public float MaxScoreWeightingConstant { get; init; }
    public float ExplorationConstant { get; init; }

    // Valor absoluto de quality que o score recompensa até (resolvido uma vez do config + recipe).
    public int QualityTarget { get; init; }

    public ActionType[] ActionPool { get; init; }

    public MCTSConfig(in SolverConfig config, in RecipeInfo recipe)
    {
        MaxStepCount = config.MaxStepCount;
        MaxRolloutStepCount = config.MaxRolloutStepCount;
        StrictActions = config.StrictActions;

        MaxScoreWeightingConstant = config.MaxScoreWeightingConstant;
        ExplorationConstant = config.ExplorationConstant;

        QualityTarget = config.ResolveQualityTarget(in recipe);

        ActionPool = config.ActionPool;
    }
}
