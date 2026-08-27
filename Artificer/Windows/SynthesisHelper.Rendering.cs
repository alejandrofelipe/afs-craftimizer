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

public sealed unsafe partial class SynthesisHelper
{
    private void DrawStatusStrip(SimulationState state)
    {
        var condition = state.Condition;
        var spacing   = ImGui.GetStyle().ItemSpacing.X;

        PluginImGuiUtils.DrawConditionIndicator(condition, spacing);
        ImGuiUtils.HoveredTooltip(condition.Description(Session.CharacterStats!.HasSplendorousBuff));

        var stepCount  = state.StepCount + 1;
        var totalSteps = Session.Macro.Count;
        ImGui.SameLine(0, spacing * 2);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(totalSteps > 0 ? $"Step {stepCount}/{totalSteps}" : $"Step {stepCount}");

        var actions  = Session.Macro.Actions.ToArray();
        var waitTime = actions.Sum(a => a.Base().MacroWaitTime);
        if (waitTime > 0)
        {
            var timeStr = $"{waitTime} sec";
            ImGui.SameLine();
            ImGuiUtils.AlignRight(ImGui.CalcTextSize(timeStr).X);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted(timeStr);
        }
    }

    private void DrawGearConditionAlert()
    {
        if (!_plugin.Configuration.ShowGearCondition) return;

        var gearCondition = Gearsets.GetMinimumGearCondition();
        if (!gearCondition.HasValue) return;

        var pct = gearCondition.Value;
        if (pct >= 50f) return;

        ImGuiHelpers.ScaledDummy(2);

        var message = PluginImGuiUtils.BuildGearMessage(pct, _plugin.Configuration.EnableGearWearTracking, Session.RecipeData, _plugin.GearWearTracker);
        var variant = pct < 25f ? AlertVariant.Danger : AlertVariant.Warning;
        ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale);
    }

    private SimulationState? hoveredState;
    private SimulationState DisplayedState => hoveredState ?? (_plugin.Configuration.SynthHelperDisplayOnlyFirstStep ? Session.Macro.FirstState : Session.Macro.State);

    private void DrawMacro()
    {
        using var panel = ImRaii2.GroupPanel("Suggested Actions", -1, out _);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var imageSize = ImGui.GetFrameHeight() * 1.4f;
        var canExecute = !Service.Condition[ConditionFlag.ExecutingCraftingAction];
        var lastState = Session.Macro.InitialState;
        hoveredState = null;

        var itemsPerRow = (int)Math.Max(1, MathF.Floor((ImGui.GetContentRegionAvail().X + spacing) / (imageSize + spacing)));

        using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
        using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
        var count = Session.Macro.Count;
        for (var i = 0; i < count; i++)
        {
            if (i % itemsPerRow != 0)
                ImGui.SameLine(0, spacing);
            var (action, response, state) = (Session.Macro[i].Action, Session.Macro[i].Response, Session.Macro[i].State);
            var actionBase = action.Base();
            var failedAction = response != ActionResponse.UsedAction;
            using var _id = ImRaii.PushId(i);
            if (i == 0)
            {
                var pos = ImGui.GetCursorScreenPos();
                var offsetVec2 = ImGui.GetStyle().ItemSpacing / 2;
                var offset = new Vector2((offsetVec2.X + offsetVec2.Y) / 2f);
                var color = canExecute ? ImGuiColors.DalamudWhite2 : ImGuiColors.DalamudGrey3;
                ImGui.GetWindowDrawList().AddRectFilled(pos - offset, pos + new Vector2(imageSize) + offset, ImGui.GetColorU32(color), 4);
            }
            bool isHovered, isHeld, isPressed;
            {
                var pos = ImGui.GetCursorScreenPos();
                var offset = ImGui.GetStyle().ItemSpacing / 2f;
                var size = new Vector2(imageSize);

                // yoinked from https://github.com/goatcorp/Dalamud/blob/48e8462550141db9b1a153cab9548e60238500c7/Dalamud/Interface/Windowing/Window.cs#L551
                var min = pos - offset;
                var max = pos + size + offset;
                var bb = new Vector4(min.X, min.Y, max.X, max.Y);

                var id = ImGui.GetID($"###ButtonContainer");
                var isClipped = !ImGuiExtras.ItemAdd(bb, id, out _, 0);

                isPressed = ImGuiExtras.ButtonBehavior(bb, id, out isHovered, out isHeld, (int)ImGuiButtonFlags.None);
            }
            ImGui.ImageButton(action.GetIcon(Session.RecipeData!.ClassJob).Handle, new(imageSize), default, Vector2.One, 0, default, failedAction ? new(1, 1, 1, ImGui.GetStyle().DisabledAlpha) : Vector4.One);
            if (isPressed && i == 0)
            {
                if (ExecuteNextAction())
                    break;
            }
            if (isHovered)
            {
                ImGuiUtils.Tooltip($"{action.GetName(Session.RecipeData!.ClassJob)}\n" +
                    $"{actionBase.GetTooltip(CreateSim(lastState), true)}" +
                    $"{(canExecute && i == 0 ? "Click or run /craftaction to execute" : string.Empty)}");
                hoveredState = state;
            }
            lastState = state;
        }

        if (count == 0 && !Session.SolverRunning)
        {
            var reservedH = imageSize + ImGui.GetStyle().ItemSpacing.Y;
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGuiUtils.TextMiddleNewLine("Click \"Suggest Macro\" below to get a suggestion", new(ImGui.GetContentRegionAvail().X, reservedH));
        }
    }

    private void DrawMacroInfo()
    {
        var state = DisplayedState;

        // 1. Status strip: condition dot + Step X/Y + tempo estimado
        DrawStatusStrip(state);

        ImGuiHelpers.ScaledDummy(3);

        // 2. Progress bars
        var reliability = Session.Macro.GetReliability(Session.RecipeData!, _plugin.Configuration.SynthHelperDisplayOnlyFirstStep ? 0 : ^1);
        {
            var allBars = new List<DynamicBars.BarData>
            {
                new("Progress",   Colors.Progress,   reliability.Progress, state.Progress,  Session.RecipeData!.RecipeInfo.MaxProgress),
                new("Quality",    Colors.Quality,    reliability.Quality,  state.Quality,   Session.RecipeData.RecipeInfo.MaxQuality),
                new("CP",         Colors.CP,         state.CP,             Session.CharacterStats!.CP),
                new("Durability", Colors.Durability, state.Durability,     Session.RecipeData.RecipeInfo.MaxDurability),
            };
            if (Session.RecipeData.RecipeInfo.MaxQuality <= 0)
                allBars.RemoveAt(1);
            if (Session.RecipeData.IsCollectable)
                allBars.Add(new("Collect.", Colors.Collectability, reliability.ParamScore, state.Collectability, state.MaxCollectability, Session.RecipeData.CollectableThresholds, $"{state.Collectability}"));
            else if (Session.RecipeData.Recipe.RequiredQuality > 0)
            {
                var qualityPercent = (float)state.Quality / Session.RecipeData.Recipe.RequiredQuality * 100;
                allBars.Add(new("Quality %", Colors.HQ, reliability.ParamScore, qualityPercent, 100, null, $"{qualityPercent:0}%"));
            }
            else if (Session.RecipeData.RecipeInfo.MaxQuality > 0)
                allBars.Add(new("HQ %", Colors.HQ, reliability.ParamScore, state.HQPercent, 100, null, $"{state.HQPercent}%"));

            DynamicBars.DrawColumns(allBars, 2);
        }

        // 3. Buffs inline — nada é desenhado quando não há buff ativo.
        {
            var effects = state.ActiveEffects;
            var hasAnyEffect = false;
            foreach (var effect in AllEffectTypes)
                if (effects.HasEffect(effect)) { hasAnyEffect = true; break; }

            if (hasAnyEffect)
            {
                ImGuiHelpers.ScaledDummy(3);
                using var _font = AxisFont.Push();

                var iconHeight    = ImGui.GetFrameHeight() * 1.5f;
                var durationShift = iconHeight * .2f;

                var first = true;
                foreach (var effect in AllEffectTypes)
                {
                    if (!effects.HasEffect(effect))
                        continue;

                    if (!first)
                        ImGui.SameLine();
                    first = false;

                    using (ImRaii.Group())
                    {
                        var icon = effect.GetIcon(effects.GetStrength(effect));
                        var size = new Vector2(iconHeight * (icon.AspectRatio ?? 1), iconHeight);

                        ImGui.Image(icon.Handle, size);
                        if (!effect.IsIndefinite())
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - durationShift);
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
                }
            }
        }

        // 4. Gear condition alert
        DrawGearConditionAlert();

        // 5. Craft Complete banner
        if (Session.Macro.State.Progress >= Session.RecipeData!.RecipeInfo.MaxProgress)
        {
            var isHQ = Session.RecipeData.RecipeInfo.MaxQuality > 0 && Session.Macro.State.HQPercent > 0;
            var text = isHQ ? "✓ Craft Complete — HQ" : "✓ Craft Complete";
            DrawStatusBanner(text, Colors.Good);
        }
    }

    private static void DrawStatusBanner(string text, Vector4 color)
    {
        ImGuiHelpers.ScaledDummy(4);
        var pos     = ImGui.GetCursorScreenPos();
        var padX    = ImGui.GetStyle().WindowPadding.X;
        var availW  = ImGui.GetContentRegionAvail().X;
        var textH   = ImGui.GetTextLineHeight();
        var vertPad = ImGui.GetStyle().FramePadding.Y;
        var bannerH = textH + vertPad * 2;

        ImGui.GetWindowDrawList().AddRectFilled(
            new(pos.X - padX, pos.Y),
            new(pos.X + availW + padX, pos.Y + bannerH),
            ImGui.GetColorU32(color with { W = 0.12f }));

        ImGui.Dummy(new(availW, bannerH));
        ImGui.SetCursorScreenPos(new(pos.X, pos.Y + vertPad));
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGuiUtils.TextCentered(text, availW);
        ImGui.SetCursorScreenPos(new(pos.X, pos.Y + bannerH + ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawMacroActions()
    {
        if (Session.SolverRunning)
        {
            Theme.PushPrimaryButton();
            if (Session.SolverCancelling)
            {
                using var _disabled = ImRaii.Disabled();
                ImGui.Button("Stopping", new(-1, 0));
                ImGuiUtils.HoveredTooltip("This might could a while, sorry! Please report if this takes longer than a second.", wrapWidth: 300);
            }
            else
            {
                if (ImGui.Button("Stop", new(-1, 0)))
                    Session.CancelSolver();
            }
            Theme.PopPrimaryButton();
            return;
        }

        var isComplete = Session.RecipeData != null &&
                         Session.Macro.State.Progress >= Session.RecipeData.RecipeInfo.MaxProgress;
        var hasMacro = Session.Macro.Count > 0;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var halfW   = (ImGui.GetContentRegionAvail().X - spacing) / 2f;

        if (isComplete)
        {
            Theme.PushPrimaryButton();
            if (ImGui.Button("Open in Macro Editor", new(halfW, 0)))
                _plugin.OpenMacroEditor(Session.CharacterStats!, Session.RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), null, [], null);
            Theme.PopPrimaryButton();
            ImGui.SameLine(0, spacing);
            if (ImGui.Button("Generate New", new(halfW, 0)))
                AttemptRetry();
        }
        else
        {
            var label = hasMacro ? "Generate New" : "Suggest Macro";
            Theme.PushPrimaryButton();
            if (ImGui.Button(label, new(halfW, 0)))
                AttemptRetry();
            ImGuiUtils.HoveredTooltip(hasMacro
                ? "Generate a new macro suggestion from scratch, discarding the current one."
                : "Suggest a way to finish the crafting recipe. " +
                  "Results aren't perfect, and levels of success " +
                  "can vary wildly depending on the solver's settings.", wrapWidth: 300);
            Theme.PopPrimaryButton();
            ImGui.SameLine(0, spacing);
            if (ImGui.Button("Open in Macro Editor", new(halfW, 0)))
                _plugin.OpenMacroEditor(Session.CharacterStats!, Session.RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), null, [], null);
        }
    }

    /// <summary>
    /// Displays a progress bar showing how many actions of the current macro have
    /// been executed in-game, with slot information when MacroChain is enabled.
    /// </summary>
    private void DrawMacroExecutionProgress()
    {
        var total = Session.Macro.Count;
        if (total == 0) return;

        var current = Math.Clamp(Session.CurrentActionCount, 0, total);
        var fraction = (float)current / total;

        var config = _plugin.Configuration.MacroCopy;
        var actionsPerSlot = MacroCopy.MacroSize
            - (config.UseNextMacro ? 1 : 0)
            - (config.UseMacroLock ? 1 : 0);
        actionsPerSlot = Math.Max(1, actionsPerSlot);

        var totalSlots = (int)Math.Ceiling((float)total / actionsPerSlot);
        var currentSlot = Math.Clamp(current / actionsPerSlot + 1, 1, totalSlots);

        var overlay = totalSlots > 1
            ? $"Slot {currentSlot}/{totalSlots}  ·  {current}/{total}"
            : $"{current} / {total}";

        ImGuiUtils.ProgressBar(fraction, new(-1, ImGui.GetFrameHeight()), overlay);
    }
}
