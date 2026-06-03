using Craftimizer.Solver;
using Craftimizer.Utils;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Craftimizer.Plugin;

public class MacroCopyConfiguration
{
    public enum CopyType
    {
        OpenWindow, // useful for big macros
        CopyToMacro, // (add option for down or right) (max macro count; open copy-paste window if too much)
        CopyToClipboard,
        CopyToMacroMate
    }

    public CopyType Type { get; set; } = CopyType.OpenWindow;

    // CopyToMacro
    public bool CopyDown { get; set; }
    public bool SharedMacro { get; set; }
    public int StartMacroIdx { get; set; } = 1;
    public int MaxMacroCount { get; set; } = 5;

    // CopyToMacroMate
    public string MacroMateName { get; set; } = "Craftimizer";
    public string MacroMateParent { get; set; } = string.Empty;

    // Add /nextmacro [down]
    public bool UseNextMacro { get; set; }

    // Add /mlock
    public bool UseMacroLock { get; set; }

    public bool AddNotification { get; set; } = true;

    // Requires AddNotification
    public bool ForceNotification { get; set; }
    public bool AddNotificationSound { get; set; } = true;
    public int IntermediateNotificationSound { get; set; } = 10;
    public int EndNotificationSound { get; set; } = 6;

    // For SND
    public bool RemoveWaitTimes { get; set; }

    // For SND; Cannot use CopyToMacro
    public bool CombineMacro { get; set; }

    public bool ShowCopiedMessage { get; set; } = true;
}

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public partial class Configuration
{
    public enum ProgressBarType
    {
        Colorful,
        Simple,
        None
    }

    [JsonInclude] [JsonPropertyName("Macros")]
    internal List<Macro> macros { get; private set; } = [];

    public int ReliabilitySimulationCount { get; set; } = 1000;
    public bool ConditionRandomness { get; set; } = true;

    [JsonPropertyName("SimulatorSolverConfig")]
    public SolverConfig RecipeNoteSolverConfig { get; set; } = SolverConfig.RecipeNoteDefault;
    public SolverConfig EditorSolverConfig { get; set; } = SolverConfig.EditorDefault;
    public SolverConfig SynthHelperSolverConfig { get; set; } = SolverConfig.SynthHelperDefault;

    public bool EnableSynthHelper { get; set; } = true;
    public bool DisableSynthHelperOnMacro { get; set; } = true;
    public bool AutoSaveCraftMacro { get; set; } = true;
    public bool ShowOptimalMacroStat { get; set; } = true;
    public bool SuggestMacroAutomatically { get; set; }
    public bool ShowCommunityMacros { get; set; } = true;
    public bool SearchCommunityMacroAutomatically { get; set; }
    public int SynthHelperStepCount { get; set; } = 1;
    public int SynthHelperMaxDisplayCount { get; set; } = 5;
    public bool SynthHelperDisplayOnlyFirstStep { get; set; }
    public bool SynthHelperAbilityAnts { get; set; }
    public bool CheckDelineations { get; set; } = true;
    public bool ShowGearCondition { get; set; } = true;
    public ProgressBarType ProgressType { get; set; } = ProgressBarType.Colorful;

    public bool PinSynthHelperToWindow { get; set; } = true;
    public bool CollapseSynthHelper { get; set; }
    public bool PinRecipeNoteToWindow { get; set; } = true;

    /// <summary>
    /// Enable gear wear tracking system (default: false).
    /// Learns how much gear durability each recipe consumes over time.
    /// </summary>
    public bool EnableGearWearTracking { get; set; }

    /// <summary>
    /// Show warning when gear durability is low (default: true).
    /// </summary>
    public bool ShowLowDurabilityWarning { get; set; } = true;

    /// <summary>
    /// Gear condition threshold (%) to trigger low durability warning (default: 10).
    /// </summary>
    public int LowDurabilityThreshold { get; set; } = 10;

    /// <summary>
    /// Stored gear wear statistics by recipe.
    /// Key format: "recipeId:recipeLevel"
    /// </summary>
    public Dictionary<string, GearWearTracker.GearWearStats> GearWearData { get; set; } = new();

    /// <summary>
    /// Enable Cosmic Tool progression tracking (default: false).
    /// Shows research data progress for Cosmic Exploration Stellar Missions.
    /// WARNING: Experimental feature - game API still being reverse-engineered.
    /// </summary>
    public bool EnableCosmicToolTracking { get; set; }

    public MacroCopyConfiguration MacroCopy { get; set; } = new();

    /// <summary>
    /// Enable automatic icon cache cleanup (default: true)
    /// </summary>
    public bool EnableIconCacheEviction { get; set; } = true;

    /// <summary>
    /// Minutes of inactivity before icon is unloaded (default: 5)
    /// </summary>
    public int IconCacheSlidingExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum minutes icon can stay in cache (default: 30)
    /// </summary>
    public int IconCacheAbsoluteExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum number of icons in cache (0 = unlimited, default: 1024)
    /// </summary>
    public int IconCacheSizeLimit { get; set; } = 1024;

    [JsonSourceGenerationOptions(Converters = [typeof(StoredActionTypeConverter)])]
    [JsonSerializable(typeof(Configuration))]
    internal sealed partial class JsonContext : JsonSerializerContext
    {
        public static JsonSerializerOptions DeserializeOptions { get; } = new()
        {
            Converters = { new StoredActionTypeConverter() }
        };
    }

    private readonly Lock _saveLock = new();

    public void Save()
    {
        lock (_saveLock)
        {
            var f = Service.PluginInterface.ConfigFile;
            using var stream = new FileStream(f.FullName, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(stream, this, JsonContext.Default.Configuration);
        }
    }

    public static Configuration Load()
    {
        var f = Service.PluginInterface.ConfigFile;
        if (f.Exists)
        {
            using var stream = f.OpenRead();

            // System.InvalidOperationException: Setting init-only properties is not supported in source generation mode.
            return JsonSerializer.Deserialize<Configuration>(stream, JsonContext.DeserializeOptions) ?? new();
        }
        return new();
    }
}
