using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Craftimizer.Solver;

namespace Craftimizer.Windows;

public sealed partial class MacroEditor
{
    private void DrawMacroInfo()
    {
        {
            var totalW    = ImGui.GetContentRegionAvail().X;
            var spacing   = ImGui.GetStyle().ItemSpacing.X;
            var condition = State.Condition;

            var pos = ImGui.GetCursorPos();
            using (var g = ImRaii.Group())
            {
                var availSize = totalW - spacing - ImGui.GetFrameHeight();
                var size      = ImGui.GetFrameHeight() + spacing + ImGui.CalcTextSize(condition.Name()).X;
                ImGuiUtils.AlignCentered(size, availSize);
                ImGuiUtils.DrawConditionIndicator(condition, spacing);
            }
            if (ImGui.IsItemHovered())
                ImGuiUtils.Tooltip(condition.Description(CharacterStats.HasSplendorousBuff));

            ImGui.SetCursorPos(pos);
            ImGuiUtils.AlignRight(ImGui.GetFrameHeight(), totalW);
            using (var disabled = ImRaii.Disabled(SolverRunning))
            {
                using var tint = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !_plugin.Configuration.ConditionRandomness);
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Dice))
                {
                    _plugin.Configuration.ConditionRandomness ^= true;
                    _plugin.Configuration.Save();
                    RecalculateState();
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGuiUtils.TooltipWrapped($"Condition Randomness{(!_plugin.Configuration.ConditionRandomness ? " (Disabled)" : string.Empty)}\n" +
                    "Allows the condition to fluctuate randomly like a real craft. " +
                    "Turns off when generating a macro.");
        }

        {
            var datas = new List<DynamicBars.BarData>
            {
                new("Durability", Colors.Durability, State.Durability,     RecipeData.RecipeInfo.MaxDurability),
                new("Progress",   Colors.Progress,   Reliability.Progress, State.Progress, RecipeData.RecipeInfo.MaxProgress),
                new("Quality",    Colors.Quality,    Reliability.Quality,  State.Quality,  RecipeData.RecipeInfo.MaxQuality),
                new("CP",         Colors.CP,         State.CP,             CharacterStats.CP),
            };
            if (RecipeData.RecipeInfo.MaxQuality <= 0)
                datas.RemoveAt(2);
            if (RecipeData.IsCollectable)
                datas.Add(new("Collect.", Colors.Collectability, Reliability.ParamScore, State.Collectability, State.MaxCollectability, RecipeData.CollectableThresholds, $"{State.Collectability}"));
            else if (RecipeData.Recipe.RequiredQuality > 0)
            {
                var qualityPercent = (float)State.Quality / RecipeData.Recipe.RequiredQuality * 100;
                datas.Add(new("Quality %", Colors.HQ, Reliability.ParamScore, qualityPercent, 100, null, $"{qualityPercent:0}%"));
            }
            else if (RecipeData.RecipeInfo.MaxQuality > 0)
                datas.Add(new("HQ %", Colors.HQ, Reliability.ParamScore, State.HQPercent, 100, null, $"{State.HQPercent}%"));
            DynamicBars.DrawRow(datas);
        }

        using (var barsTable = ImRaii.Table("buffBars", 2, ImGuiTableFlags.SizingStretchSame))
        {
            if (barsTable)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch, 3);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch, 1);

                var buffIconHeight = ImGui.GetFrameHeight() * 1.75f;
                var buffDurationShift = buffIconHeight * .2f;
                var buffHeight = buffIconHeight + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetTextLineHeight() - buffDurationShift;

                var infoHeight = 3 * ImGui.GetTextLineHeightWithSpacing();

                var panelHeight = Math.Max(buffHeight, infoHeight);

                ImGui.TableNextColumn();
                using (ImRaii2.GroupPanel("Buffs", -1, out _))
                {
                    using var _font = AxisFont.Push();

                    ImGui.Dummy(new(0, panelHeight));
                    ImGui.SameLine(0, 0);
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (panelHeight - buffHeight) / 2f);

                    var effects = State.ActiveEffects;
                    foreach (var effect in Enum.GetValues<EffectType>())
                    {
                        if (!effects.HasEffect(effect))
                            continue;

                        using (var group = ImRaii.Group())
                        {
                            var icon = effect.GetIcon(effects.GetStrength(effect));
                            var size = new Vector2(buffIconHeight * (icon.AspectRatio ?? 1), buffIconHeight);

                            ImGui.Image(icon.Handle, size);
                            if (!effect.IsIndefinite())
                            {
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - buffDurationShift);
                                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 1);
                                ImGuiUtils.TextCentered($"{effects.GetDuration(effect)}", size.X);
                            }
                        }
                        if (ImGui.IsItemHovered())
                        {
                            var status = effect.Status();
                            using var _reset = ImRaii.DefaultFont();
                            ImGuiUtils.Tooltip($"{status.Name}\n{status.Description}");
                        }
                        ImGui.SameLine();
                    }
                }

                ImGui.TableNextColumn();
                using (ImRaii2.GroupPanel("Info", -1, out _))
                {
                    var actions = Macro.Actions.ToArray();
                    var waitTime = actions.Sum(a => a.Base().MacroWaitTime);
                    var waitTimeOptimal = waitTime - actions.Length;
                    var delineationCount = actions.Count(SolverConfig.SpecialistActions.Contains);

                    var height = (delineationCount == 0 ? 2 : 3) * ImGui.GetTextLineHeightWithSpacing();

                    ImGui.Dummy(new(0, panelHeight));
                    ImGui.SameLine(0, 0);

                    using (ImRaii.Group())
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPos().Y + (panelHeight - height) / 2f);
                        ImGuiUtils.TextCentered($"{actions.Length} Step{(actions.Length != 1 ? "s" : string.Empty)}");
                        ImGuiUtils.TextCentered($"{waitTime} sec");
                        if (ImGui.IsItemHovered())
                            ImGuiUtils.Tooltip($"Optimal Time: {waitTimeOptimal:0.#} sec");
                        if (delineationCount != 0)
                            ImGuiUtils.TextCentered($"{delineationCount} Delineation{(delineationCount != 1 ? "s" : string.Empty)}");
                    }
                }
            }
        }
    }

    private void DrawMacro()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var imageSize = ImGui.GetFrameHeight() * 2;
        var lastState = Macro.InitialState;

        using var panel = ImRaii2.GroupPanel("Macro", -1, out var availSpace);
        ImGui.Dummy(new(0, imageSize));
        ImGui.SameLine(0, 0);

        var macroActionsHeight = ImGui.GetFrameHeightWithSpacing() * (1 + (SolverRunning ? 1 : 0));
        var childHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y * 2 - ImGui.GetStyle().CellPadding.Y - macroActionsHeight - ImGui.GetStyle().ItemSpacing.Y * 2;

        using (var child = ImRaii.Child("##macroActions", new(availSpace, childHeight)))
        {
            if (Macro.Count == 0 && !SolverRunning)
            {
                var totalH = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetTextLineHeight();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, (childHeight - totalH) / 2f));
                ImGuiUtils.TextCentered("Drop actions here to build your macro", availSpace);
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGuiUtils.TextCentered("Or use Solve & Replace to auto-generate", availSpace);
            }

            var itemsPerRow = (int)Math.Max(1, MathF.Floor((ImGui.GetContentRegionAvail().X + spacing) / (imageSize + spacing)));
            using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
            using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
            using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
            using var _alpha = ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, ImGui.GetStyle().DisabledAlpha * .5f);
            for (var i = 0; i < Macro.Count; i++)
            {
                if (i % itemsPerRow != 0)
                    ImGui.SameLine(0, spacing);
                var (action, response, state) = (Macro[i].Action, Macro[i].Response, Macro[i].State);
                var actionBase = action.Base();
                var failedAction = response != ActionResponse.UsedAction;
                using var id = ImRaii.PushId(i);
                if (ImGui.ImageButton(action.GetIcon(RecipeData!.ClassJob).Handle, new(imageSize), default, Vector2.One, 0, default, failedAction ? new(1, 1, 1, ImGui.GetStyle().DisabledAlpha) : Vector4.One) && !SolverRunning)
                    RemoveStep(i);
                if (response is ActionResponse.ActionNotUnlocked ||
                    (
                        failedAction &&
                        (CharacterStats.Level < actionBase.Level ||
                            (action == ActionType.Manipulation && !CharacterStats.CanUseManipulation) ||
                            (action is ActionType.HeartAndSoul or ActionType.CarefulObservation && !CharacterStats.IsSpecialist)
                        )
                    )
                )
                {
                    Vector2 v1 = ImGui.GetItemRectMin(), v2 = ImGui.GetItemRectMax();
                    ImGui.PushClipRect(v1, v2, true);
                    (v1.X, v2.X) = (v2.X, v1.X);
                    ImGui.GetWindowDrawList().AddLine(v1, v2, ImGui.GetColorU32(Colors.Bad with { W = ImGui.GetStyle().DisabledAlpha / 2 }), 5 * ImGuiHelpers.GlobalScale);
                    ImGui.PopClipRect();
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGuiUtils.Tooltip($"{action.GetName(RecipeData!.ClassJob)}\n{actionBase.GetTooltip(CreateSim(lastState), true)}");

                if (!SolverRunning)
                {
                    using var _padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                    using (var _source = ImRaii.DragDropSource())
                    {
                        if (_source)
                        {
                            ImGuiExtras.SetDragDropPayload("macroAction", i);
                            ImGui.ImageButton(action.GetIcon(RecipeData!.ClassJob).Handle, new(imageSize));
                        }
                    }
                    using (var _target = ImRaii.DragDropTarget())
                    {
                        if (_target)
                        {
                            if (ImGuiExtras.AcceptDragDropPayload("macroAction", out int j))
                                Macro.Move(j, i);
                            else if (ImGuiExtras.AcceptDragDropPayload("macroActionInsert", out ActionType newAction))
                                Macro.Insert(i, newAction);
                        }
                    }
                }
                lastState = state;
            }
        }

        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(default);
        ImGui.GetWindowDrawList().AddLine(pos, pos + new Vector2(availSpace, 0), ImGui.GetColorU32(ImGuiCol.Border));
        ImGui.Dummy(default);
        ProgressBarComponent.ProgressSnapshot[] solverSnapshots;
        lock (_solverSnapshots)
            solverSnapshots = [.. _solverSnapshots];

        if (solverSnapshots.Length > 0)
        {
            var chipState = solverSnapshots[0].State switch
            {
                ProgressBarComponent.ProgressState.Completed => ImGuiUtils.SolverState.Complete,
                ProgressBarComponent.ProgressState.Cancelled => ImGuiUtils.SolverState.Failed,
                _ => ImGuiUtils.SolverState.Solving
            };
            ImGuiUtils.DrawStateChip(chipState);
            var config = new ProgressBarComponent.VisualConfig(
                Mode: ProgressBarComponent.DisplayMode.Horizontal,
                ColorTheme: _plugin.Configuration.ProgressType,
                Width: availSpace,
                ShowPercentage: true,
                ShowDetailedTooltip: true,
                ShowSummaryWhenAggregated: true
            );
            ProgressBarComponent.DrawAggregated(solverSnapshots, config);
        }
        DrawMacroActions(availSpace);
    }
}
