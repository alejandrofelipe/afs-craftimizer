using Artificer.Plugin;
using System;
using System.Collections.Generic;

namespace Artificer.Application.Crafting;

/// <summary>Decisões puras dos fluxos de busca e auto-save de macro.</summary>
public static class MacroSelection
{
    public enum AutoSaveOutcome { Insert, Overwrite, Skip }

    /// <summary>Decide o que o auto-save faz com a macro Auto da receita.</summary>
    public static AutoSaveOutcome DecideAutoSave(float? existingScore, float newScore, float epsilon = 0.001f)
    {
        if (existingScore is null)
            return AutoSaveOutcome.Insert;
        return newScore > existingScore.Value + epsilon ? AutoSaveOutcome.Overwrite : AutoSaveOutcome.Skip;
    }

    /// <summary>
    /// Melhor macro (maior score &gt; 0) para a receita, considerando qualquer origem.
    /// Ignora macros de outras receitas ou sem ações. Empate mantém a primeira encontrada.
    /// </summary>
    public static Macro? SelectBestForRecipe(IEnumerable<Macro> macros, ushort recipeId, Func<Macro, float> score)
    {
        Macro? best = null;
        var bestScore = 0f;
        foreach (var m in macros)
        {
            if (m.RecipeId != recipeId || m.Actions.Count == 0)
                continue;
            var s = score(m);
            if (s > 0f && (best is null || s > bestScore))
            {
                best = m;
                bestScore = s;
            }
        }
        return best;
    }
}
