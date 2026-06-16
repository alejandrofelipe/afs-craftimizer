using Artificer.Plugin;
using Artificer.Utils;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sim = Artificer.Simulator.SimulatorNoRandom;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace Artificer.Windows;

public sealed class MacroLibrary : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    public CharacterStats? CharacterStats { get; private set; }
    public RecipeData? RecipeData { get; private set; }

    private readonly global::Artificer.Plugin.Plugin _plugin;
    private IReadOnlyList<Macro> Macros => _plugin.MacroRepository.Macros;
    private Dictionary<Macro, SimulationState> MacroStateCache { get; } = [];
    private CosmicToolTracker.ToolProgress? _cosmicProgress;
    private readonly TitleBarButton _cosmicButton;

    public MacroLibrary(global::Artificer.Plugin.Plugin plugin) : base("Macro Library", WindowFlags, false)
    {
        _plugin = plugin;
        RefreshSearch();

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

        _plugin.MacroRepository.MacroUpdated += OnMacroChanged;
        _plugin.MacroRepository.MacroListChanged += OnMacroListChanged;

        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;

        SizeConstraints = new() { MinimumSize = new(UIConstants.MacroListMinWidth, UIConstants.MacroListMinHeight), MaximumSize = new(float.PositiveInfinity) };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Plus,
                IconOffset = new(2, 1),
                Click = _ => OpenEditor(null),
                ShowTooltip = () => ImGuiUtils.Tooltip("New Macro")
            },
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("General"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.Plugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("Support the original author on Ko-fi")
            }
        ];

        if (_cosmicProgress != null)
            TitleBarButtons.Insert(0, _cosmicButton);

        _plugin.WindowSystem.AddWindow(this);
    }

    public override bool DrawConditions()
    {
        return Service.Objects.LocalPlayer != null;
    }

    public override void PreDraw()
    {
        var oldCharacterStats = CharacterStats;
        var oldRecipeData = RecipeData;

        (CharacterStats, RecipeData, _) = _plugin.GetDefaultStats();

        if (oldCharacterStats != CharacterStats || oldRecipeData != RecipeData)
            RecalculateStats();
        Theme.Push();
    }

    public override void PostDraw()
    {
        Theme.Pop();
        base.PostDraw();
    }

    public override void Draw()
    {
        DrawSearchBar();
        DrawPagination();
        using var child = ImRaii.Child("macros", new(-1, -1));
        if (sortedMacros.Count > 0)
        {
            var width = ImGui.GetContentRegionAvail().X;
            var startIdx = currentPage * MacrosPerPage;
            var endIdx = Math.Min(startIdx + MacrosPerPage, sortedMacros.Count);
            var macros = sortedMacros.GetRange(startIdx, endIdx - startIdx);
            for (var i = 0; i < macros.Count; ++i)
            {
                var pos = ImGui.GetCursorPos();
                DrawMacro(macros[i]);
                ImGui.SetCursorPos(pos);
                ImGui.InvisibleButton($"###macroButton{i}", ImGui.GetItemRectSize());
                if (isUnsorted)
                {
                    using (var _source = ImRaii.DragDropSource(ImGuiDragDropFlags.SourceNoDisableHover))
                    {
                        if (_source)
                        {
                            ImGuiExtras.SetDragDropPayload("macroListItem", i);
                            DrawMacro(macros[i], width);
                        }
                    }
                    using (var _target = ImRaii.DragDropTarget())
                    {
                        if (_target)
                        {
                            if (ImGuiExtras.AcceptDragDropPayload("macroListItem", out int j))
                                _plugin.MacroRepository.Move(startIdx + j, startIdx + i);
                        }
                    }
                }
            }
        }
        else
        {
            DrawEmptyState();
        }
    }

    private void DrawEmptyState()
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            ImGuiUtils.DrawEmptyState(
                (int)FontAwesomeIcon.Search,
                $"No macros match \"{searchText}\"",
                "Try a different search term.",
                primaryButton: ("Clear Search", () => { searchText = string.Empty; RefreshSearch(); }),
                buttonWidth: 160f);
        }
        else
        {
            ImGuiUtils.DrawEmptyState(
                (int)FontAwesomeIcon.Clipboard,
                "No macros yet",
                "Create your first macro from the Macro Editor or Crafting Log.",
                primaryButton: ("Open Macro Editor", () => OpenEditor(null)),
                secondaryButton: ("Open Crafting Log", Plugin.Plugin.OpenCraftingLog));
        }
    }

    private const int MacrosPerPage = 20; // UIConstants.MacrosPerPage
    private string searchText = string.Empty;
    private List<Macro> sortedMacros = null!;
    private bool isUnsorted = true;
    private int currentPage;
    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint("##search", "Search", ref searchText, 100))
            RefreshSearch();
    }

    private void DrawPagination()
    {
        if (sortedMacros.Count <= MacrosPerPage)
            return;

        var totalPages = (int)Math.Ceiling(sortedMacros.Count / (float)MacrosPerPage);
        var availWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = 80f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        using var disabled = ImRaii.Disabled(currentPage == 0);
        if (ImGui.Button("<< Previous", new(buttonWidth, 0)))
            currentPage = Math.Max(0, currentPage - 1);
        disabled.Dispose();

        ImGui.SameLine();
        var pageText = $"Page {currentPage + 1} / {totalPages} · {sortedMacros.Count} macros";
        var textWidth = ImGui.CalcTextSize(pageText).X;
        ImGui.SetCursorPosX((availWidth - textWidth) / 2f);
        ImGui.TextUnformatted(pageText);

        ImGui.SameLine();
        ImGui.SetCursorPosX(availWidth - buttonWidth);
        using var disabled2 = ImRaii.Disabled(currentPage >= totalPages - 1);
        if (ImGui.Button("Next >>", new(buttonWidth, 0)))
            currentPage = Math.Min(totalPages - 1, currentPage + 1);
    }

    private void DrawMacro(Macro macro, float width = -1)
    {
        width = width < 0 ? ImGui.GetContentRegionAvail().X : width;

        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (macro.Actions.Any(a => a.Category() == ActionCategory.Combo))
            throw new InvalidOperationException("Combo actions should be sanitized away");

        var stateNullable = GetMacroState(macro);

        using var panel = ImRaii2.GroupPanel(macro.Name, width - ImGui.GetStyle().ItemSpacing.X * 2, out var availWidth, accentLabel: false);
        var stepsAvailWidthOffset = width - availWidth;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var miniRowHeight = (windowHeight - spacing) / 2f;

        using (var table = ImRaii.Table("table", stateNullable.HasValue ? 3 : 2, ImGuiTableFlags.None))
        {
            if (table)
            {
                if (stateNullable.HasValue)
                    ImGui.TableSetupColumn("stats", ImGuiTableColumnFlags.WidthFixed, 0);
                ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, 0);
                ImGui.TableSetupColumn("steps", ImGuiTableColumnFlags.WidthStretch, 0);

                ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
                if (stateNullable is { } state)
                {
                    ImGui.TableNextColumn();
                    PluginImGuiUtils.DrawMacroStatArcs(state, windowHeight);
                }

                ImGui.TableNextColumn();
                {
                    if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.Edit, "Open in Macro Editor", miniRowHeight))
                        OpenEditor(macro);
                    ImGui.SameLine(0, spacing);
                    if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.PencilAlt, miniRowHeight))
                        ShowRenamePopup(macro);
                    DrawRenamePopup(macro);
                    ImGuiUtils.HoveredTooltip("Rename");

                    if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.Paste, "Copy to Clipboard", miniRowHeight))
                        MacroCopy.Copy(macro.Actions, _plugin);
                    ImGui.SameLine(0, spacing);
                    Theme.PushDangerButton();
                    using (var _disabled = ImRaii.Disabled(!ImGui.GetIO().KeyShift))
                    {
                        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Trash, miniRowHeight))
                            _plugin.MacroRepository.Remove(macro);
                    }
                    Theme.PopDangerButton();
                    ImGuiUtils.HoveredTooltip("Delete (Hold Shift)", (int)ImGuiHoveredFlags.AllowWhenDisabled);
                }

                ImGui.TableNextColumn();
                {
                    var itemsPerRow = (int)MathF.Floor((ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset + spacing * 2) / (miniRowHeight + spacing));
                    var itemCount = macro.Actions.Count;
                    for (var i = 0; i < itemsPerRow * 2; i++)
                    {
                        if (i % itemsPerRow != 0)
                            ImGui.SameLine(0, spacing);
                        if (i < itemCount)
                        {
                            var shouldShowMore = i + 1 == itemsPerRow * 2 && i + 1 < itemCount;
                            if (!shouldShowMore)
                            {
                                ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowHeight));
                                ImGuiUtils.HoveredTooltip(macro.Actions[i].GetName(RecipeData!.ClassJob));
                            }
                            else
                            {
                                var amtMore = itemCount - itemsPerRow * 2;
                                var pos = ImGui.GetCursorPos();
                                ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowHeight), default, Vector2.One, new(1, 1, 1, .5f));
                                ImGuiUtils.HoveredTooltip($"{macro.Actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
                                ImGui.SetCursorPos(pos);
                                ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), ImGui.GetColorU32(ImGuiCol.FrameBg), miniRowHeight / 8f);
                                ImGui.GetWindowDrawList().AddTextClippedEx(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), $"+{amtMore}", null, new(.5f), null);
                            }
                        }
                        else
                            ImGui.Dummy(new(miniRowHeight));
                    }
                }
            }
        }

        // Footer badges
        if (stateNullable is { } footerState && RecipeData != null)
        {
            var maxProgress = RecipeData.RecipeInfo.MaxProgress;
            var maxQuality = RecipeData.RecipeInfo.MaxQuality;
            if (maxProgress > 0)
            {
                var pct = (int)MathF.Round(footerState.Progress * 100f / maxProgress);
                ImGuiUtils.DrawBadgePill($"{pct}% Progress", Colors.Progress);
                ImGui.SameLine(0, spacing);
            }
            if (maxQuality > 0)
            {
                ImGuiUtils.DrawBadgePill($"{footerState.HQPercent}% HQ", Colors.HQ);
                ImGui.SameLine(0, spacing);
            }
        }
        ImGuiUtils.DrawBadgePill($"{macro.Actions.Count} steps", Colors.TextMuted);
    }

    private string popupMacroName = string.Empty;
    private Macro? popupMacro;
    private void ShowRenamePopup(Macro macro)
    {
        ImGui.OpenPopup($"##renamePopup-{macro.GetHashCode()}");
        popupMacro = macro;
        popupMacroName = macro.Name;
        ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(ImGui.CalcItemWidth() * .25f, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
    }

    private void DrawRenamePopup(Macro macro)
    {
        using var popup = ImRaii.Popup($"##renamePopup-{macro.GetHashCode()}");
        if (popup)
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(ImGui.CalcItemWidth());
            if (ImGui.InputTextWithHint($"##setName", "Name", ref popupMacroName, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrWhiteSpace(popupMacroName))
                {
                    popupMacro!.Name = popupMacroName;
                    _plugin.MacroRepository.Update(popupMacro!);
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    private void RecalculateStats()
    {
        MacroStateCache.Clear();
    }

    private void RefreshSearch()
    {
        currentPage = 0;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            sortedMacros = [.. Macros];
            isUnsorted = true;
            return;
        }
        isUnsorted = false;
        var matcher = new FuzzyMatcher(searchText.ToLowerInvariant(), MatchMode.FuzzyParts);
        var query = Macros.AsParallel().Select(i => (Item: i, Score: matcher.Matches(i.Name.ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Item);
        sortedMacros = [.. query];
    }

    private void OpenEditor(Macro? macro)
    {
        var stats = _plugin.GetDefaultStats();
        _plugin.OpenMacroEditor(stats.Character, stats.Recipe, stats.Buffs, null, macro?.Actions ?? Enumerable.Empty<ActionType>(), macro != null ? (actions => { macro.ActionEnumerable = actions; _plugin.MacroRepository.Update(macro); }) : null);
    }

    private void OnMacroChanged(Macro macro)
    {
        MacroStateCache.Remove(macro);
    }

    private void OnMacroListChanged()
    {
        RefreshSearch();
    }

    private SimulationState? GetMacroState(Macro macro)
    {
        if (CharacterStats == null || RecipeData == null)
            return null;

        if (MacroStateCache.TryGetValue(macro, out var state))
            return state;

        state = new SimulationState(new(CharacterStats, RecipeData.RecipeInfo));
        var sim = new Sim();
        (_, state, _) = sim.ExecuteMultiple(state, macro.Actions);
        return MacroStateCache[macro] = state;
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
        _plugin.MacroRepository.MacroUpdated -= OnMacroChanged;
        _plugin.MacroRepository.MacroListChanged -= OnMacroListChanged;

        _plugin.WindowSystem.RemoveWindow(this);
    }
}
