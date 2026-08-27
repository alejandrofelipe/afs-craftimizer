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

public sealed unsafe partial class CraftingHelper
{
    private void ClearSuggestedMacro()
    {
        _bestMacroSolver.Invalidate(() => SuggestedMacroTask?.Cancel());
        SuggestedMacroTask = null;
        _prevSuggestedActions = null;
        _prevSuggestedState   = null;
        _bestShowAlternative  = false;
    }

    private IEnumerable<int> CalculateIngredientHqCounts()
    {
        if (RecipeData == null)
            throw new InvalidOperationException("RecipeData must not be null");

        var ingredientCount = RecipeData.Ingredients.Count;
        var ingredientSpan = MemoryMarshal.Cast<CSRecipeNote.RecipeIngredient, RecipeIngredient2>(CSRecipeNote.Instance()->RecipeList->SelectedRecipe->Ingredients);
        return ingredientSpan.ToArray().Take(ingredientCount).Select(i => (int)i.HQCount);
    }

    private enum MacroTaskType
    {
        Saved,
        Suggested,
        Community
    }
    // T de BackgroundTask<T> precisa ser struct; record struct atende e carrega o score.
    private readonly record struct SavedMacroResult(
        Macro? Best, SimulationState? BestState, float BestScore,
        Macro? HashMatch, SimulationState? HashMatchState);

    private readonly record struct SuggestedMacroResult(
        SolverSolution Solution, float Score);

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
        public Action? TitleSuffix;   // badge de fonte (★ Saved / ✦ Suggested) + bookmark de prefill
        public Action? Footer;        // rodapé de comparação/nota, desenhado dentro do painel
        public string? TitleOverride; // substitui o título padrão (ex.: "Best Macro" no card unificado)
    }

    private static Action BuildBestMacroBadge(BestMacroSource source, bool prefilled)
    {
        var (icon, color, label) = source == BestMacroSource.Suggested
            ? ("✦", Colors.Progress, "Suggested")
            : ("★", Colors.Quality,  "Saved");
        return () =>
        {
            ImGui.SameLine(0, 6);
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
                ImGui.TextUnformatted($"{icon} {label}");
            if (prefilled)
            {
                ImGui.SameLine(0, 4);
                using (ImRaii.PushFont(UiBuilder.IconFont))
                using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled)))
                    ImGui.TextUnformatted(FontAwesomeIcon.Bookmark.ToIconString());
                ImGuiUtils.HoveredTooltip("Pre-filled from saved macro — solver still comparing");
            }
        };
    }

    private void DrawMacro(in MacroTaskState state, float panelWidth)
    {
        var panelTitle = state.TitleOverride ?? (state.Type switch
        {
            MacroTaskType.Saved => "Best Saved Macro",
            MacroTaskType.Suggested => "Suggested Macro",
            MacroTaskType.Community => "Best Community Macro",
            _ => throw new ArgumentOutOfRangeException(nameof(state), "state.Type must have a valid type")
        });

        Action? titleSuffix = state.TitleSuffix ?? (state.IsPrefilled ? () =>
        {
            ImGui.SameLine(0, 4);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled)))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(FontAwesomeIcon.Bookmark.ToIconString());
            }
            ImGuiUtils.HoveredTooltip("Pre-filled from saved macro — solver still comparing");
        } : null);

        using var panel = ImRaii2.GroupPanel(panelTitle, panelWidth, out var contentW, titleSuffix: titleSuffix);
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
                        ImGuiUtils.HoveredTooltip("Searches FFXIV Teamcraft to find you the best macro", wrapWidth: 300);
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
            var innerW     = contentW;
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

                    // Row 2: HQ% | "Regenerating..."
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
                            ImGui.TextUnformatted("Regenerating...");
                        }
                    }
                }
            }

            // Overlay: novo componente de progresso (chip + dots + algo + barra) centrado sobre o card
            var afterCardPos = ImGui.GetCursorPos();
            if (state.Solver is { } regenSolver)
            {
                var cardH    = windowHeight + spacing.Y + botRowH;
                var snapshot = SolverProgressBar.FromSolver(regenSolver, "Solver");
                var overlayH = ImGui.GetFrameHeightWithSpacing() * 2f; // 2 linhas: chip+dots / barra
                var overlayY = cardTopPos.Y + (cardH - overlayH) / 2f;

                ImGui.SetCursorPos(new Vector2(cardTopPos.X, overlayY));
                var screenMin = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(
                    screenMin,
                    screenMin + new Vector2(contentW, overlayH),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.14f, 0.85f)),
                    3f);

                PluginImGuiUtils.DrawSolverProgressArea(
                    contentW, [snapshot], _plugin.Configuration.ProgressType);
                ImGui.SetCursorPos(afterCardPos);
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
                            break; // solver not yet assigned by background thread — skip this frame

                        var snapshot = SolverProgressBar.FromSolver(solver, "Solver");
                        PluginImGuiUtils.DrawSolverProgressArea(
                            contentW, [snapshot], _plugin.Configuration.ProgressType);
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
                var innerW    = contentW;
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
                                    ImGuiUtils.HoveredTooltip(actions[i].GetName(RecipeData!.ClassJob));
                                }
                                else
                                {
                                    var amtMore = itemCount - itemsPerRow * 2;
                                    var aPos = ImGui.GetCursorPos();
                                    ImGui.Image(actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowH), default, Vector2.One, new(1, 1, 1, .5f));
                                    ImGuiUtils.HoveredTooltip($"{actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
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
                            ImGuiUtils.HoveredTooltip("This macro was saved with different character stats and may not perform as expected", wrapWidth: 300);
                            ImGui.SameLine();
                        }

                        var cellStart  = ImGui.GetCursorPos();
                        var cellAvailW = ImGui.GetContentRegionAvail().X;
                        var iconH      = botRowH;

                        var editX = cellStart.X + cellAvailW - iconH * 2 - spacing.X;
                        ImGui.SetCursorPos(new Vector2(editX, cellStart.Y));
                        if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.Edit, "Open in Macro Editor", iconH))
                            _plugin.OpenMacroEditor(CharacterStats!, RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), CalculateIngredientHqCounts(), actions, state.MacroEditorSetter);
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
                        // Sugestão do solver não tem nome salvo → mostra o nome do item craftado
                        // (o badge "✦ Suggested" no topo já marca a fonte). "AI Suggestion" fica só de fallback.
                        var suggestedItemName = RecipeData!.Recipe.ItemResult.Value!.Name.ToString();
                        var displayName = state.MacroName ?? (state.Type == MacroTaskType.Suggested
                            ? (string.IsNullOrWhiteSpace(suggestedItemName) ? "AI Suggestion" : suggestedItemName)
                            : "");
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

        if (state.Footer is { } drawFooter)
            drawFooter();
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
            config = config.ForDelineations(_plugin.Configuration.CheckDelineations, hasDelineations);
            var mctsConfig = new MCTSConfig(config, RecipeData!.RecipeInfo);
            var simulator = new SimulatorNoRandom();
            List<Macro> macros = [.. _plugin.MacroRepository.SnapshotMacros()
                .Where(m => m.RecipeId == RecipeData!.RecipeId)];

            token.ThrowIfCancellationRequested();

            if (macros.Count == 0)
                return default;   // SavedMacroResult vazio (Best == null)

            var results = macros
                .Select(macro =>
                {
                    var (score, outState) = CommunityMacros.CommunityMacro.CalculateScore(macro.Actions, simulator, in state, in mctsConfig);
                    return (macro, outState, score);
                })
                .ToList();

            token.ThrowIfCancellationRequested();

            var completing = results.Where(r => r.score > 0f).ToList();
            if (completing.Count == 0)
                return default; // nenhuma macro da receita completa → sem "Best"
            var bestSaved = completing.MaxBy(m => m.score);

            var hashMatchResults = currentHash.HasValue
                ? results.Where(r => r.macro.CharacterStatsHash == currentHash && r.score > 0).ToList()
                : new();
            (Macro macro, SimulationState? outState, float score)? bestHashMatch = hashMatchResults.Count > 0
                ? hashMatchResults.MaxBy(r => r.score)
                : null;

            return new SavedMacroResult(
                bestSaved.macro, bestSaved.outState, bestSaved.score,
                bestHashMatch?.macro, bestHashMatch?.outState);
        });
        SavedMacroTask.Start();
    }

    private void CalculateSuggestedMacro()
    {
        if (SuggestedMacroTask?.Result is { } prev)
        {
            _prevSuggestedActions = prev.Solution.Actions;
            _prevSuggestedState   = prev.Solution.State;
        }
        else
        {
            _prevSuggestedActions = null;
            _prevSuggestedState   = null;
        }
        var generation = _bestMacroSolver.Begin();
        _bestShowAlternative = false;
        SuggestedMacroTask?.Cancel();
        var hasDelineations = Gearsets.HasDelineations();
        SuggestedMacroTask = new(token =>
        {
            var input = new SimulationInput(CharacterStats!, RecipeData!.RecipeInfo, StartingQuality);
            var state = new SimulationState(input);
            var config = _plugin.Configuration.RecipeNoteSolverConfig;
            config = config.ForDelineations(_plugin.Configuration.CheckDelineations, hasDelineations);

            token.ThrowIfCancellationRequested();

            var solver = new Solver.Solver(config, state) { Token = token };
            solver.OnLog += Log.Debug;
            solver.OnWarn += t => Plugin.Plugin.DisplaySolverWarning(t);
            token.ThrowIfCancellationRequested();
            if (!_bestMacroSolver.TryPublish(generation, solver))
                throw new OperationCanceledException(token);

            solver.Start();
            var solution = solver.GetTask().GetAwaiter().GetResult();

            token.ThrowIfCancellationRequested();

            var mctsConfig = new MCTSConfig(config, RecipeData!.RecipeInfo);
            var simulator  = new SimulatorNoRandom();
            var (score, _) = CommunityMacros.CommunityMacro.CalculateScore(
                solution.Actions, simulator, in state, in mctsConfig);
            return new SuggestedMacroResult(solution, score);
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
            config = config.ForDelineations(_plugin.Configuration.CheckDelineations, hasDelineations);
            var mctsConfig = new MCTSConfig(config, RecipeData!.RecipeInfo);
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
}
