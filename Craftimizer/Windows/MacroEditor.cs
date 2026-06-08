using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sim = Craftimizer.Simulator.Simulator;
using SimNoRandom = Craftimizer.Simulator.SimulatorNoRandom;
using Recipe = Lumina.Excel.Sheets.Recipe;
using Dalamud.Utility;
using Craftimizer.Solver;

namespace Craftimizer.Windows;

public sealed partial class MacroEditor : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    private CharacterStats characterStats = null!;
    public CharacterStats CharacterStats
    {
        get => characterStats;
        private set
        {
            characterStats = value with
            {
                Craftsmanship = Math.Clamp(value.Craftsmanship, 0, UIConstants.MaxCraftStat),
                Control = Math.Clamp(value.Control, 0, UIConstants.MaxCraftStat),
                CP = Math.Clamp(value.CP, UIConstants.MinCP, UIConstants.MaxCP),
                Level = Math.Clamp(value.Level, 1, 100),
            };
        }
    }
    public RecipeData RecipeData { get; private set; }

    public record CrafterBuffs
    {
        public (int Craftsmanship, int Control) FC { get; init; }
        public (uint ItemId, bool IsHQ) Food { get; init; }
        public (uint ItemId, bool IsHQ) Medicine { get; init; }

        public CrafterBuffs(StatusList? statuses)
        {
            if (statuses == null)
                return;

            foreach (var status in statuses)
            {
                if (status.StatusId == GameConstants.CrafterStatusIds.WellFed)
                    Food = FoodStatus.ResolveFoodParam(status.Param) ?? default;
                else if (status.StatusId == GameConstants.CrafterStatusIds.Medicated)
                    Medicine = FoodStatus.ResolveFoodParam(status.Param) ?? default;
                else if (status.StatusId == GameConstants.CrafterStatusIds.InControl)
                    FC = FC with { Craftsmanship = status.Param / 5 };
                else if (status.StatusId == GameConstants.CrafterStatusIds.EatFromTheHand)
                    FC = FC with { Control = status.Param / 5 };
            }
        }
    }
    public CrafterBuffs Buffs { get; set; }

    private List<int> HQIngredientCounts { get; set; }
    private int StartingQuality => RecipeData.CalculateStartingQuality(HQIngredientCounts);

    private SimulatedMacro Macro { get; set; } = null!;
    private SimulationState State => Macro.State;
    private SimulatedMacro.Reliablity Reliability => Macro.GetReliability(RecipeData);

    private readonly global::Craftimizer.Plugin.Plugin _plugin;

    private ActionType[] DefaultActions { get; }
    private Action<IEnumerable<ActionType>>? MacroSetter { get; set; }

    private BackgroundTask<int>? SolverTask { get; set; }
    private bool SolverRunning => (!SolverTask?.Completed) ?? false;
    private Solver.Solver? SolverObject { get; set; }
    private int? SolverStartStepCount { get; set; }

    private CosmicToolTracker.ToolProgress? _cosmicProgress;
    private readonly TitleBarButton _cosmicButton;
    private ITextureIcon CosmicExplorationBadge { get; }
    private ITextureIcon SplendorousBadge { get; }
    private ITextureIcon SpecialistBadge { get; }
    private ITextureIcon NoManipulationBadge { get; }
    private ITextureIcon ManipulationBadge { get; }
    private ILoadedTextureIcon WellFedBadge { get; }
    private ILoadedTextureIcon MedicatedBadge { get; }
    private ILoadedTextureIcon InControlBadge { get; }
    private ILoadedTextureIcon EatFromTheHandBadge { get; }
    private IFontHandle AxisFont { get; }

    private string popupSaveAsMacroName = string.Empty;

    private string popupImportText = string.Empty;
    private string popupImportUrl = string.Empty;
    private string popupImportError = string.Empty;
    private CancellationTokenSource? popupImportUrlTokenSource;
    private CommunityMacros.CommunityMacro? popupImportUrlMacro;

    public MacroEditor(global::Craftimizer.Plugin.Plugin plugin, CharacterStats characterStats, RecipeData recipeData, CrafterBuffs buffs, IEnumerable<int>? ingredientHqCounts, IEnumerable<ActionType> actions, Action<IEnumerable<ActionType>>? setter) : base("Macro Editor", WindowFlags)
    {
        _plugin = plugin;
        Macro = new(_plugin.Configuration);
        CharacterStats = characterStats;
        RecipeData = recipeData;
        Buffs = buffs;
        MacroSetter = setter;
        DefaultActions = [.. actions];

        HQIngredientCounts = [.. ingredientHqCounts ?? Enumerable.Repeat(0, RecipeData.Ingredients.Count)];

        RecalculateState();
        foreach (var action in DefaultActions)
            AddStep(action);

        _cosmicProgress = _plugin.CosmicToolTracker.CachedProgress;
        _plugin.CosmicToolTracker.OnProgressChanged += OnCosmicProgressChanged;

        CosmicExplorationBadge = Service.IconManager.GetIconCached(60810);
        SplendorousBadge = Service.IconManager.GetAssemblyTextureCached("Graphics.splendorous.png");
        SpecialistBadge = Service.IconManager.GetAssemblyTextureCached("Graphics.specialist.png");
        NoManipulationBadge = Service.IconManager.GetAssemblyTextureCached("Graphics.no_manip.png");
        ManipulationBadge = ActionType.Manipulation.GetIcon(RecipeData.ClassJob);
        WellFedBadge = IconManager.GetIcon(LuminaSheets.StatusSheet.GetRow(GameConstants.CrafterStatusIds.WellFed)!.Icon);
        MedicatedBadge = IconManager.GetIcon(LuminaSheets.StatusSheet.GetRow(GameConstants.CrafterStatusIds.Medicated)!.Icon);
        InControlBadge = IconManager.GetIcon(LuminaSheets.StatusSheet.GetRow(GameConstants.CrafterStatusIds.InControl)!.Icon);
        EatFromTheHandBadge = IconManager.GetIcon(LuminaSheets.StatusSheet.GetRow(GameConstants.CrafterStatusIds.EatFromTheHand)!.Icon);
        AxisFont = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        IsOpen = true;

        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;

        _cosmicButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Star,
            IconOffset  = new(2, 1),
            IconColor   = _cosmicProgress?.MissionActive == true ? Colors.CosmicMission : Colors.CosmicActive,
            Click       = _ => _plugin.CosmicTrackerWindow.ToggleHidden(),
            ShowTooltip = () => ImGuiUtils.Tooltip("Cosmic Tool Progress\nClick to show/hide tracker"),
        };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("Macro Editor"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.Plugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("Support me on Ko-fi!")
            }
        ];

        if (_cosmicProgress != null)
            TitleBarButtons.Insert(0, _cosmicButton);

        MinWindowHeight = float.PositiveInfinity;

        _plugin.WindowSystem.AddWindow(this);
    }

    private float MinWindowHeight { get; set; }
    private static ReadOnlySpan<(float Scale, int MinWidth)> MinWindowWidths =>
        new[]
        {
            (0.80f, 715),
            (0.90f, 745),
            (0.95f, 775),
            (1.00f, 805),
            (1.10f, 865),
            (1.25f, 944),
            (1.50f, 1128),
            (2.00f, 1504),
            (3.00f, UIConstants.MacroEditorMaxWidth),
        };

    public override void PreDraw()
    {
        base.PreDraw();

        var scale = ImGuiHelpers.GlobalScale;
        var widths = MinWindowWidths;
        var height = MinWindowWidths[^1].MinWidth;
        for (var i = 0; i < widths.Length; ++i)
        {
            if (scale <= widths[i].Scale)
            {
                if (i == 0)
                    height = widths[i].MinWidth;
                else
                    height = (int)float.Lerp(
                        widths[i - 1].MinWidth, widths[i].MinWidth,
                        (scale - widths[i - 1].Scale) / (widths[i].Scale - widths[i - 1].Scale)
                    );
                break;
            }
        }
        ImGui.SetNextWindowSizeConstraints(new Vector2(height, MinWindowHeight), new Vector2(float.PositiveInfinity));
        Theme.Push();
    }

    public override void PostDraw()
    {
        Theme.Pop();
        base.PostDraw();
    }

    public override void OnClose()
    {
        base.OnClose();
        SolverTask?.Cancel();
    }

    public override void Update()
    {
        Macro.FlushQueue();
    }

    public override void Draw()
    {
        var modifiedInput = false;

        using (var table = ImRaii.Table("params", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            if (table)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextColumn();
                modifiedInput = DrawCharacterParams();
                ImGui.TableNextColumn();
                modifiedInput |= DrawRecipeParams();
            }
        }

        if (modifiedInput)
            RecalculateState();

        using (var table = ImRaii.Table("macroInfo", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            if (table)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch, 2);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch, 3);
                ImGui.TableNextColumn();
                DrawActionHotbars();
                ImGui.TableNextColumn();
                DrawMacroInfo();
                DrawMacro();
            }
        }
    }

    private void SaveMacro()
    {
        MacroSetter?.Invoke(Macro.Actions);
    }

    private void RecalculateState()
    {
        Macro.InitialState = new SimulationState(new(CharacterStats, RecipeData.RecipeInfo, StartingQuality));
    }

    private Sim CreateSim(in SimulationState state) =>
        _plugin.Configuration.ConditionRandomness ? new Sim() { State = state } : new SimNoRandom() { State = state };

    private void AddStep(ActionType action)
    {
        if (SolverRunning)
            throw new InvalidOperationException("Cannot add steps while solver is running");
        if (!SolverRunning)
            SolverStartStepCount = null;

        Macro.Add(action);
    }

    private void RemoveStep(int index)
    {
        if (SolverRunning)
            throw new InvalidOperationException("Cannot remove steps while solver is running");
        SolverStartStepCount = null;

        Macro.RemoveAt(index);
    }

    private void OnCosmicProgressChanged(CosmicToolTracker.ToolProgress? progress)
    {
        _cosmicProgress = progress;
        _cosmicButton.IconColor = progress?.MissionActive == true
            ? Colors.CosmicMission
            : Colors.CosmicActive;
        if (progress != null && !TitleBarButtons.Contains(_cosmicButton))
            TitleBarButtons.Insert(0, _cosmicButton);
        else if (progress == null)
            TitleBarButtons.Remove(_cosmicButton);
    }

    public void Dispose()
    {
        _plugin.CosmicToolTracker.OnProgressChanged -= OnCosmicProgressChanged;
        _plugin.WindowSystem.RemoveWindow(this);

        // CosmicExplorationBadge, SplendorousBadge, SpecialistBadge, NoManipulationBadge are
        // ITextureIcon from IconManager cache — lifetime managed by IconManager, not the window.
        WellFedBadge.Dispose();
        MedicatedBadge.Dispose();
        InControlBadge.Dispose();
        EatFromTheHandBadge.Dispose();
        AxisFont.Dispose();
    }
}
