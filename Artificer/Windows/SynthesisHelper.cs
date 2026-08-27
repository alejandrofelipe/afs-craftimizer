using Artificer.Application.Crafting;
using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionType = Artificer.Simulator.Actions.ActionType;
using Sim = Artificer.Simulator.Simulator;
using SimNoRandom = Artificer.Simulator.SimulatorNoRandom;

namespace Artificer.Windows;

public sealed unsafe partial class SynthesisHelper : Window, IDisposable
{
    private static readonly EffectType[] AllEffectTypes = Enum.GetValues<EffectType>();
    private const ImGuiWindowFlags WindowFlagsPinned = WindowFlagsFloating
      | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags WindowFlagsFloating =
        ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string WindowNamePinned = "Synthesis Helper###ArtificerSynthHelper";
    private const string WindowNameFloating = $"{WindowNamePinned}Floating";

    public AddonSynthesis* Addon { get; private set; }
    public RecipeData? RecipeData => Session.RecipeData;
    public CharacterStats? CharacterStats => Session.CharacterStats;
    public SimulationInput? SimulationInput => Session.SimulationInput;
    public ActionType? NextAction => ShouldOpen ? Session.NextAction : null;
    public bool ShouldDrawAnts => ShouldOpen && !IsCollapsed;

    private CraftingSession Session { get; }
    private readonly global::Artificer.Plugin.Plugin _plugin;
    private IFontHandle AxisFont { get; }
    private CosmicToolTracker.ToolProgress? _cosmicProgress;
    private readonly TitleBarButton _cosmicButton;

    public SynthesisHelper(global::Artificer.Plugin.Plugin plugin) : base(WindowNamePinned)
    {
        _plugin = plugin;
        Session = new CraftingSession(plugin);
        AxisFont = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        _cosmicProgress = plugin.CosmicToolTracker.CachedProgress;
        _plugin.CosmicToolTracker.OnProgressChanged += OnCosmicProgressChanged;

        _cosmicButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Star,
            IconOffset  = new(2, 1),
            IconColor   = _cosmicProgress?.MissionActive == true ? Colors.CosmicMission : Colors.CosmicActive,
            Click       = _ => _plugin.CosmicTrackerWindow.ToggleHidden(),
            ShowTooltip = () => ImGuiUtils.Tooltip("Cosmic Tool Progress\nClick to show/hide tracker"),
        };

        _plugin.Hooks.OnActionUsed += OnUseAction;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(UIConstants.SynthHelperWidth, -1),
            MaximumSize = new(UIConstants.SynthHelperWidth, 10000)
        };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("Synthesis Helper"),
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

        _plugin.WindowSystem.AddWindow(this);
    }

    private bool IsCollapsed { get; set; }
    private bool ShouldOpen { get; set; }

    private bool WasOpen { get; set; }
    private bool WasCollapsed { get; set; }

    /// <summary>
    /// Used to automatically collapse the helper window when a new craft starts.
    /// </summary>
    private bool ShouldCollapse { get; set; }

    private bool ShouldCalculate => !IsCollapsed && ShouldOpen;
    private bool WasCalculatable { get; set; }

    public override void Update()
    {
        base.Update();

        ShouldOpen = CalculateShouldOpen();

        if (ShouldCalculate != WasCalculatable)
        {
            if (WasCalculatable)
                Session.CancelSolver();
            else if (Session.Macro.Count == 0)
                RefreshCurrentState();
        }

        if (Session.Macro.Count == 0 && ShouldOpen)
        {
            if (ShouldOpen != WasOpen || IsCollapsed != WasCollapsed)
                RefreshCurrentState();
        }

        if (!ShouldOpen)
        {
            StyleAlpha = LastAlpha = null;
            LastPosition = null;
        }

        WasOpen = ShouldOpen;
        WasCollapsed = IsCollapsed;
        WasCalculatable = ShouldCalculate;
    }

    public override bool DrawConditions() =>
        ShouldOpen;

    private bool wasInCraftAction;
    private bool CalculateShouldOpen()
    {
        if (Service.Objects.LocalPlayer == null)
            return false;

        if (!_plugin.Configuration.EnableSynthHelper)
            return false;

        var recipeId = CSRecipeNote.Instance()->ActiveCraftRecipeId;

        if (recipeId == 0)
        {
            Session.ClearRecipe();
            return false;
        }

        Addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis").Address;

        if (Addon == null)
        {
            Session.ClearRecipe();
            return false;
        }

        // Check if Synthesis addon is visible
        if (Addon->AtkUnitBase.WindowNode == null)
            return false;

        if (_plugin.Configuration.DisableSynthHelperOnMacro)
        {
            var module = RaptureShellModule.Instance();
            if (module->MacroCurrentLine >= 0)
            {
                var hasCraftAction = false;
                foreach (ref var line in module->MacroLines)
                {
                    if (line.EqualToString("/craftaction"))
                    {
                        hasCraftAction = true;
                        break;
                    }
                }
                if (!hasCraftAction)
                    return false;
            }
        }

        if (Session.RecipeData?.RecipeId != recipeId)
        {
            var newRecipeData = new RecipeData(recipeId);
            var characterStats = ComputeCharacterStats(newRecipeData);
            Session.StartCrafting(newRecipeData, characterStats);
            Session.SetCurrentState(GetCurrentState(), ShouldCalculate);

            if (_plugin.Configuration.CollapseSynthHelper) ShouldCollapse = true;
        }

        if (Session.IsRecalculateQueued)
            Session.SetCurrentState(GetCurrentState(), ShouldCalculate);

        Session.FlushMacroQueue();

        // Once the solver finishes, compare its result against the saved macro and
        // use whichever is better as the displayed suggestion.
        Session.TryFinalizeSolverComparison();

        var isInCraftAction = Service.Condition[ConditionFlag.ExecutingCraftingAction];
        if (!isInCraftAction && wasInCraftAction)
        {
            Session.SetCurrentState(GetCurrentState(), ShouldCalculate);
            Session.TryAutoSaveMacro();
        }
        wasInCraftAction = isInCraftAction;

        return true;
    }

    private Vector2? LastPosition { get; set; }
    private byte? StyleAlpha { get; set; }
    private byte? LastAlpha { get; set; }
    public override void PreDraw()
    {
        base.PreDraw();

        IsCollapsed = true;

        if (_plugin.Configuration.PinSynthHelperToWindow)
        {
            ref var unit = ref Addon->AtkUnitBase;
            var scale = unit.Scale;
            var pos = new Vector2(unit.X, unit.Y);
            var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

            var offset = 5;

            var newAlpha = unit.WindowNode->AtkResNode.Alpha_2;
            StyleAlpha = LastAlpha ?? newAlpha;
            LastAlpha = newAlpha;

            var newPosition = pos + new Vector2(size.X, offset * scale);
            Position = ImGuiHelpers.MainViewport.Pos + (LastPosition ?? newPosition);
            LastPosition = newPosition;
            Flags = WindowFlagsPinned;
            WindowName = WindowNamePinned;
        }
        else
        {
            StyleAlpha = LastAlpha = null;
            Position = LastPosition = null;
            Flags = WindowFlagsFloating;
            WindowName = WindowNameFloating;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, StyleAlpha.HasValue ? (StyleAlpha.Value / 255f) : 1);
        Theme.Push();
    }

    public override void PostDraw()
    {
        Theme.Pop();
        ImGui.PopStyleVar();

        base.PostDraw();
    }

    public override void Draw()
    {

        if (ShouldCollapse)
        {
            ImGui.SetWindowCollapsed(true);
            ShouldCollapse = false;
        }

        IsCollapsed = false;

        DrawMacro();

        ImGuiHelpers.ScaledDummy(3);

        DrawMacroInfo();

        ImGuiHelpers.ScaledDummy(3);

        DrawMacroExecutionProgress();

        if (Session.SolverSnapshots.Any())
        {
            ImGuiHelpers.ScaledDummy(2);
            PluginImGuiUtils.DrawSolverProgressArea(
                ImGui.GetContentRegionAvail().X,
                Session.SolverSnapshots.ToArray(),
                _plugin.Configuration.ProgressType);
        }

        DrawMacroActions();
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
        _plugin.Hooks.OnActionUsed -= OnUseAction;
        Session.Dispose();
        _plugin.WindowSystem.RemoveWindow(this);
        AxisFont.Dispose();
    }
}
