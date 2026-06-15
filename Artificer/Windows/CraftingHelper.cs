using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Artificer.Utils;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ActionType = Artificer.Simulator.Actions.ActionType;
using ClassJob = Artificer.Simulator.ClassJob;
using CSRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;
using RecipeIngredient2 = Artificer.Utils.CSRecipeNote.RecipeIngredient;

namespace Artificer.Windows;

public sealed unsafe class CraftingHelper : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlagsPinned = WindowFlagsFloating
      | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags WindowFlagsFloating =
        ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string WindowNamePinned = "Crafting Helper###ArtificerRecipeNote";
    private const string WindowNameFloating = $"{WindowNamePinned}Floating";

    public enum CraftableStatus
    {
        OK,
        LockedClassJob,
        WrongClassJob,
        SpecialistRequired,
        RequiredItem,
        RequiredStatus,
        CraftsmanshipTooLow,
        ControlTooLow,
    }

    public AtkUnitBase* Addon { get; private set; }
    public bool IsWKS { get; private set; }

    public RecipeData? RecipeData { get; private set; }
    public CharacterStats? CharacterStats { get; private set; }
    private int StartingQuality { get; set; }
    public CraftableStatus CraftStatus { get; private set; }

    private int? _currentCharacterHash;
    private IReadOnlyList<ActionType>? _prevSuggestedActions;
    private SimulationState?           _prevSuggestedState;
    private readonly Dictionary<MacroTaskType, DateTimeOffset> _copiedAt = new();
    private BackgroundTask<(Macro?, SimulationState?, Macro?, SimulationState?)>? SavedMacroTask { get; set; }
    private BackgroundTask<SolverSolution>? SuggestedMacroTask { get; set; }
    private BackgroundTask<(CommunityMacros.CommunityMacro?, SimulationState?)>? CommunityMacroTask { get; set; }

    private Solver.Solver? BestMacroSolver { get; set; }
    public bool HasSavedMacro { get; private set; }

    private ILoadedTextureIcon ExpertBadge { get; }
    private ILoadedTextureIcon CollectibleBadge { get; }
    private IFontHandle AxisFont { get; }

    private CosmicToolTracker.ToolProgress? _cosmicProgress;
    private readonly TitleBarButton _cosmicButton;

    private readonly global::Artificer.Plugin.Plugin _plugin;

    public CraftingHelper(global::Artificer.Plugin.Plugin plugin) : base(WindowNamePinned)
    {
        _plugin = plugin;
        _cosmicProgress = _plugin.CosmicToolTracker.CachedProgress;
        _plugin.CosmicToolTracker.OnProgressChanged += OnCosmicProgressChanged;

        _cosmicButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Star,
            IconOffset  = new(2, 1),
            IconColor   = Colors.CosmicActive,
            Click       = _ => _plugin.CosmicTrackerWindow.ToggleHidden(),
            ShowTooltip = () => ImGuiUtils.Tooltip("Cosmic Tool Progress\nClick to show/hide tracker"),
        };
        ExpertBadge = IconManager.GetAssemblyTexture("Graphics.expert_badge.png");
        CollectibleBadge = IconManager.GetAssemblyTexture("Graphics.collectible_badge.png");
        AxisFont = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(-1),
            MaximumSize = new(10000, 10000)
        };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("Crafting Log"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.Plugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("Support the original author on Ko-fi")
            }
        ];

        // If already in cosmic on plugin load, show the button immediately
        if (_cosmicProgress != null)
            TitleBarButtons.Insert(0, _cosmicButton);

        _plugin.WindowSystem.AddWindow(this);
    }

    private bool IsCollapsed { get; set; }
    private bool ShouldOpen { get; set; }

    private bool WasOpen { get; set; }
    private bool WasCollapsed { get; set; }

    private bool ShouldCalculate => !IsCollapsed && ShouldOpen;
    private bool WasCalculatable { get; set; }

    public override void Update()
    {
        base.Update();

        ShouldOpen = CalculateShouldOpen();

        if (ShouldCalculate != WasCalculatable)
        {
            if (WasCalculatable)
            {
                SavedMacroTask?.Cancel();
                SuggestedMacroTask?.Cancel();
                CommunityMacroTask?.Cancel();
            }
            else if (CraftStatus == CraftableStatus.OK && !StatsChanged)
            {
                // If it didn't exist before or it already ran, we need to recalculate
                if (SavedMacroTask?.Result == null && (SavedMacroTask?.Completed ?? true))
                    CalculateSavedMacro();

                // If it didn't exist before or it already ran, we need to recalculate
                if (_plugin.Configuration.SuggestMacroAutomatically && SuggestedMacroTask?.Result == null && (SuggestedMacroTask?.Completed ?? true))
                    CalculateSuggestedMacro();
                // If we don't want to suggest automatically, we should cancel and clean out the task
                else if (!_plugin.Configuration.SuggestMacroAutomatically && SuggestedMacroTask?.Result == null)
                {
                    SuggestedMacroTask?.Cancel();
                    SuggestedMacroTask = null;
                    _prevSuggestedActions = null;
                    _prevSuggestedState   = null;
                }

                // If it didn't exist before or it already ran, we need to recalculate
                if (_plugin.Configuration.ShowCommunityMacros && _plugin.Configuration.SearchCommunityMacroAutomatically && CommunityMacroTask?.Result == null && (CommunityMacroTask?.Completed ?? true))
                    CalculateCommunityMacro();
                // If we don't want to search automatically, we should cancel and clean out the task
                else if (!_plugin.Configuration.SearchCommunityMacroAutomatically && CommunityMacroTask?.Result == null)
                {
                    CommunityMacroTask?.Cancel();
                    CommunityMacroTask = null;
                }
            }
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

    private bool StatsChanged { get; set; }
    private bool CalculateShouldOpen()
    {
        if (Service.Objects.LocalPlayer == null)
            return false;

        bool ShouldUseRecipeNote()
        {
            Addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("RecipeNote").Address;
            if (Addon == null)
                return false;

            // Check if RecipeNote addon is visible
            if (Addon->WindowNode == null)
                return false;

            // Check if RecipeNote has a visible selected recipe
            if (!Addon->GetNodeById(57)->IsVisible())
                return false;

            return true;
        }

        bool ShouldUseWKSRecipeNote()
        {
            Addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("WKSRecipeNotebook").Address;
            if (Addon == null)
                return false;

            // Check if WKS addon is visible
            if (Addon->WindowNode == null)
                return false;

            // Check if WKS has a visible selected recipe
            if (!Addon->GetNodeById(13)->IsVisible())
                return false;

            return true;
        }

        if (ShouldUseRecipeNote())
            IsWKS = false;
        else if (ShouldUseWKSRecipeNote())
            IsWKS = true;
        else
            return false;

        StatsChanged = false;
        {
            var instance = CSRecipeNote.Instance();

            var list = instance->RecipeList;
            if (list == null)
                return false;

            var recipeEntry = list->SelectedRecipe;
            if (recipeEntry == null)
                return false;

            var recipeId = recipeEntry->RecipeId;
            if (recipeId != RecipeData?.RecipeId)
            {
                RecipeData = new(recipeId);
                StatsChanged = true;
            }
        }

        var equipped = Gearsets.TryComputeCurrentStats(RecipeData.ClassJob.GetPlayerLevel(), RecipeData.ClassJob.CanPlayerUseManipulation());
        if (equipped == null)
            return false;

        var (characterStats, gearItems) = equipped.Value;
        if (characterStats != CharacterStats)
        {
            // Re-initialize recipe data to recalculate adjusted level if needed
            if (RecipeData.AdjustedJobLevel != null && characterStats.Level != CharacterStats?.Level)
            {
                RecipeData = new(RecipeData.RecipeId);
            }
            CharacterStats = characterStats;
            _currentCharacterHash = CharacterStats.ComputeHash(characterStats);
            StatsChanged = true;
        }

        var craftStatus = CalculateCraftStatus(gearItems);
        if (craftStatus != CraftStatus)
        {
            CraftStatus = craftStatus;
            StatsChanged = true;
        }

        var startingQuality = RecipeData.CalculateStartingQuality(CalculateIngredientHqCounts());
        var qualityChanged = startingQuality != StartingQuality;
        if (qualityChanged)
            StartingQuality = startingQuality;

        if ((StatsChanged || qualityChanged) && CraftStatus == CraftableStatus.OK)
        {
            // Stats changed and we are still craftable, so we need to recalculate
            CalculateSavedMacro();

            // If we want to suggest automatically, we should recalculate
            if (_plugin.Configuration.SuggestMacroAutomatically)
                CalculateSuggestedMacro();
            // Otherwise, we should cancel and clean out the task
            else
            {
                SuggestedMacroTask?.Cancel();
                SuggestedMacroTask = null;
                _prevSuggestedActions = null;
                _prevSuggestedState   = null;
            }

            // If we want to search automatically, we should recalculate
            if (_plugin.Configuration.ShowCommunityMacros && _plugin.Configuration.SearchCommunityMacroAutomatically)
                CalculateCommunityMacro();
            // Otherwise, we should cancel and clean out the task
            else
            {
                CommunityMacroTask?.Cancel();
                CommunityMacroTask = null;
            }
        }

        return true;
    }

    private IEnumerable<int> CalculateIngredientHqCounts()
    {
        if (RecipeData == null)
            throw new InvalidOperationException("RecipeData must not be null");

        var ingredientCount = RecipeData.Ingredients.Count;
        var ingredientSpan = MemoryMarshal.Cast<CSRecipeNote.RecipeIngredient, RecipeIngredient2>(CSRecipeNote.Instance()->RecipeList->SelectedRecipe->Ingredients);
        return ingredientSpan.ToArray().Take(ingredientCount).Select(i => (int)i.HQCount);
    }

    private Vector2? LastPosition { get; set; }
    private byte? StyleAlpha { get; set; }
    private byte? LastAlpha { get; set; }
    private float _anchoredMaxWidth = 10000f;
    public override void PreDraw()
    {
        base.PreDraw();

        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(_anchoredMaxWidth, -1), MaximumSize = new(_anchoredMaxWidth, 10000) };

        IsCollapsed = true;

        if (_plugin.Configuration.PinRecipeNoteToWindow)
        {
            ref var unit = ref *Addon;
            var scale = unit.Scale;
            var pos = new Vector2(unit.X, unit.Y);
            var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

            var newAlpha = unit.WindowNode->AtkResNode.Alpha_2;
            StyleAlpha = LastAlpha ?? newAlpha;
            LastAlpha = newAlpha;

            uint nodeId, nodeParentId;
            if (IsWKS)
            {
                nodeId = 15;
                nodeParentId = 13;
            }
            else
            {
                nodeId = 59;
                nodeParentId = 57;
            }

            var node = Addon->GetNodeById(nodeId);
            var nodeParent = Addon->GetNodeById(nodeParentId);
            var newPosition = pos + new Vector2(size.X, (nodeParent->Y + node->Y) * scale);

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
        IsCollapsed = false;

        var availWidth = ImGui.GetContentRegionAvail().X;
        using (var crPanel = ImRaii2.GroupPanel("Crafter / Recipe", -1, out _))
        {
            if (crPanel)
            {
                using var table = ImRaii.Table("stats", 2, ImGuiTableFlags.SizingFixedSame | ImGuiTableFlags.NoSavedSettings);
                if (table)
                {
                    if (StatsChanged)
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 150 * ImGuiHelpers.GlobalScale);
                    }

                    ImGui.TableNextColumn();
                    DrawCharacterStats();
                    ImGui.TableNextColumn();
                    DrawRecipeStats();

                    // Ensure that we know the window should be the same size as this table. Any more and it'll grow slowly and won't shrink when it could
                    ImGui.SameLine(0, 0);
                    availWidth = ImGui.GetCursorPosX() - ImGui.GetStyle().WindowPadding.X + ImGui.GetStyle().CellPadding.X;
                }
            }
        }
        availWidth += ImGui.GetStyle().ItemSpacing.X * 2;
        _anchoredMaxWidth = availWidth + ImGui.GetStyle().WindowPadding.X * 2;

        if (CraftStatus != CraftableStatus.OK)
            return;

        ImGui.Spacing();

        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;

        {
            var savedResult = SavedMacroTask?.Result;
            var bestMacro  = savedResult?.Item1;
            var state = new MacroTaskState()
            {
                Type          = MacroTaskType.Saved,
                Exception     = SavedMacroTask?.Exception,
                Started       = SavedMacroTask != null,
                Completed     = SavedMacroTask?.Completed ?? false,
                Actions       = bestMacro?.Actions,
                MacroName     = bestMacro?.Name,
                State         = savedResult?.Item2,
                HasHashMismatch = bestMacro?.CharacterStatsHash != null
                               && bestMacro.CharacterStatsHash != _currentCharacterHash,
            };
            if (bestMacro is { } savedMacro)
                state.MacroEditorSetter = a => { savedMacro.ActionEnumerable = a; _plugin.MacroRepository.Update(savedMacro); };
            DrawMacro(in state, panelWidth);
        }

        {
            var solverResult   = SuggestedMacroTask?.Result;
            var solverDone     = SuggestedMacroTask?.Completed ?? false;
            var savedResult    = SavedMacroTask?.Result;
            var hashMatch      = savedResult?.Item3;
            var hashMatchState = savedResult?.Item4;
            var isRegenerating = _prevSuggestedActions != null
                              && SuggestedMacroTask is { Completed: false };
            var isPrefilled    = hashMatch != null && !solverDone && !isRegenerating;

            var state = new MacroTaskState()
            {
                Type      = MacroTaskType.Suggested,
                Exception = SuggestedMacroTask?.Exception,
                Started   = SuggestedMacroTask != null || isPrefilled,
                Completed = solverDone || isPrefilled,
                Actions   = solverResult?.Actions ?? (isPrefilled ? hashMatch!.Actions : null),
                State     = solverResult?.State   ?? (isPrefilled ? hashMatchState     : null),
                Solver    = BestMacroSolver,
                IsPrefilled          = isPrefilled && solverResult == null,
                IsRegenerating       = isRegenerating,
                RegeneratingSnapshot = isRegenerating
                    ? (_prevSuggestedActions!, _prevSuggestedState!.Value)
                    : null,
            };

            if (solverDone)
            {
                _prevSuggestedActions = null;
                _prevSuggestedState   = null;
            }

            DrawMacro(in state, panelWidth);
        }

        if (_plugin.Configuration.ShowCommunityMacros)
        {
            var macroTaskResult = CommunityMacroTask?.Result;
            var state = new MacroTaskState()
            {
                Type = MacroTaskType.Community,
                Exception = CommunityMacroTask?.Exception,
                Started = CommunityMacroTask != null,
                Completed = CommunityMacroTask?.Completed ?? false,
                Actions = macroTaskResult?.Item1?.Actions,
                MacroName = macroTaskResult?.Item1?.Name,
                MacroUrl = macroTaskResult?.Item1?.Url,
                State = macroTaskResult?.Item2,
            };
            DrawMacro(in state, panelWidth);
        }

        ImGuiHelpers.ScaledDummy(5);

        {
            var task = SuggestedMacroTask;
            Theme.PushPrimaryButton();
            if (task != null && !task.Completed)
            {
                if (task.Cancelling)
                {
                    using var _disabled = ImRaii.Disabled();
                    ImGui.Button("Stopping", new(availWidth, 0));
                }
                else
                {
                    if (ImGui.Button("Stop", new(availWidth, 0)))
                        task.Cancel();
                }
            }
            else
            {
                var hasResult = task?.Result != null;
                var label = hasResult ? "Regenerate" : "Suggest Macro";
                if (ImGui.Button(label, new(availWidth, 0)))
                    CalculateSuggestedMacro();
                if (ImGui.IsItemHovered())
                    ImGuiUtils.TooltipWrapped(hasResult
                        ? "Generate a new macro suggestion from scratch, discarding the current one."
                        : "Suggest a way to finish the crafting recipe. " +
                          "Results aren't perfect, and levels of success " +
                          "can vary wildly depending on the solver's settings.");
            }
            Theme.PopPrimaryButton();
        }

        ImGuiHelpers.ScaledDummy(3);

        if (ImGui.Button("Open in Macro Editor", new(availWidth, 0)))
            _plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), CalculateIngredientHqCounts(), [], null);

        if (ImGui.Button("View Saved Macros", new(availWidth, 0)))
            _plugin.OpenMacroListWindow();
    }

    private void DrawCharacterStats()
    {
        var level = RecipeData!.ClassJob.GetPlayerLevel();
        {
            var textClassName = RecipeData.ClassJob.GetAbbreviation();
            var textClassSize = AxisFont.CalcTextSize(textClassName);
            var levelText = string.Empty;
            if (level != 0)
                levelText = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(level);
            var imageSize = ImGui.GetFrameHeight();
            bool hasSplendorous = false, hasSpecialist = false, shouldHaveManip = false;
            if (CraftStatus is not (CraftableStatus.LockedClassJob or CraftableStatus.WrongClassJob))
            {
                hasSplendorous = CharacterStats!.HasSplendorousBuff;
                hasSpecialist = CharacterStats!.IsSpecialist;
                shouldHaveManip = !CharacterStats.CanUseManipulation && CharacterStats.Level >= ActionType.Manipulation.Level();
            }

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                textClassSize.X +
                (level == 0 ? 0 : (3 + ImGui.CalcTextSize(levelText).X)) +
                (hasSplendorous ? (3 + imageSize) : 0) +
                (hasSpecialist ? (3 + imageSize) : 0) +
                (shouldHaveManip ? (3 + imageSize) : 0)
                );
            ImGui.AlignTextToFramePadding();

            var uv0 = UIConstants.ItemIconUv0;
            var uv1 = UIConstants.ItemIconUv1;

            ImGui.Image(_plugin.IconManager.GetIconCached(RecipeData.ClassJob.GetIconId()).Handle, new Vector2(imageSize), uv0, uv1);
            ImGui.SameLine(0, 5);

            if (level != 0)
            {
                ImGui.TextUnformatted(levelText);
                ImGui.SameLine(0, 3);
            }

            AxisFont.Text(textClassName);

            if (hasSplendorous)
            {
                ImGui.SameLine(0, 3);
                ImGuiUtils.DrawBadge(Service.IconManager.GetAssemblyTextureCached("Graphics.splendorous.png").Handle.Handle, new Vector2(imageSize), "Splendorous Tool");
            }

            if (hasSpecialist)
            {
                ImGui.SameLine(0, 3);
                ImGuiUtils.DrawBadge(Service.IconManager.GetAssemblyTextureCached("Graphics.specialist.png").Handle.Handle, new Vector2(imageSize), "Specialist", Colors.SpecialistGold);
            }

            if (shouldHaveManip)
            {
                ImGui.SameLine(0, 3);
                ImGuiUtils.DrawBadge(Service.IconManager.GetAssemblyTextureCached("Graphics.no_manip.png").Handle.Handle, new Vector2(imageSize), "No Manipulation (Missing Job Quest)");
            }
        }

        switch (CraftStatus)
        {
            case CraftableStatus.LockedClassJob:
                {
                    ImGuiUtils.TextCentered($"You do not have {RecipeData.ClassJob.GetName()} unlocked.");
                    ImGui.Separator();
                    var unlockQuest = RecipeData.ClassJob.GetUnlockQuest();
                    var (questGiver, questTerritory, questLocation, mapPayload) = ResolveLevelData(unlockQuest.IssuerLocation.RowId);

                    var unlockText = $"Unlock it from {questGiver}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGui.GetFrameHeight());
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Open in map");

                    ImGuiUtils.TextCentered($"{questTerritory} ({GetCoordinatesString(questLocation)})");
                }
                break;
            case CraftableStatus.WrongClassJob:
                {
                    ImGuiUtils.TextCentered($"You are not {RecipeData.ClassJob.GetNameArticle()} {RecipeData.ClassJob.GetName()}.");
                    var gearsetId = GetGearsetForJob(RecipeData.ClassJob);
                    if (gearsetId.HasValue)
                    {
                        if (ImGuiUtils.ButtonCentered("Switch Job"))
                            RaptureGearsetModule.Instance()->EquipGearset(gearsetId.Value);
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Swap to gearset {gearsetId + 1}");
                    }
                    else
                        ImGuiUtils.TextCentered($"You do not have any {RecipeData.ClassJob.GetName()} gearsets.");
                    ImGui.Dummy(default);
                }
                break;
            case CraftableStatus.SpecialistRequired:
                {
                    ImGuiUtils.TextCentered($"You need to be a specialist to craft this recipe.");

                    var (vendorName, vendorTerritory, vendorLoation, mapPayload) = ResolveLevelData(5891399);

                    var unlockText = $"Trade a Soul of the Crafter to {vendorName}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGui.GetFrameHeight());
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Flag))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip("Open in map");

                    ImGuiUtils.TextCentered($"{vendorTerritory} ({GetCoordinatesString(vendorLoation)})");
                }
                break;
            case CraftableStatus.RequiredItem:
                {
                    var item = RecipeData.Recipe.ItemRequired.Value!;
                    var itemName = item.Name.ToString();
                    var imageSize = ImGui.GetFrameHeight();

                    ImGuiUtils.TextCentered($"You are missing the required equipment.");
                    ImGuiUtils.AlignCentered(imageSize + 5 + ImGui.CalcTextSize(itemName).X);
                    ImGui.AlignTextToFramePadding();
                    ImGui.Image(_plugin.IconManager.GetIconCached(item.Icon).Handle, new(imageSize));
                    ImGui.SameLine(0, 5);
                    ImGui.TextUnformatted(itemName);
                }
                break;
            case CraftableStatus.RequiredStatus:
                {
                    var status = RecipeData.Recipe.StatusRequired.Value!;
                    var statusName = status.Name.ToString();
                    var statusIcon = _plugin.IconManager.GetIconCached(status.Icon);
                    var imageSize = new Vector2(ImGui.GetFrameHeight() * (statusIcon.AspectRatio ?? 1), ImGui.GetFrameHeight());

                    ImGuiUtils.TextCentered($"You are missing the required status effect.");
                    ImGuiUtils.AlignCentered(imageSize.X + 5 + ImGui.CalcTextSize(statusName).X);
                    ImGui.AlignTextToFramePadding();
                    ImGui.Image(statusIcon.Handle, imageSize);
                    ImGui.SameLine(0, 5);
                    ImGui.TextUnformatted(statusName);
                }
                break;
            case CraftableStatus.CraftsmanshipTooLow:
                {
                    ImGuiUtils.TextCentered("Your Craftsmanship is too low.");

                    DrawRequiredStatsTable(CharacterStats!.Craftsmanship, RecipeData.Recipe.RequiredCraftsmanship);
                }
                break;
            case CraftableStatus.ControlTooLow:
                {
                    ImGuiUtils.TextCentered("Your Control is too low.");

                    DrawRequiredStatsTable(CharacterStats!.Control, RecipeData.Recipe.RequiredControl);
                }
                break;
            case CraftableStatus.OK:
                {
                    using var table = ImRaii.Table("characterStats", 2);
                    if (table)
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("Craftsmanship");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats!.Craftsmanship}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("Control");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats.Control}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("CP");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats.CP}");
                    }
                }
                break;
        }

        // Gear condition indicator (if CraftStatus is OK)
        if (CraftStatus == CraftableStatus.OK && _plugin.Configuration.ShowGearCondition)
        {
            var gearCondition = Gearsets.GetMinimumGearCondition();
            if (gearCondition.HasValue)
            {
                ImGuiHelpers.ScaledDummy(2);
                
                var conditionPercent = gearCondition.Value;
                var conditionColor = conditionPercent switch
                {
                    >= 70f => new Vector4(0.3f, 0.9f, 0.3f, 1f),
                    >= 30f => new Vector4(0.9f, 0.9f, 0.3f, 1f),
                    _      => new Vector4(0.9f, 0.3f, 0.3f, 1f)
                };

                using (ImRaii.PushColor(ImGuiCol.Text, conditionColor))
                {
                    ImGuiUtils.TextCentered($"⚙ Gear: {conditionPercent:0}%");
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGuiUtils.Tooltip("Condição mínima do equipamento atual.\nRepare antes de craftar se estiver baixo.");
                }
            }
        }

        using var _spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawRecipeStats()
    {
        {
            var textStars = new string('★', RecipeData!.Table.Stars);
            var textStarsSize = Vector2.Zero;
            if (!string.IsNullOrEmpty(textStars))
            {
                textStarsSize = AxisFont.CalcTextSize(textStars);
            }
            var textLevel = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(RecipeData.AdjustedJobLevel ?? RecipeData.RecipeInfo.ClassJobLevel);
            var isExpert = RecipeData.RecipeInfo.IsExpert;
            var isCollectable = RecipeData.IsCollectable;
            var isAdjustable = RecipeData.AdjustedJobLevel.HasValue;
            var imageSize = ImGui.GetFrameHeight();
            var textSize = ImGui.GetFontSize();
            var badgeSize = new Vector2(textSize * (ExpertBadge.AspectRatio ?? 1), textSize);
            var badgeOffset = (imageSize - badgeSize.Y) / 2;

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                ImGui.CalcTextSize(textLevel).X +
                (textStarsSize != Vector2.Zero ? textStarsSize.X + 3 : 0) +
                (isAdjustable ? imageSize + 3 : 0) +
                (isCollectable ? badgeSize.X + 3 : 0) +
                (isExpert ? badgeSize.X + 3 : 0)
                );
            ImGui.AlignTextToFramePadding();

            ImGui.Image(_plugin.IconManager.GetIconCached(RecipeData.Recipe.ItemResult.Value!.Icon).Handle, new Vector2(imageSize));

            ImGui.SameLine(0, 5);
            ImGui.TextUnformatted(textLevel);

            if (textStarsSize != Vector2.Zero)
            {
                ImGui.SameLine(0, 3);

                // Aligns better
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1);
                AxisFont.Text(textStars);
            }

            if (isAdjustable)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(Service.IconManager.GetIconCached(60810).Handle, new(imageSize));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Cosmic Exploration");
            }

            if (isCollectable)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(CollectibleBadge.Handle, badgeSize);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Collectible");
            }

            if (isExpert)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(ExpertBadge.Handle, badgeSize);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Expert Recipe");
            }
        }

        using var table = ImRaii.Table("recipeStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Progress");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxProgress}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Quality");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxQuality}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Durability");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxDurability}");

            if (RecipeData.IsCosmicExploration
                && _plugin.Configuration.EnableCosmicToolTracking
                && _cosmicProgress is { } cp)
            {
                var active = cp.Types[cp.ActiveType];
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"Research Type {CosmicRomanType(cp.ActiveType)}");
                ImGui.TableNextColumn();
                var frac = active.Needed > 0 ? (float)active.Current / active.Needed : 0f;
                ImGui.ProgressBar(frac, new Vector2(-1, ImGui.GetTextLineHeight()));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"{active.Current:N0} / {active.Needed:N0}  ({frac:P0})");
            }
        }
    }

    private static string CosmicRomanType(int zeroBasedType) => (zeroBasedType + 1) switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV",
        5 => "V", 6 => "VI", 7 => "VII",
        _ => (zeroBasedType + 1).ToString()
    };

    private enum MacroTaskType
    {
        Saved,
        Suggested,
        Community
    }
    private record struct MacroTaskState
    {
        public MacroTaskType Type;
        public Exception? Exception;
        public bool Started;
        public bool Completed;
        public IReadOnlyList<ActionType>? Actions;
        public string? MacroName;
        public string? MacroUrl;
        public SimulationState? State;
        public Solver.Solver? Solver;
        public Action<IEnumerable<ActionType>>? MacroEditorSetter;
        public bool HasHashMismatch;  // only valid for MacroTaskType.Saved
        public bool IsPrefilled;      // only valid for MacroTaskType.Suggested
        public bool IsRegenerating;   // only valid for MacroTaskType.Suggested
        public (IReadOnlyList<ActionType> Actions, SimulationState State)? RegeneratingSnapshot;
    }

    private void DrawMacro(in MacroTaskState state, float panelWidth)
    {
        var panelTitle = state.Type switch
        {
            MacroTaskType.Saved => "Best Saved Macro",
            MacroTaskType.Suggested => "Suggested Macro",
            MacroTaskType.Community => "Best Community Macro",
            _ => throw new ArgumentOutOfRangeException(nameof(state), "state.Type must have a valid type")
        };

        Action? titleSuffix = state.IsPrefilled ? () =>
        {
            ImGui.SameLine(0, 4);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled)))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(FontAwesomeIcon.Bookmark.ToIconString());
            }
            if (ImGui.IsItemHovered())
                ImGuiUtils.Tooltip("Pre-filled from saved macro — solver still comparing");
        } : null;

        using var panel = ImRaii2.GroupPanel(panelTitle, panelWidth, out _, titleSuffix: titleSuffix);
        if (!panel)
            return;

        var stepsAvailWidthOffset = ImGui.GetContentRegionAvail().X - panelWidth;

        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (!state.Started)
        {
            switch (state.Type)
            {
                case MacroTaskType.Saved:
                    throw new InvalidOperationException("Saved macro window should always be started or completed");
                case MacroTaskType.Suggested:
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                            ImGuiUtils.TextMiddleNewLine(
                                "Click \"Suggest Macro\" below to get a suggestion",
                                new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                        break;
                    }
                case MacroTaskType.Community:
                    {
                        using var _padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, ImGui.GetStyle().FramePadding * 2);
                        var size = ImGui.CalcTextSize("Search Online") + ImGui.GetStyle().FramePadding * 2;
                        var c = ImGui.GetCursorPos();
                        var availSize = new Vector2(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight);
                        ImGuiUtils.AlignMiddle(size, availSize);
                        if (ImGui.Button("Search Online"))
                            CalculateCommunityMacro();
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.TooltipWrapped("Searches FFXIV Teamcraft to find you the best macro");
                        ImGui.SetCursorPos(c + new Vector2(0, availSize.Y + ImGui.GetStyle().ItemSpacing.Y));
                        break;
                    }
            }
        }
        else if (state.IsRegenerating && state.RegeneratingSnapshot is { } rsnap)
        {
            // Desenha o card anterior dimmed como âncora de tamanho
            var cardTopPos = ImGui.GetCursorPos();
            var spacing    = ImGui.GetStyle().ItemSpacing;
            var miniRowH   = (windowHeight - spacing.Y) / 2f;
            var arcColW    = miniRowH * 2 + spacing.X;
            var innerW     = panelWidth;
            var rightColW  = MathF.Max(1f, innerW - arcColW - 1f);
            var botRowH    = ImGui.GetFrameHeight();

            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.25f))
            {
                using var table = ImRaii.Table("macroCardRegen", 2,
                    ImGuiTableFlags.None,
                    new Vector2(innerW, 0));
                if (table)
                {
                    ImGui.TableSetupColumn("left",  ImGuiTableColumnFlags.WidthFixed, arcColW);
                    ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthFixed, rightColW);

                    // Row 1: arcs | action icons
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
                    ImGui.TableSetColumnIndex(0);
                    PluginImGuiUtils.DrawMacroStatArcs(rsnap.State, windowHeight, asGrid: true);

                    ImGui.TableSetColumnIndex(1);
                    {
                        var itemsPerRow = (int)MathF.Floor((rightColW + spacing.X) / (miniRowH + spacing.X));
                        itemsPerRow     = Math.Max(1, itemsPerRow);
                        var itemCount   = rsnap.Actions.Count;
                        for (var i = 0; i < itemsPerRow * 2; i++)
                        {
                            if (i % itemsPerRow != 0)
                                ImGui.SameLine(0, spacing.X);
                            if (i < itemCount)
                                ImGui.Image(rsnap.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowH));
                            else
                                ImGui.Dummy(new(miniRowH));
                        }
                    }

                    // Row 2: HQ% | "Regenerando..."
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, botRowH);
                    ImGui.TableSetColumnIndex(0);
                    {
                        var hqPct = rsnap.State.HQPercent;
                        ImGui.AlignTextToFramePadding();
                        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                            ImGuiUtils.TextCentered($"{hqPct}%", arcColW);
                    }
                    ImGui.TableSetColumnIndex(1);
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        {
                            ImGui.AlignTextToFramePadding();
                            ImGui.TextUnformatted("Regenerando...");
                        }
                    }
                }
            }

            // Overlay: progress bar centrada verticalmente sobre o card
            var afterCardPos = ImGui.GetCursorPos(); // save cursor at bottom of card
            if (state.Solver is { } regenSolver)
            {
                var cardH     = windowHeight + spacing.Y + botRowH;
                var snapshot  = SolverProgressBar.FromSolver(regenSolver, "Solver");
                var barConfig = new ProgressBarComponent.VisualConfig(
                    Mode: ProgressBarComponent.DisplayMode.Horizontal,
                    ColorTheme: _plugin.Configuration.ProgressType,
                    Width: panelWidth,
                    ShowPercentage: true,
                    ShowDetailedTooltip: true
                );

                // Fundo semi-transparente — posicionar cursor antes de obter screenPos
                var barH     = ImGui.GetFrameHeightWithSpacing();
                var overlayY = cardTopPos.Y + (cardH - barH) / 2f;
                ImGui.SetCursorPos(new Vector2(cardTopPos.X, overlayY));
                var screenMin = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(
                    screenMin,
                    screenMin + new Vector2(panelWidth, barH),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.14f, 0.85f)),
                    3f);

                ProgressBarComponent.DrawSingle(snapshot, barConfig);
                ImGui.SetCursorPos(afterCardPos); // restore cursor to bottom of card
            }
        }
        else if (!state.Completed)
        {
            switch (state.Type)
            {
                case MacroTaskType.Saved:
                    ImGuiUtils.TextMiddleNewLine("Calculating...", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
                case MacroTaskType.Suggested:
                    {
                        if (state.Solver is not { } solver)
                            throw new ArgumentNullException(nameof(state), "Solver should not be null");

                        var snapshot = SolverProgressBar.FromSolver(solver, "Solver");
                        var config = new ProgressBarComponent.VisualConfig(
                            Mode: ProgressBarComponent.DisplayMode.Horizontal,
                            ColorTheme: _plugin.Configuration.ProgressType,
                            Width: panelWidth,
                            ShowPercentage: true,
                            ShowDetailedTooltip: true
                        );
                        
                        ProgressBarComponent.DrawSingle(snapshot, config);
                        break;
                    }
                case MacroTaskType.Community:
                    ImGuiUtils.TextMiddleNewLine("Searching...", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
            }
        }
        else if (state.Exception != null)
        {
            ImGui.AlignTextToFramePadding();
            using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                ImGuiUtils.TextCentered("An exception occurred");
            if (ImGuiUtils.ButtonCentered("Copy Error Message"))
                ImGui.SetClipboardText(state.Exception.ToString());
        }
        else if (state.Actions is not { } actions || state.State is not { } simState)
        {
            switch (state.Type)
            {
                case MacroTaskType.Saved:
                {
                    var availW  = ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset;
                    var iconH   = ImGui.GetTextLineHeight() * 1.6f;
                    var totalH  = iconH + ImGui.GetStyle().ItemSpacing.Y
                                       + ImGui.GetTextLineHeightWithSpacing()
                                       + ImGui.GetTextLineHeight();
                    var startY  = ImGui.GetCursorPosY() + Math.Max(0f, (windowHeight - totalH) / 2f);
                    ImGui.SetCursorPosY(startY);
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        ImGuiUtils.TextCentered(FontAwesomeIcon.FolderOpen.ToIconString(), availW);
                    ImGuiUtils.TextCentered("No saved macro for this recipe", availW);
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        ImGuiUtils.TextCentered("Create one in the Macro Editor or solve below.", availW);
                    ImGui.SetCursorPosY(startY + windowHeight + ImGui.GetStyle().ItemSpacing.Y);
                    break;
                }
                case MacroTaskType.Suggested:
                    // Cancelled?
                    break;
                case MacroTaskType.Community:
                    ImGuiUtils.TextMiddleNewLine("No macros found!", new(ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset, windowHeight + 1));
                    break;
            }
        }
        else
        {
            if (actions.Any(a => a.Category() == ActionCategory.Combo))
                throw new InvalidOperationException("Combo actions should be sanitized away");

            if (actions.Count == 0)
            {
                var availW   = ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset;
                var iconH    = ImGui.GetTextLineHeight() * 1.6f;
                var hasRetry = state.Type == MacroTaskType.Suggested;
                var totalH   = iconH + ImGui.GetStyle().ItemSpacing.Y
                                     + ImGui.GetTextLineHeightWithSpacing()
                                     + ImGui.GetTextLineHeight()
                                     + (hasRetry
                                         ? ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight()
                                         : 0f);
                var startY = ImGui.GetCursorPosY() + Math.Max(0f, (windowHeight - totalH) / 2f);
                ImGui.SetCursorPosY(startY);

                using (ImRaii.PushFont(UiBuilder.IconFont))
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGuiUtils.TextCentered(FontAwesomeIcon.ExclamationTriangle.ToIconString(), availW);

                ImGuiUtils.TextCentered("Couldn't generate a macro", availW);

                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGuiUtils.TextCentered("Try adjusting solver settings", availW);

                if (hasRetry && ImGuiUtils.ButtonCentered("Suggest Again"))
                    CalculateSuggestedMacro();

                ImGui.SetCursorPosY(startY + windowHeight + ImGui.GetStyle().ItemSpacing.Y);
            }
            else
            {
                var spacing   = ImGui.GetStyle().ItemSpacing;
                var miniRowH  = (windowHeight - spacing.Y) / 2f;
                var arcColW   = miniRowH * 2 + spacing.X;
                var botRowH   = ImGui.GetFrameHeight();
                var innerW    = panelWidth;
                var rightColW = MathF.Max(1f, innerW - arcColW - 1f);

                using var table = ImRaii.Table("macroCard", 2,
                    ImGuiTableFlags.None,
                    new Vector2(innerW, 0));
                if (table)
                {
                    ImGui.TableSetupColumn("left",  ImGuiTableColumnFlags.WidthFixed, arcColW);
                    ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthFixed, rightColW);

                    // ── Row 1: 2×2 arc grid | action slots ──────────────────────────
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
                    ImGui.TableSetColumnIndex(0);
                    PluginImGuiUtils.DrawMacroStatArcs(simState, windowHeight, asGrid: true);

                    ImGui.TableSetColumnIndex(1);
                    {
                        var itemsPerRow = (int)MathF.Floor((rightColW + spacing.X) / (miniRowH + spacing.X));
                        itemsPerRow     = Math.Max(1, itemsPerRow);
                        var itemCount   = actions.Count;
                        for (var i = 0; i < itemsPerRow * 2; i++)
                        {
                            if (i % itemsPerRow != 0)
                                ImGui.SameLine(0, spacing.X);
                            if (i < itemCount)
                            {
                                var shouldShowMore = i + 1 == itemsPerRow * 2 && i + 1 < itemCount;
                                if (!shouldShowMore)
                                {
                                    ImGui.Image(actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowH));
                                    if (ImGui.IsItemHovered())
                                        ImGuiUtils.Tooltip(actions[i].GetName(RecipeData!.ClassJob));
                                }
                                else
                                {
                                    var amtMore = itemCount - itemsPerRow * 2;
                                    var aPos = ImGui.GetCursorPos();
                                    ImGui.Image(actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowH), default, Vector2.One, new(1, 1, 1, .5f));
                                    if (ImGui.IsItemHovered())
                                        ImGuiUtils.Tooltip($"{actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
                                    ImGui.SetCursorPos(aPos);
                                    ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowH), ImGui.GetColorU32(ImGuiCol.FrameBg), miniRowH / 8f);
                                    ImGui.GetWindowDrawList().AddTextClippedEx(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowH), $"+{amtMore}", null, new(.5f), null);
                                }
                            }
                            else
                                ImGui.Dummy(new(miniRowH));
                        }
                    }

                    // ── Row 2: HQ % | macro name + edit + copy ──────────────────────
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, botRowH);
                    ImGui.TableSetColumnIndex(0);
                    {
                        var hqPct    = simState.HQPercent;
                        var pctColor = RecipeData!.RecipeInfo.MaxQuality <= 0 ? Colors.TextMuted :
                                       hqPct >= 100 ? Colors.Progress :
                                       hqPct >=  75 ? Colors.Quality :
                                       hqPct >=  50 ? Colors.ActionBuff :
                                                      Colors.Bad;
                        ImGui.AlignTextToFramePadding();
                        using (ImRaii.PushColor(ImGuiCol.Text, pctColor))
                            ImGuiUtils.TextCentered($"{hqPct}%", arcColW);
                    }

                    ImGui.TableSetColumnIndex(1);
                    {
                        if (state.HasHashMismatch)
                        {
                            using (ImRaii.PushFont(UiBuilder.IconFont))
                            using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                            {
                                ImGui.AlignTextToFramePadding();
                                ImGui.TextUnformatted(FontAwesomeIcon.ExclamationTriangle.ToIconString());
                            }
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.TooltipWrapped("This macro was saved with different character stats and may not perform as expected");
                            ImGui.SameLine();
                        }

                        var cellStart  = ImGui.GetCursorPos();
                        var cellAvailW = ImGui.GetContentRegionAvail().X;
                        var iconH      = botRowH;

                        var editX = cellStart.X + cellAvailW - iconH * 2 - spacing.X;
                        ImGui.SetCursorPos(new Vector2(editX, cellStart.Y));
                        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Edit, iconH))
                            _plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), CalculateIngredientHqCounts(), actions, state.MacroEditorSetter);
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip("Open in Macro Editor");
                        ImGui.SameLine(0, spacing.X);
                        var justCopied = _copiedAt.TryGetValue(state.Type, out var copiedAt)
                                      && (DateTimeOffset.UtcNow - copiedAt).TotalSeconds < 2.0;
                        var copyIcon   = justCopied ? FontAwesomeIcon.Check : FontAwesomeIcon.Paste;
                        bool copyClicked;
                        if (justCopied)
                        {
                            using (ImRaii.PushColor(ImGuiCol.Text, Colors.Progress))
                                copyClicked = ImGuiUtils.IconButtonSquare((int)copyIcon, iconH);
                        }
                        else
                        {
                            copyClicked = ImGuiUtils.IconButtonSquare((int)copyIcon, iconH);
                        }
                        if (copyClicked)
                        {
                            MacroCopy.Copy(actions, _plugin);
                            _copiedAt[state.Type] = DateTimeOffset.UtcNow;
                        }
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip(justCopied ? "Copied!" : "Copy to Clipboard");

                        var nameMaxW  = cellAvailW - iconH * 2 - spacing.X * 2;
                        ImGui.SetCursorPos(new Vector2(cellStart.X, cellStart.Y + (botRowH - ImGui.GetTextLineHeight()) * 0.5f));
                        var nameScrnMin = ImGui.GetCursorScreenPos();
                        ImGui.PushClipRect(nameScrnMin, nameScrnMin + new Vector2(nameMaxW, botRowH), true);
                        var displayName = state.MacroName ?? (state.Type == MacroTaskType.Suggested ? "AI Suggestion" : "");
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            if (state.MacroUrl is { } macroUrl)
                                ImGuiUtils.Hyperlink(displayName, macroUrl, false);
                            else
                            {
                                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                                    ImGui.TextUnformatted(displayName);
                            }
                        }
                        ImGui.PopClipRect();
                    }
                }
            }
        }
    }

    private static void DrawRequiredStatsTable(int current, int required)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(current, required);

        using var table = ImRaii.Table("requiredStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Current");
            ImGui.TableNextColumn();
            ImGui.TextColored(Colors.Good, $"{current}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Required");
            ImGui.TableNextColumn();
            ImGui.TextColored(Colors.Bad, $"{required}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("You need");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{required - current}");
        }
    }

    private CraftableStatus CalculateCraftStatus(Gearsets.GearsetItem[] gearItems)
    {
        if (RecipeData!.ClassJob.GetPlayerLevel() == 0)
            return CraftableStatus.LockedClassJob;

        if (PlayerState.Instance()->CurrentClassJobId != RecipeData.ClassJob.GetClassJobIndex())
            return CraftableStatus.WrongClassJob;

        if (RecipeData.Recipe.IsSpecializationRequired && !CharacterStats!.IsSpecialist)
            return CraftableStatus.SpecialistRequired;

        var itemRequired = RecipeData.Recipe.ItemRequired;
        if (itemRequired.RowId != 0 && itemRequired.IsValid)
        {
            if (!gearItems.Any(i => Gearsets.IsItem(i, itemRequired.RowId)))
                return CraftableStatus.RequiredItem;
        }

        var statusRequired = RecipeData.Recipe.StatusRequired;
        if (statusRequired.RowId != 0 && statusRequired.IsValid)
        {
            if (!Service.Objects.LocalPlayer!.StatusList.Any(s => s.StatusId == statusRequired.RowId))
                return CraftableStatus.RequiredStatus;
        }

        if (RecipeData.Recipe.RequiredCraftsmanship > CharacterStats!.Craftsmanship)
            return CraftableStatus.CraftsmanshipTooLow;

        if (RecipeData.Recipe.RequiredControl > CharacterStats.Control)
            return CraftableStatus.ControlTooLow;

        return CraftableStatus.OK;
    }

    private static (string NpcName, string Territory, Vector2 MapLocation, MapLinkPayload Payload) ResolveLevelData(uint levelRowId)
    {
        var level = LuminaSheets.LevelSheet.GetRow(levelRowId);
        var placeName = level.Territory.Value.PlaceName.Value.Name.ToString();
        var location = WorldToMap2(new(level.X, level.Z), level.Map.Value!);

        return (ResolveNpcResidentName(level.Object.RowId), placeName, location, new(level.Territory.RowId, level.Map.RowId, location.X, location.Y));
    }

    private static Vector2 WorldToMap2(Vector2 worldCoordinates, Lumina.Excel.Sheets.Map map)
    {
        return MapUtil.WorldToMap(worldCoordinates, map.OffsetX, map.OffsetY, map.SizeFactor);
    }

    private static string ResolveNpcResidentName(uint npcRowId)
    {
        return Service.SeStringEvaluator.EvaluateObjStr(ObjectKind.EventNpc, npcRowId);
    }

    private static string GetCoordinatesString(Vector2 pos)
    {
        return $"{pos.X.ToString("0.0", CultureInfo.InvariantCulture)}, {pos.Y.ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    private static int? GetGearsetForJob(ClassJob job)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        var i = -1;
        foreach (ref var gearset in gearsetModule->Entries)
        {
            i++;

            if (!gearset.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
            if (gearset.Id != i)
                continue;
            if (gearset.ClassJob != job.GetClassJobIndex())
                continue;

            return i;
        }
        return null;
    }

    private void CalculateSavedMacro()
    {
        SavedMacroTask?.Cancel();
        var hasDelineations = Gearsets.HasDelineations();
        var currentHash = _currentCharacterHash;
        SavedMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo, StartingQuality);
            var state = new SimulationState(input);
            var config = _plugin.Configuration.RecipeNoteSolverConfig;
            var canUseDelineations = !_plugin.Configuration.CheckDelineations || hasDelineations;
            if (!canUseDelineations)
                config = config.FilterSpecialistActions();
            var mctsConfig = new MCTSConfig(config);
            var simulator = new SimulatorNoRandom();
            List<Macro> macros = [.. _plugin.MacroRepository.Macros];

            token.ThrowIfCancellationRequested();

            if (macros.Count == 0)
                return (null, null, null, null);

            var results = macros
                .Select(macro =>
                {
                    var (score, outState) = CommunityMacros.CommunityMacro.CalculateScore(macro.Actions, simulator, in state, in mctsConfig);
                    return (macro, outState, score);
                })
                .ToList();

            token.ThrowIfCancellationRequested();

            var bestSaved = results.MaxBy(m => m.score);

            var hashMatchResults = currentHash.HasValue
                ? results.Where(r => r.macro.CharacterStatsHash == currentHash && r.score > 0).ToList()
                : new();
            (Macro macro, SimulationState? outState, float score)? bestHashMatch = hashMatchResults.Count > 0
                ? hashMatchResults.MaxBy(r => r.score)
                : null;

            return (bestSaved.macro, bestSaved.outState, bestHashMatch?.macro, bestHashMatch?.outState);
        });
        SavedMacroTask.Start();
    }

    private void CalculateSuggestedMacro()
    {
        if (SuggestedMacroTask?.Result is { } prev)
        {
            _prevSuggestedActions = prev.Actions;
            _prevSuggestedState   = prev.State;
        }
        else
        {
            _prevSuggestedActions = null;
            _prevSuggestedState   = null;
        }
        SuggestedMacroTask?.Cancel();
        var hasDelineations = Gearsets.HasDelineations();
        SuggestedMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo, StartingQuality);
            var state = new SimulationState(input);
            var config = _plugin.Configuration.RecipeNoteSolverConfig;
            var canUseDelineations = !_plugin.Configuration.CheckDelineations || hasDelineations;
            if (!canUseDelineations)
                config = config.FilterSpecialistActions();

            token.ThrowIfCancellationRequested();

            var solver = new Solver.Solver(config, state) { Token = token };
            solver.OnLog += Log.Debug;
            solver.OnWarn += t => Plugin.Plugin.DisplaySolverWarning(t);
            BestMacroSolver = solver;
            solver.Start();
            var solution = solver.GetTask().GetAwaiter().GetResult();

            token.ThrowIfCancellationRequested();

            return solution;
        });
        SuggestedMacroTask.Start();
    }

    public void CalculateCommunityMacro()
    {
        CommunityMacroTask?.Cancel();
        var hasDelineations = Gearsets.HasDelineations();
        CommunityMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo, StartingQuality);
            var state = new SimulationState(input);
            var config = _plugin.Configuration.RecipeNoteSolverConfig;
            var canUseDelineations = !_plugin.Configuration.CheckDelineations || hasDelineations;
            if (!canUseDelineations)
                config = config.FilterSpecialistActions();
            var mctsConfig = new MCTSConfig(config);
            var simulator = new SimulatorNoRandom();
            var macros = _plugin.CommunityMacros.RetrieveRotations((int)RecipeData.Table.RowId, token).GetAwaiter().GetResult();

            token.ThrowIfCancellationRequested();

            if (macros.Count == 0)
                return (null, null);
            var bestSaved = macros
                .Select(macro =>
                {
                    var (score, outState) = CommunityMacros.CommunityMacro.CalculateScore(macro.Actions, simulator, in state, in mctsConfig);
                    return (macro, outState, score);
                })
                .MaxBy(m => m.score);

            token.ThrowIfCancellationRequested();

            return (bestSaved.macro, bestSaved.outState);
        });
        CommunityMacroTask.Start();
    }

    private void OnCosmicProgressChanged(CosmicToolTracker.ToolProgress? progress)
    {
        _cosmicProgress = progress;

        // Update star button color (amber when mission active, violet otherwise)
        _cosmicButton.IconColor = progress?.MissionActive == true
            ? Colors.CosmicMission
            : Colors.CosmicActive;

        // Add or remove the cosmic button from the title bar
        if (progress != null && !TitleBarButtons.Contains(_cosmicButton))
            TitleBarButtons.Insert(0, _cosmicButton);
        else if (progress == null)
            TitleBarButtons.Remove(_cosmicButton);
    }

    public void Dispose()
    {
        _plugin.CosmicToolTracker.OnProgressChanged -= OnCosmicProgressChanged;
        SavedMacroTask?.Dispose();
        SuggestedMacroTask?.Dispose();
        CommunityMacroTask?.Dispose();
        _plugin.WindowSystem.RemoveWindow(this);
        AxisFont?.Dispose();
        ExpertBadge.Dispose();
        CollectibleBadge.Dispose();
    }
}
