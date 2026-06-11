using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Artificer.Windows;

public sealed partial class MacroEditor
{
    private void DrawActionHotbars()
    {
        var sim = CreateSim(State);

        var imageSize = ImGui.GetFrameHeight() * 2;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
        using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
        using var _alpha = ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, ImGui.GetStyle().DisabledAlpha * .5f);
        foreach (var category in Enum.GetValues<ActionCategory>())
        {
            if (category == ActionCategory.Combo)
                continue;

            var actions = category.GetActions();
            using var panel = ImRaii2.GroupPanel(category.GetDisplayName(), -1, out var availSpace);
            var itemsPerRow = (int)MathF.Floor((availSpace + spacing) / (imageSize + spacing));
            var itemCount = actions.Count;
            var iterCount = (int)(Math.Ceiling((float)itemCount / itemsPerRow) * itemsPerRow);
            for (var i = 0; i < iterCount; i++)
            {
                if (i % itemsPerRow != 0)
                    ImGui.SameLine(0, spacing);
                if (i < itemCount)
                {
                    var actionBase = actions[i].Base();
                    var canUse = actionBase.CanUse(sim);

                    // Category tint: draw a subtle colored background behind the action icon
                    var tintColor = category switch
                    {
                        ActionCategory.Synthesis or ActionCategory.FirstTurn => Colors.ActionSynth,
                        ActionCategory.Quality => Colors.ActionTouch,
                        ActionCategory.Buffs => Colors.ActionBuff,
                        _ => Colors.ActionSpecial,
                    };
                    var slotPos = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddRectFilled(
                        slotPos,
                        slotPos + new Vector2(imageSize),
                        ImGui.GetColorU32(new Vector4(tintColor.X, tintColor.Y, tintColor.Z, 0.10f)),
                        ImGui.GetStyle().FrameRounding);

                    if (ImGui.ImageButton(actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(imageSize), default, Vector2.One, 0, default, !canUse ? new(1, 1, 1, ImGui.GetStyle().DisabledAlpha) : Vector4.One) && !SolverRunning)
                        AddStep(actions[i]);
                    if (!canUse &&
                        (CharacterStats.Level < actionBase.Level ||
                            (actions[i] == ActionType.Manipulation && !CharacterStats.CanUseManipulation) ||
                            (actions[i] is ActionType.HeartAndSoul or ActionType.CarefulObservation or ActionType.QuickInnovation && !CharacterStats.IsSpecialist)
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
                        ImGuiUtils.Tooltip($"{actions[i].GetName(RecipeData!.ClassJob)}\n{actionBase.GetTooltip(sim, true)}");

                    using var _padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                    using (var _source = ImRaii.DragDropSource())
                    {
                        if (_source)
                        {
                            ImGuiExtras.SetDragDropPayload("macroActionInsert", actions[i]);
                            ImGui.ImageButton(actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(imageSize));
                        }
                    }
                }
                else
                    ImGui.Dummy(new(imageSize));
            }
        }

        var minY = ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().CellPadding.Y;
        if (MinWindowHeight != minY)
            MinWindowHeight = minY;
    }
}
