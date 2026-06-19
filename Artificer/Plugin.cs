using Artificer.Application.CraftingLists;
using Artificer.Data;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Artificer.Windows;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Reflection;
using SimActionUtils = Artificer.Utils.ActionUtils;

namespace Artificer.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Version { get; }
    public string Author { get; }
    public string BuildConfiguration { get; }
    public ILoadedTextureIcon Icon { get; }
    public const string SupportLink     = "https://ko-fi.com/camora";
    public const string OriginalAuthor  = "Asriel (WorkingRobot)";
    public const string OriginalRepoLink = "https://github.com/WorkingRobot/Craftimizer";

    public WindowSystem WindowSystem { get; }
    public Settings SettingsWindow { get; }
    public CraftingHelper RecipeNoteWindow { get; }
    public SynthesisHelper SynthHelperWindow { get; }
    public MacroLibrary ListWindow { get; private set; }
    public MacroEditor? EditorWindow { get; private set; }
    public MacroClipboard? ClipboardWindow { get; private set; }
    public CosmicTracker CosmicTrackerWindow { get; }
    public CraftingListWindow CraftingListWindow { get; }
    public CraftingListAddWindow CraftingListAddWindow { get; }
    public CraftingListDetailWindow CraftingListDetailWindow { get; }
    public CraftingListMergeWindow CraftingListMergeWindow { get; }

    public Configuration Configuration { get; }
    public MacroRepository MacroRepository { get; }
    public IconManager IconManager { get; }
    public Hooks Hooks { get; }
    public GearWearTracker GearWearTracker { get; }
    public CosmicToolTracker CosmicToolTracker { get; }
    public CommunityMacros CommunityMacros { get; }
    public Ipc Ipc { get; }
    public CraftingListRepository CraftingListRepository { get; }
    public CraftingListManager CraftingListManager { get; }
    public RecipeSearchHelper RecipeSearchHelper { get; }
    public RecipeRestrictionChecker RecipeRestrictionChecker { get; }
    public GatheringLocator GatheringLocator { get; }
    public TeleportHelper TeleportHelper { get; }
    public MarketboardHelper MarketboardHelper { get; }
    public AttributeCommandManager AttributeCommandManager { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);
        UiServices.Current = new DalamudUiServices(pluginInterface);

        WindowSystem = new("Artificer");
        MacroRepository = new(pluginInterface);
        Configuration = Configuration.Load();
        // Migrate macros from legacy JSON config on first run
        if (Configuration.macros.Count > 0)
        {
            MacroRepository.MigrateFromJson(Configuration.macros);
            Configuration.macros.Clear();
            Configuration.Save();
        }
        IconManager = new(Configuration);
        Service.IconManager = IconManager;
        Hooks = new(this);
        GearWearTracker = new(this);
        CosmicToolTracker = new(this);
        CommunityMacros = new();
        CraftingListRepository = new(pluginInterface);
        CraftingListManager = new(CraftingListRepository, this);
        RecipeSearchHelper = new();
        RecipeRestrictionChecker = new();
        GatheringLocator = new();
        TeleportHelper = new(pluginInterface);
        MarketboardHelper = new(pluginInterface, CraftingListRepository);
        Ipc = new(pluginInterface);
        AttributeCommandManager = new(this);

        var assembly = Assembly.GetExecutingAssembly();
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
        BuildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        if (DateTime.Now is { Day: 1, Month: 4 })
            Icon = IconManager.GetAssemblyTexture("Graphics.horse_icon.png");
        else
            Icon = IconManager.GetAssemblyTexture("Graphics.icon.png");

        SettingsWindow = new(this);
        RecipeNoteWindow = new(this);
        SynthHelperWindow = new(this);
        ListWindow = new(this);
        CosmicTrackerWindow = new(this);
        CraftingListWindow = new(this);
        CraftingListAddWindow = new(this);
        CraftingListDetailWindow = new(this);
        CraftingListMergeWindow = new(this);

        // Trigger static constructors so a hitch doesn't occur on first RecipeNote frame.
        FoodStatus.Initialize();
        SimActionUtils.Initialize();

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += () => OpenSettingsWindow(true);
        Service.PluginInterface.UiBuilder.OpenMainUi += OpenCraftingLog;
    }

    public (CharacterStats? Character, RecipeData? Recipe, MacroEditor.CrafterBuffs? Buffs) GetOpenedStats()
    {
        var editorWindow = (EditorWindow?.IsOpen ?? false) ? EditorWindow : null;
        var recipeData = editorWindow?.RecipeData ?? RecipeNoteWindow.RecipeData;
        var characterStats = editorWindow?.CharacterStats ?? RecipeNoteWindow.CharacterStats;
        var buffs = editorWindow?.Buffs ?? (RecipeNoteWindow.CharacterStats != null ? new(Service.Objects.LocalPlayer?.StatusList) : null);

        return (characterStats, recipeData, buffs);
    }

    public (CharacterStats Character, RecipeData Recipe, MacroEditor.CrafterBuffs Buffs) GetDefaultStats()
    {
        var stats = GetOpenedStats();
        return (
            stats.Character ?? new()
            {
                Craftsmanship = 100,
                Control = 100,
                CP = 200,
                Level = 10,
                CanUseManipulation = false,
                HasSplendorousBuff = false,
                IsSpecialist = false,
            },
            stats.Recipe ?? new(1023),
            stats.Buffs ?? new(null)
        );
    }

    [Command(name: "/crafteditor", aliases: "/macroeditor", description: "Open the crafting macro editor.")]
    public void OpenEmptyMacroEditor()
    {
        var stats = GetDefaultStats();
        OpenMacroEditor(stats.Character, stats.Recipe, stats.Buffs, null, [], null);
    }

    public void OpenMacroEditor(CharacterStats characterStats, RecipeData recipeData, MacroEditor.CrafterBuffs buffs, IEnumerable<int>? ingredientHqCounts, IEnumerable<ActionType> actions, Action<IEnumerable<ActionType>>? setter)
    {
        EditorWindow?.Dispose();
        EditorWindow = new(this, characterStats, recipeData, buffs, ingredientHqCounts, actions, setter);
    }

    [Command(name: "/craftaction", description: "Execute the suggested action in the synthesis helper. Can also be run inside a macro. This command is useful for controller players.")]
    public void ExecuteSuggestedSynthHelperAction() =>
        SynthHelperWindow.ExecuteNextAction();

    [Command(name: "/craftretry", description: "Clicks \"Retry\" in the synthesis helper. Can also be run inside a macro. This command is useful for controller players.")]
    public void ExecuteRetrySynthHelper() =>
        SynthHelperWindow.AttemptRetry();

    [Command(name: "/Artificer", description: "Open the settings window.")]
    private void OnArtificerCommand(string command, string args)
    {
        OpenSettingsWindow(true);
    }

    public void OpenSettingsWindow(bool force = false)
    {
        if (SettingsWindow.IsOpen ^= !force || !SettingsWindow.IsOpen)
            SettingsWindow.BringToFront();
    }

    public void OpenSettingsTab(string selectedTabLabel)
    {
        OpenSettingsWindow(true);
        SettingsWindow.SelectTab(selectedTabLabel);
    }

    [Command(name: "/craftmacros", aliases: "/macrolist", description: "Open the crafting macros window.")]
    public void OpenMacroListWindow()
    {
        ListWindow.IsOpen = true;
        ListWindow.BringToFront();
    }

    [Command("/craftlist", "Open the crafting list window.", false, "/craftinglist", "/coleta")]
    public void OpenCraftingListWindow()
    {
        if (!Configuration.EnableCraftingLists)
        {
            DisplayNotification(new()
            {
                Content = "Ative as Listas de Coleta em Configurações → Experimental.",
                Title = "Listas de Coleta",
                Type = NotificationType.Warning
            });
            OpenSettingsTab("Experimental");
            return;
        }
        CraftingListWindow.OpenAndFocus();
    }

    public static void OpenCraftingLog()
    {
        Chat.SendMessage("/craftinglog");
    }

    public void OpenMacroClipboard(List<string> macros)
    {
        ClipboardWindow?.Dispose();
        ClipboardWindow = new(this, macros);
    }

    public static IActiveNotification DisplaySolverWarning(string text) =>
        DisplayNotification(new()
        {
            Content = text,
            Title = "Solver Warning",
            Type = NotificationType.Warning
        });

    public static IActiveNotification DisplayNotification(Notification notification)
    {
        var ret = Service.NotificationManager.AddNotification(notification);
        // ret.SetIconTexture(Icon.RentAsync().ContinueWith(t => (IDalamudTextureWrap?)t));
        return ret;
    }

    public void Dispose()
    {
        AttributeCommandManager.Dispose();
        SettingsWindow.Dispose();
        RecipeNoteWindow.Dispose();
        SynthHelperWindow.Dispose();
        ListWindow.Dispose();
        EditorWindow?.Dispose();
        ClipboardWindow?.Dispose();
        IconManager.Dispose();
        Hooks.Dispose();
        GearWearTracker.Dispose();
        CosmicToolTracker.Dispose();
        CosmicTrackerWindow.Dispose();
        CraftingListWindow.Dispose();
        CraftingListAddWindow.Dispose();
        CraftingListDetailWindow.Dispose();
        CraftingListMergeWindow.Dispose();
        Icon.Dispose();
        TeleportHelper.Dispose();
        MarketboardHelper.Dispose();
        CraftingListManager.Dispose();
        CraftingListRepository.Dispose();
        MacroRepository.Dispose();
    }
}
