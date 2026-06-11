using Artificer.Plugin;
using System.Collections.Generic;
using System.Linq;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Builds and holds an in-memory, name-sorted index of every craftable recipe,
/// used by the crafting-list UI to search for recipes by item name.
/// </summary>
public sealed class RecipeSearchHelper
{
    public IReadOnlyList<RecipeSearchResult> Index { get; }

    public RecipeSearchHelper()
    {
        Index = LuminaSheets.RecipeSheet
            .Where(r => r.ItemResult.RowId != 0)
            .Select(r => new RecipeSearchResult(
                RecipeId: r.RowId,
                ItemId: r.ItemResult.RowId,
                ItemName: r.ItemResult.ValueNullable?.Name.ExtractText() ?? string.Empty,
                JobAbbrev: GetJobAbbrev(r.CraftType.RowId),
                Level: (int)(r.RecipeLevelTable.ValueNullable?.ClassJobLevel ?? 0)
            ))
            .Where(r => r.ItemName.Length > 0)
            .OrderBy(r => r.ItemName, System.StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static string GetJobAbbrev(uint craftTypeId) => craftTypeId switch
    {
        0 => "CRP", 1 => "BSM", 2 => "ARM", 3 => "GSM",
        4 => "LTW", 5 => "WVR", 6 => "ALC", 7 => "CUL",
        _ => "???"
    };
}

public sealed record RecipeSearchResult(
    uint RecipeId, uint ItemId,
    string ItemName, string JobAbbrev, int Level
);
