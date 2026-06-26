using Artificer.Simulator;
using Artificer.Simulator.Actions;
using System.Runtime.InteropServices;

namespace Artificer.Solver;

[StructLayout(LayoutKind.Auto)]
public struct SimulationNode(in SimulationState state, ActionType? action, CompletionState completionState, ActionSet actions)
{
    public readonly SimulationState State = state;
    public readonly ActionType? Action = action;
    public readonly CompletionState SimulationCompletionState = completionState;

    public ActionSet AvailableActions = actions;

    public readonly CompletionState CompletionState => GetCompletionState(SimulationCompletionState, AvailableActions);

    public readonly bool IsComplete => CompletionState != CompletionState.Incomplete;

    public static CompletionState GetCompletionState(CompletionState simCompletionState, ActionSet actions) =>
        actions.IsEmpty && simCompletionState == CompletionState.Incomplete ?
        CompletionState.NoMoreActions :
        simCompletionState;

    // Constante mínima para que qualquer craft completo pontue estritamente > 0 (preserva a lógica
    // "MaxScore == 0 => nenhum finish encontrado ainda" do MCTS.Search e evita score 0 quando stepBonus é 0).
    private const float CompletionBase = 0.01f;

    public readonly float? CalculateScore(in MCTSConfig config) =>
        CalculateScoreForState(State, SimulationCompletionState, config);

    public static float? CalculateScoreForState(in SimulationState state, CompletionState completionState, in MCTSConfig config)
    {
        // Objetivo estritamente lexicográfico (espelha o Raphael):
        //   completion > quality (até o target) > menos steps.
        // Só crafts completos pontuam (o gate força "síntese terminada" como prioridade máxima).
        // Durabilidade e CP NÃO entram no objetivo de propósito (são moeda de busca/viabilidade, não
        // metas) — recompensar dur/CP que sobra enche o fim do craft (upstream issues #6/#44).
        if (completionState != CompletionState.ProgressComplete)
            return null;

        var stepBonus = 1f - ((float)(state.ActionCount + 1) / config.MaxStepCount);

        var target = config.QualityTarget;

        // Receita sem quality (ou target zero): o único objetivo é menos steps.
        if (target <= 0)
            return CompletionBase + ((1f - CompletionBase) * stepBonus);

        var qualityFrac = Math.Clamp((float)state.Quality / target, 0f, 1f);

        // stepWeight fica logo abaixo do valor de UM ponto de quality, então quality domina
        // estritamente: nunca se troca quality por menos steps; steps só desempatam (quase-)empates.
        var remaining = 1f - CompletionBase;
        var stepWeight = remaining / (target + 1);
        var qualityWeight = remaining - stepWeight;

        return CompletionBase + (qualityWeight * qualityFrac) + (stepWeight * stepBonus);
    }
}
