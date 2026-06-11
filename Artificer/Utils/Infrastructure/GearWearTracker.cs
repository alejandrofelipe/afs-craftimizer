using Artificer.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Artificer.Utils;

/// <summary>
/// Tracks gear durability consumption during crafts to predict when gear will need repair.
/// </summary>
public class GearWearTracker : IDisposable
{
    /// <summary>
    /// Statistics for gear wear on a specific recipe.
    /// </summary>
    public class GearWearStats
    {
        /// <summary>
        /// Recipe ID from game data.
        /// </summary>
        public uint RecipeId { get; set; }

        /// <summary>
        /// Recipe level for variance tracking.
        /// </summary>
        public ushort RecipeLevel { get; set; }

        /// <summary>
        /// Total condition loss observed (sum of all samples).
        /// </summary>
        public float TotalConditionLoss { get; set; }

        /// <summary>
        /// Number of crafts sampled for this recipe.
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// Average condition loss per craft.
        /// </summary>
        [JsonIgnore]
        public float AverageConditionLoss => SampleCount > 0 ? TotalConditionLoss / SampleCount : 0f;

        /// <summary>
        /// Confidence level (0-1) based on sample count.
        /// </summary>
        [JsonIgnore]
        public float Confidence => Math.Min(1f, SampleCount / 10f);
    }

    private readonly Plugin.Plugin _plugin;
    private float? _preCraftCondition;
    private uint _currentRecipeId;
    private ushort _currentRecipeLevel;
    private bool _isCrafting;

    public GearWearTracker(Plugin.Plugin plugin)
    {
        _plugin = plugin;
        
        // Subscribe to craft start/end events
        _plugin.Hooks.OnActionUsed += OnActionUsed;
    }

    private unsafe void OnActionUsed(Simulator.Actions.ActionType action)
    {
        // Only track if feature is enabled
        if (!_plugin.Configuration.EnableGearWearTracking)
            return;

        // Check if we're in synthesis window
        var recipeId = CSRecipeNote.Instance()->ActiveCraftRecipeId;
        if (recipeId == 0)
        {
            ResetTracking();
            return;
        }

        // First action of craft - capture initial condition
        if (!_isCrafting)
        {
            StartCraftTracking(recipeId);
        }
    }

    private unsafe void StartCraftTracking(uint recipeId)
    {
        _isCrafting = true;
        _currentRecipeId = recipeId;
        
        // Get recipe level
        var recipe = LuminaSheets.RecipeSheet.GetRowOrDefault(recipeId);
        _currentRecipeLevel = (ushort)(recipe?.RecipeLevelTable.RowId ?? 0);

        // Capture pre-craft condition
        _preCraftCondition = Gearsets.GetMinimumGearCondition();

        // Subscribe to condition flag to detect craft end
        Service.Framework.Update += OnFrameworkUpdate;
    }

    private unsafe void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        if (!_isCrafting)
        {
            Service.Framework.Update -= OnFrameworkUpdate;
            return;
        }

        // Check if craft is complete (synthesis window closed or no longer in craft action)
        var recipeId = CSRecipeNote.Instance()->ActiveCraftRecipeId;
        var isInCraftAction = Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting];
        
        if (recipeId == 0 || !isInCraftAction)
        {
            // Give a frame delay to ensure condition updates
            if (_preCraftCondition.HasValue)
            {
                EndCraftTracking();
            }
            else
            {
                ResetTracking();
            }
        }
    }

    private void EndCraftTracking()
    {
        if (!_preCraftCondition.HasValue)
        {
            ResetTracking();
            return;
        }

        // Capture post-craft condition
        var postCraftCondition = Gearsets.GetMinimumGearCondition();
        
        if (!postCraftCondition.HasValue)
        {
            ResetTracking();
            return;
        }

        // Calculate condition loss
        var conditionLoss = _preCraftCondition.Value - postCraftCondition.Value;

        // Ignore invalid data (negative loss or unreasonably high loss)
        if (conditionLoss < 0f || conditionLoss > 10f)
        {
            Log.Debug($"[GearWearTracker] Ignoring invalid condition loss: {conditionLoss:F2}%");
            ResetTracking();
            return;
        }

        // Store the data
        RecordWearData(_currentRecipeId, _currentRecipeLevel, conditionLoss);

        Log.Debug($"[GearWearTracker] Recipe {_currentRecipeId}: Lost {conditionLoss:F2}% condition");

        ResetTracking();
    }

    private void RecordWearData(uint recipeId, ushort recipeLevel, float conditionLoss)
    {
        var key = GetRecipeKey(recipeId, recipeLevel);
        
        if (!_plugin.Configuration.GearWearData.TryGetValue(key, out var stats))
        {
            stats = new GearWearStats
            {
                RecipeId = recipeId,
                RecipeLevel = recipeLevel
            };
            _plugin.Configuration.GearWearData[key] = stats;
        }

        stats.TotalConditionLoss += conditionLoss;
        stats.SampleCount++;

        _plugin.Configuration.Save();
    }

    private void ResetTracking()
    {
        _isCrafting = false;
        _preCraftCondition = null;
        _currentRecipeId = 0;
        _currentRecipeLevel = 0;
        Service.Framework.Update -= OnFrameworkUpdate;
    }

    /// <summary>
    /// Estimate how many crafts can be performed with current gear condition.
    /// </summary>
    /// <param name="recipeId">Recipe to craft.</param>
    /// <param name="recipeLevel">Recipe level.</param>
    /// <returns>Estimated crafts remaining, or null if no data available.</returns>
    public (int MinCrafts, int MaxCrafts, float Confidence)? EstimateCraftsRemaining(uint recipeId, ushort recipeLevel)
    {
        var currentCondition = Gearsets.GetMinimumGearCondition();
        if (!currentCondition.HasValue)
            return null;

        var key = GetRecipeKey(recipeId, recipeLevel);
        if (!_plugin.Configuration.GearWearData.TryGetValue(key, out var stats))
        {
            // No data - use conservative fallback estimate (~1% per craft)
            return EstimateWithFallback(currentCondition.Value);
        }

        if (stats.SampleCount == 0)
            return EstimateWithFallback(currentCondition.Value);

        var avgLoss = stats.AverageConditionLoss;
        if (avgLoss <= 0f)
            return null;

        // Conservative estimate (assume higher loss for min)
        var conservativeLoss = avgLoss * 1.2f;
        var minCrafts = (int)Math.Floor(currentCondition.Value / conservativeLoss);

        // Optimistic estimate (use exact average)
        var maxCrafts = (int)Math.Floor(currentCondition.Value / avgLoss);

        return (Math.Max(0, minCrafts), Math.Max(0, maxCrafts), stats.Confidence);
    }

    /// <summary>
    /// Get average condition loss for a recipe, or null if no data.
    /// </summary>
    public float? GetAverageConditionLoss(uint recipeId, ushort recipeLevel)
    {
        var key = GetRecipeKey(recipeId, recipeLevel);
        if (!_plugin.Configuration.GearWearData.TryGetValue(key, out var stats))
            return null;

        return stats.SampleCount > 0 ? stats.AverageConditionLoss : null;
    }

    /// <summary>
    /// Fallback estimation when no tracking data exists (conservative 1% per craft).
    /// </summary>
    private static (int MinCrafts, int MaxCrafts, float Confidence) EstimateWithFallback(float currentCondition)
    {
        const float conservativeLoss = 1.0f; // 1% per craft
        var crafts = (int)Math.Floor(currentCondition / conservativeLoss);
        return (crafts, crafts, 0f); // 0 confidence = no tracking data
    }

    private static string GetRecipeKey(uint recipeId, ushort recipeLevel) => $"{recipeId}:{recipeLevel}";

    public void Dispose()
    {
        _plugin.Hooks.OnActionUsed -= OnActionUsed;
        Service.Framework.Update -= OnFrameworkUpdate;
    }
}
