using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Artificer.Utils;
using System.Collections.Generic;

namespace Artificer.Application.Crafting;

/// <summary>
/// Ponto único de score de macro. Usa o objetivo lexicográfico do solver
/// (<see cref="SimulationNode.CalculateScoreForState"/>): quality-target/collectable-aware,
/// range [0,1] (0 = incompleto).
/// </summary>
public static class MacroScoring
{
    /// <summary>Pontua um estado final já conhecido (ex.: craft completado in-game).</summary>
    public static float ScoreState(in SimulationState finalState, in MCTSConfig cfg)
    {
        var completion = new SimulatorNoRandom { State = finalState }.CompletionState;
        return SimulationNode.CalculateScoreForState(finalState, completion, cfg) ?? 0f;
    }

    /// <summary>Replaya as ações a partir de <paramref name="startingState"/> e pontua o resultado.</summary>
    public static float ScoreActions(
        IReadOnlyCollection<ActionType> actions, SimulatorNoRandom sim,
        in SimulationState startingState, in MCTSConfig cfg)
        => CommunityMacros.CommunityMacro.CalculateScore(actions, sim, startingState, cfg).Score;
}
