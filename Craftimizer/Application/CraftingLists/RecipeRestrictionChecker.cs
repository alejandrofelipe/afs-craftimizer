using Craftimizer.Plugin;
using System.Collections.Generic;

namespace Craftimizer.Application.CraftingLists;

public enum RecipeRestrictionKind { MasterRecipeBook, Specialist, QuestUnlock, ExpertRecipe }

public sealed record RecipeRestriction(
    RecipeRestrictionKind Kind, string Title, string Detail, string HowToDetail);

/// <summary>
/// Determines which acquisition/usage restrictions apply to a recipe
/// (master recipe book, specialist, quest unlock, expert), with per-recipe caching.
/// </summary>
public sealed class RecipeRestrictionChecker
{
    private readonly Dictionary<uint, IReadOnlyList<RecipeRestriction>> _cache = new();

    public IReadOnlyList<RecipeRestriction> GetRestrictions(uint recipeId)
    {
        if (_cache.TryGetValue(recipeId, out var cached))
            return cached;
        var result = Compute(recipeId);
        _cache[recipeId] = result;
        return result;
    }

    private static List<RecipeRestriction> Compute(uint recipeId)
    {
        if (LuminaSheets.RecipeSheet.GetRowOrDefault(recipeId) is not { } recipe)
            return [];

        var result = new List<RecipeRestriction>();

        // SecretRecipeBook (master recipe book)
        if (recipe.SecretRecipeBook.RowId != 0)
        {
            var name = recipe.SecretRecipeBook.ValueNullable?.Name.ExtractText() ?? "Livro mestre";
            result.Add(new(RecipeRestrictionKind.MasterRecipeBook, "Requer livro mestre",
                $"Obtenha \"{name}\" no Scrip Exchange.", "White Crafters' Scrips"));
        }

        // Specialist
        if (recipe.IsSpecializationRequired)
        {
            var job = GetJobName(recipe.CraftType.RowId);
            result.Add(new(RecipeRestrictionKind.Specialist, "Requer Specialist",
                $"Torne-se Specialist em {job}.",
                "Use um Soul of the Crafter via Ixali Tribal Quests ou eventos."));
        }

        // Quest unlock
        if (recipe.Quest.RowId != 0)
        {
            var questName = recipe.Quest.ValueNullable?.Name.ExtractText() ?? "quest de desbloqueio";
            result.Add(new(RecipeRestrictionKind.QuestUnlock, "Desbloqueada por quest",
                $"Complete \"{questName}\".", string.Empty));
        }

        // Expert
        if (recipe.IsExpert)
            result.Add(new(RecipeRestrictionKind.ExpertRecipe, "Receita Expert",
                "Condições variáveis — síntese mais exigente.", string.Empty));

        return result;
    }

    private static string GetJobName(uint craftTypeId) => craftTypeId switch
    {
        0 => "Carpenter", 1 => "Blacksmith", 2 => "Armorer", 3 => "Goldsmith",
        4 => "Leatherworker", 5 => "Weaver", 6 => "Alchemist", 7 => "Culinarian",
        _ => "crafter"
    };
}
