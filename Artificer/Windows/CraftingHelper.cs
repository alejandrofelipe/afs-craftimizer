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

public sealed unsafe partial class CraftingHelper : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlagsPinned = WindowFlagsFloating
      | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags WindowFlagsFloating =
        ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string WindowNamePinned = "Crafting Helper###ArtificerRecipeNote";
    private const string WindowNameFloating = $"{WindowNamePinned}Floating";

    public AtkUnitBase* Addon { get; private set; }
    public bool IsWKS { get; private set; }

    public RecipeData? RecipeData { get; private set; }
    public CharacterStats? CharacterStats { get; private set; }
    private int StartingQuality { get; set; }
    public CraftableStatus CraftStatus { get; private set; }

    private int? _currentCharacterHash;
    private IReadOnlyList<ActionType>? _prevSuggestedActions;
    private SimulationState?           _prevSuggestedState;
    private bool _bestShowAlternative;
    private readonly Dictionary<MacroTaskType, DateTimeOffset> _copiedAt = new();
    private BackgroundTask<SavedMacroResult>? SavedMacroTask { get; set; }
    private BackgroundTask<SuggestedMacroResult>? SuggestedMacroTask { get; set; }
    private BackgroundTask<(CommunityMacros.CommunityMacro?, SimulationState?)>? CommunityMacroTask { get; set; }

    private readonly GenerationSlot<Solver.Solver> _bestMacroSolver = new();
    private Solver.Solver? BestMacroSolver => _bestMacroSolver.Value;
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
    private bool WasSuggestMacroAutomatically { get; set; }

    public override void Update()
    {
        base.Update();

        var suggestMacroAutomatically = _plugin.Configuration.SuggestMacroAutomatically;
        ShouldOpen = CalculateShouldOpen();

        if (ShouldCalculate
         && SuggestedMacroCleanupPolicy.ShouldClearWhenAutomaticSuggestionChanges(WasSuggestMacroAutomatically, suggestMacroAutomatically)
         && SuggestedMacroTask is { Result: null })
            ClearSuggestedMacro();

        if (ShouldCalculate != WasCalculatable)
        {
            if (WasCalculatable)
            {
                SavedMacroTask?.Cancel();
                _bestMacroSolver.Invalidate(() => SuggestedMacroTask?.Cancel());
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
                    ClearSuggestedMacro();

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
        WasSuggestMacroAutomatically = suggestMacroAutomatically;
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

        var inputsChanged = StatsChanged || qualityChanged;
        if (SuggestedMacroCleanupPolicy.ShouldClearForChangedInputs(inputsChanged, CraftStatus == CraftableStatus.OK))
            ClearSuggestedMacro();

        if (inputsChanged && CraftStatus == CraftableStatus.OK)
        {
            // Stats changed and we are still craftable, so we need to recalculate
            CalculateSavedMacro();

            // If we want to suggest automatically, we should recalculate
            if (_plugin.Configuration.SuggestMacroAutomatically)
                CalculateSuggestedMacro();
            // Otherwise, we should cancel and clean out the task
            else
                ClearSuggestedMacro();

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

        var colW   = 160f * ImGuiHelpers.GlobalScale;
        var tableW = colW * 2;

        using (var crPanel = ImRaii2.GroupPanel("Crafter / Recipe", tableW, out _))
        {
            if (crPanel)
            {
                using var table = ImRaii.Table("stats", 2, ImGuiTableFlags.SizingFixedSame | ImGuiTableFlags.NoSavedSettings);
                if (table)
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, colW);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, colW);

                    ImGui.TableNextColumn();
                    DrawCharacterStats();
                    ImGui.TableNextColumn();
                    DrawRecipeStats();
                }
            }
        }
        var gpWidth = ImGui.GetItemRectSize().X;
        // INVARIANTE ANTI-CRESCIMENTO: a largura da janela é fixada (min == max) em
        // _anchoredMaxWidth no PreDraw. Ele deriva SÓ deste primeiro painel (gpWidth = a
        // caixa renderizada do painel Crafter/Recipe, de largura interna fixa tableW) e é
        // medido ANTES dos painéis de macro. Os painéis de macro nunca realimentam
        // _anchoredMaxWidth, então conteúdo que transborde um painel não pode fazer a
        // janela crescer. Não dimensione nada sob AlwaysAutoResize a partir de
        // GetContentRegionAvail() sem manter este pino.
        _anchoredMaxWidth = gpWidth + ImGui.GetStyle().WindowPadding.X * 2;

        if (CraftStatus != CraftableStatus.OK)
            return;

        ImGui.Spacing();

        var availWidth = gpWidth;
        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;

        if (_plugin.Configuration.ShowGearCondition)
        {
            var gearCondition = Gearsets.GetMinimumGearCondition();
            if (gearCondition.HasValue)
            {
                var pct     = gearCondition.Value;
                var variant = pct < 25f ? AlertVariant.Danger
                            : pct < 50f ? AlertVariant.Warning
                            :             AlertVariant.Info;
                var message = PluginImGuiUtils.BuildGearMessage(
                    pct,
                    _plugin.Configuration.EnableGearWearTracking,
                    RecipeData,
                    _plugin.GearWearTracker);
                ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale, availWidth);
                ImGui.Spacing();
            }
        }

        // ── Card unificado "Best Macro" (Saved + Suggested) ────────────────────
        {
            var savedRes = SavedMacroTask?.Result;
            var suggRes  = SuggestedMacroTask?.Result;
            var suggTask = SuggestedMacroTask;
            var suggDone = suggTask?.Completed ?? false;

            var isRegenerating = _prevSuggestedActions != null && suggTask is { Completed: false };
            var hashMatch      = savedRes?.HashMatch;
            var hashMatchState = savedRes?.HashMatchState;
            var isPrefilled    = hashMatch != null && !suggDone && !isRegenerating && suggRes == null;
            var suggException  = suggDone && suggRes == null && suggTask?.Exception != null;

            // Scores para escolher o vencedor (bestSaved vs sugerida).
            float? savedScore = savedRes?.Best != null ? savedRes.Value.BestScore : null;
            float? suggScore  = suggRes != null ? suggRes.Value.Score : null;
            var winner = ImGuiUtils.PickBestMacroSource(savedScore, suggScore);

            var bothExist = savedScore != null && suggScore != null;
            if (!bothExist)
                _bestShowAlternative = false;

            // Qual fonte exibir: o vencedor, salvo se o usuário alternou para a alternativa.
            var showSuggested = winner switch
            {
                BestMacroSource.Suggested => !_bestShowAlternative,
                BestMacroSource.Saved     =>  _bestShowAlternative,
                _                         => false,
            };

            var state = new MacroTaskState();

            // branches: exceção(7) · progresso/prefill(2) · showSuggested(4) · saved(0/1/3/5/6)
            if (suggException && savedRes?.Best == null)   // só mostra o card de exceção quando não há salva p/ cair de volta
            {
                state.Type        = MacroTaskType.Suggested;
                state.Exception   = suggTask!.Exception;
                state.Started     = true;
                state.Completed   = true;
                state.TitleSuffix = BuildBestMacroBadge(BestMacroSource.Suggested, false);
            }
            else if (suggTask is { Completed: false } || isPrefilled)
            {
                // Solver em andamento / prefill → caminho de progresso da sugestão.
                state.Type      = MacroTaskType.Suggested;
                state.Exception = suggTask?.Exception;
                state.Started   = suggTask != null || isPrefilled;
                state.Completed = suggDone || isPrefilled;
                state.Actions   = suggRes?.Solution.Actions ?? (isPrefilled ? hashMatch!.Actions : null);
                state.State     = suggRes?.Solution.State   ?? (isPrefilled ? hashMatchState     : null);
                state.Solver    = BestMacroSolver;
                state.IsPrefilled          = isPrefilled;
                state.IsRegenerating       = isRegenerating;
                state.RegeneratingSnapshot = isRegenerating
                    ? (_prevSuggestedActions!, _prevSuggestedState!.Value)
                    : null;
                state.TitleSuffix = BuildBestMacroBadge(BestMacroSource.Suggested, isPrefilled);
            }
            else if (showSuggested)
            {
                state.Type        = MacroTaskType.Suggested;
                state.Exception   = suggTask?.Exception;
                state.Started     = true;
                state.Completed   = true;
                state.Actions     = suggRes?.Solution.Actions;
                state.State       = suggRes?.Solution.State;
                state.Solver      = BestMacroSolver;
                state.TitleSuffix = BuildBestMacroBadge(BestMacroSource.Suggested, false);
            }
            else
            {
                // Mostra a salva (venceu, ou toggle, ou fallback quando a sugestão falhou/ausente, ou nada).
                state.Type      = MacroTaskType.Saved;
                state.Exception = SavedMacroTask?.Exception;
                state.Started   = SavedMacroTask != null;
                state.Completed = SavedMacroTask?.Completed ?? false;
                state.Actions   = savedRes?.Best?.Actions;
                state.MacroName = savedRes?.Best?.Name;
                state.State     = savedRes?.BestState;
                state.HasHashMismatch = savedRes?.Best is { CharacterStatsHash: not null } bm
                                     && bm.CharacterStatsHash != _currentCharacterHash;
                if (savedRes?.Best is { } savedMacro)
                {
                    state.TitleSuffix       = BuildBestMacroBadge(BestMacroSource.Saved, false);
                    state.MacroEditorSetter = a => { savedMacro.ActionEnumerable = a; _plugin.MacroRepository.Update(savedMacro); };
                }
            }

            // Rodapé: comparação (quando as duas fontes existem, com sugestão não-vazia) ou nota de fallback.
            var suggEmpty = suggDone && suggRes is { } sres0 && (sres0.Solution.Actions?.Count ?? 0) == 0;

            // "%" apenas quando é significativo: receita com quality E macro completa o craft.
            // Caso contrário marca "✗" (não completa) ou "—" (receita sem quality).
            string HqOrMark(SimulationState? st)
            {
                if (st is not { } s || RecipeData!.RecipeInfo.MaxQuality <= 0)
                    return "—";
                var completes = s.Progress >= RecipeData.RecipeInfo.MaxProgress;
                return completes ? $"{s.HQPercent}%" : "✗";
            }

            if (suggException)
            {
                if (savedRes?.Best != null)
                {
                    var ex = suggTask!.Exception;
                    state.Footer = () =>
                    {
                        ImGui.Spacing();
                        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                            ImGui.TextUnformatted("Solver failed to generate a suggestion.");
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Copy error"))
                            ImGui.SetClipboardText(ex?.ToString() ?? "");
                    };
                }
                // sem salva → o card de exceção já mostra o erro; sem footer
            }
            else if (suggEmpty && savedRes?.Best != null && !showSuggested)
            {
                state.Footer = () =>
                {
                    ImGui.Spacing();
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        ImGui.TextUnformatted("Solver couldn't improve on your saved macro.");
                };
            }
            else if (bothExist && !suggEmpty && savedRes is { } sr && suggRes is { } su)
            {
                var savedMark = HqOrMark(sr.BestState);
                var suggMark  = HqOrMark(su.Solution.State);
                var altName   = showSuggested ? "saved" : "suggested";
                state.Footer = () =>
                {
                    ImGui.Spacing();
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        ImGui.TextUnformatted($"✦ {suggMark}  vs  ★ saved {savedMark}");
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"View {altName}"))
                        _bestShowAlternative = !_bestShowAlternative;
                };
            }

            if (suggDone)
            {
                _prevSuggestedActions = null;
                _prevSuggestedState   = null;
            }

            state.TitleOverride = "Best Macro";
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
                        _bestMacroSolver.Invalidate(task.Cancel);
                }
            }
            else
            {
                var hasResult = task?.Result != null;
                var label = hasResult ? "Regenerate" : "Suggest Macro";
                if (ImGui.Button(label, new(availWidth, 0)))
                    CalculateSuggestedMacro();
                ImGuiUtils.HoveredTooltip(hasResult
                    ? "Generate a new macro suggestion from scratch, discarding the current one."
                    : "Suggest a way to finish the crafting recipe. " +
                      "Results aren't perfect, and levels of success " +
                      "can vary wildly depending on the solver's settings.", wrapWidth: 300);
            }
            Theme.PopPrimaryButton();
        }

        ImGuiHelpers.ScaledDummy(3);

        if (ImGui.Button("Open in Macro Editor", new(availWidth, 0)))
            _plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), CalculateIngredientHqCounts(), [], null);

        if (ImGui.Button("View Saved Macros", new(availWidth, 0)))
            _plugin.OpenMacroListWindow();
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
        _bestMacroSolver.Invalidate(() => SuggestedMacroTask?.Dispose());
        CommunityMacroTask?.Dispose();
        _plugin.WindowSystem.RemoveWindow(this);
        AxisFont?.Dispose();
        ExpertBadge.Dispose();
        CollectibleBadge.Dispose();
    }
}
