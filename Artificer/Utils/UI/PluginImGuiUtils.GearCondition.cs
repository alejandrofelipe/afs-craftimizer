namespace Artificer.Utils;

internal static partial class PluginImGuiUtils
{
    public static string BuildGearMessage(
        float pct,
        bool enableTracking,
        RecipeData? recipeData,
        GearWearTracker tracker)
    {
        if (!enableTracking || recipeData == null)
            return $"{pct:0}% — Repair gear before continuing!";

        var recipe      = recipeData.Recipe;
        var recipeLevel = (ushort)recipeData.Table.RowId;
        var estimate    = tracker.EstimateCraftsRemaining(recipe.RowId, recipeLevel);

        return estimate switch
        {
            null                   => $"{pct:0}% — Repair gear before continuing!",
            { Confidence: > 0f } e => e.MinCrafts == e.MaxCrafts
                ? $"{pct:0}% · ~{e.MinCrafts} crafts left"
                : $"{pct:0}% · ~{e.MinCrafts}–{e.MaxCrafts} crafts left",
            { } e                  => $"{pct:0}% · ~{e.MinCrafts} crafts left (no data)",
        };
    }
}
