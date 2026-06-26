using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Artificer.Windows;

public sealed partial class Settings
{
    private void DrawSolverConfig(ref SolverConfig configRef, SolverConfig defaultConfig, bool disableOptimal, out bool isDirty)
    {
        isDirty = false;

        var config = configRef;

        using (var panel = ImRaii2.GroupPanel("General", -1, out _))
        {
            if (ImGui.Button("Reset to Default", OptionButtonSize))
            {
                config = defaultConfig;
                isDirty = true;
            }

            DrawOption(
                "Algorithm",
                "The algorithm to use when solving for a macro. Different " +
                "algorithms provide different pros and cons for using them. " +
                "By far, the Optimal and Stepwise Genetic algorithms provide " +
                "the best results, especially for very difficult crafts.",
                GetAlgorithmName,
                GetAlgorithmTooltip,
                config.Algorithm,
                v => config = config with { Algorithm = v },
                ref isDirty,
                disableOptimal ? [SolverAlgorithm.Raphael] : []
            );

            if (config.Algorithm == SolverAlgorithm.NextActionForked)
            {
                DrawOption(
                    "Time Limit (ms)",
                    "Wall-clock budget in milliseconds for the Next Action Forked algorithm. " +
                    "Each candidate action gets an equal share of this budget.\n" +
                    "0 = use Target Iterations instead (no time limit).",
                    config.MaxTimeMs,
                    0,
                    60000,
                    v => config = config with { MaxTimeMs = v },
                    ref isDirty
                );
            }

            using (ImRaii.Disabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseGenetic or SolverAlgorithm.Raphael or SolverAlgorithm.NextActionForked)))
                DrawOption(
                    "Max Core Count",
                    "The number of cores to use when solving. You should use as many " +
                    "as you can. If it's too high, it will have an effect on your gameplay " +
                    $"experience. A good estimate would be 1 or 2 cores less than your " +
                    $"system (FYI, you have {Environment.ProcessorCount} cores), but make sure to accomodate " +
                    $"for any other tasks you have in the background, if you have any.\n" +
                    "(Only used in the Forked, Genetic, and Optimal algorithms)",
                    config.MaxThreadCount,
                    1,
                    Environment.ProcessorCount,
                    v => config = config with { MaxThreadCount = v },
                    ref isDirty
                );

            if (config.Algorithm != SolverAlgorithm.Raphael)
            {
                DrawOption(
                    "Target Iterations",
                    "The total number of iterations to run per crafting step. " +
                    "Higher values require more computational power. Higher values " +
                    "also may decrease variance, so other values should be tweaked " +
                    "as necessary to get a more favorable outcome.",
                    config.Iterations,
                    1000,
                    1000000,
                    v => config = config with { Iterations = v },
                    ref isDirty
                );

                DrawOption(
                    "Max Iterations",
                    "The solver may go about the target iteration value if the craft " +
                    "is sufficiently difficult, and it wasn't able to find any way to " +
                    "complete it yet. In rare cases, the solver might go on for a very " +
                    "long time. This maximum is here to prevent the solver from stealing " +
                    "all your RAM.",
                    config.MaxIterations,
                    config.Iterations,
                    5000000,
                    v => config = config with { MaxIterations = v },
                    ref isDirty
                );

                DrawOption(
                    "Max Step Count",
                    "The maximum number of crafting steps; this is generally the only " +
                    "setting you should change, and it should be set to around 5 steps " +
                    "more than what you'd expect. If this value is too low, the solver " +
                    "won't learn much per iteration; too high and it will waste time " +
                    "on useless extra steps.",
                    config.MaxStepCount,
                    1,
                    100,
                    v => config = config with { MaxStepCount = v },
                    ref isDirty
                );

                DrawOption(
                    "Exploration Constant",
                    "A constant that decides how often the solver will explore new, " +
                    "possibly good paths. If this value is too high, " +
                    "moves will mostly be decided at random.",
                    config.ExplorationConstant,
                    0,
                    10,
                    v => config = config with { ExplorationConstant = v },
                    ref isDirty
                );

                DrawOption(
                    "Score Weighting Constant",
                    "A constant ranging from 0 to 1 that configures how the solver " +
                    "scores and picks paths to travel to next. A value of 0 means " +
                    "actions will be chosen based on their average outcome, whereas " +
                    "1 uses their best outcome achieved so far.",
                    config.MaxScoreWeightingConstant,
                    0,
                    1,
                    v => config = config with { MaxScoreWeightingConstant = v },
                    ref isDirty
                );

                using (ImRaii.Disabled(config.Algorithm is not (SolverAlgorithm.OneshotForked or SolverAlgorithm.StepwiseForked or SolverAlgorithm.StepwiseGenetic or SolverAlgorithm.NextActionForked)))
                    DrawOption(
                        "Fork Count",
                        "Split the number of iterations across different solvers. In general, " +
                        "you should increase this value to at least the number of cores in " +
                        $"your system (FYI, you have {Environment.ProcessorCount} cores) to attain the most speedup. " +
                        "The higher the number, the more chance you have of finding a " +
                        "better local maximum; this concept similar but not equivalent " +
                        "to the exploration constant.\n" +
                        "(Only used in the Forked, Genetic, and Next Action Forked algorithms)",
                        config.ForkCount,
                        1,
                        500,
                        v => config = config with { ForkCount = v },
                        ref isDirty
                    );

                using (ImRaii.Disabled(config.Algorithm is not SolverAlgorithm.StepwiseGenetic))
                    DrawOption(
                        "Elitist Action Count",
                        "On every craft step, pick this many top solutions and use them as " +
                        "the input for the next craft step. For best results, use Fork Count / 2 " +
                        "and add about 1 or 2 more if needed.\n" +
                        "(Only used in the Stepwise Genetic algorithm)",
                        config.FurcatedActionCount,
                        1,
                        500,
                        v => config = config with { FurcatedActionCount = v },
                        ref isDirty
                    );
            }
            else
            {
                DrawOption(
                    "Backload Progress",
                    "Speeds up solve times. Backloads all Progress " +
                    "actions to the end of the rotation.",
                    config.BackloadProgress,
                    v => config = config with { BackloadProgress = v },
                    ref isDirty
                );
                DrawOption(
                    "Ensure Reliability",
                    "Find a rotation that can reach the target quality no matter " +
                    "how unlucky the random conditions are.",
                    config.Adversarial,
                    v => config = config with { Adversarial = v },
                    ref isDirty
                );

                if (config.Adversarial)
                {
                    ImGui.SameLine();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                    {
                        using var font = ImRaii.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    }
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.HoveredTooltip("\"Ensure Reliability\" uses a lot more memory and can significantly increase solve times.", wrapWidth: 300);
                }
            }
        }

        using (var panel = ImRaii2.GroupPanel("Quality Target", -1, out _))
        {
            ImGui.SetNextItemWidth(OptionWidth);
            var percent = config.QualityTargetPercent;
            if (ImGui.SliderInt("Quality Target %##qualityTarget", ref percent, 1, 100, "%d%%"))
            {
                config = config with { QualityTargetPercent = percent };
                isDirty = true;
            }
            if (ImGui.IsItemHovered())
                ImGuiUtils.HoveredTooltip(
                    "The quality % to aim for. The solver stops increasing quality once this " +
                    "target is reached, freeing CP and steps for fewer/safer actions.\n" +
                    "100% = always aim for maximum quality (default behavior).", wrapWidth: 300);

            DrawOption(
                "Cap at Max Collectability Tier",
                "For collectible recipes, cap quality at the highest collectability tier " +
                "instead of 100%. Reduces wasted quality points for crafts where more " +
                "quality than the top tier has no benefit. Requires plugin-side resolution.",
                config.QualityTargetToMaxCollectability,
                v => config = config with { QualityTargetToMaxCollectability = v },
                ref isDirty
            );
        }

        using (var panel = ImRaii2.GroupPanel("Action Pool", -1, out var poolWidth))
        {
            poolWidth -= ImGui.GetStyle().ItemSpacing.X * 2;

            ImGui.TextUnformatted("Select the actions you want the solver to choose from.");

            var pool = config.ActionPool;
            DrawActionPool(ref pool, poolWidth, out var isPoolDirty);
            if (isPoolDirty)
            {
                config = config with { ActionPool = pool };
                isDirty = true;
            }
        }

        if (config.Algorithm != SolverAlgorithm.Raphael)
        {
            using (var panel = ImRaii2.GroupPanel("Advanced", -1, out _))
            {
                DrawOption(
                    "Max Rollout Step Count",
                    "The maximum number of crafting steps every iteration can consider. " +
                    "Decreasing this value can have unintended side effects. Only change " +
                    "this value if you absolutely know what you're doing.",
                    config.MaxRolloutStepCount,
                    1,
                    50,
                    v => config = config with { MaxRolloutStepCount = v },
                    ref isDirty
                );

                DrawOption(
                    "Strict Actions",
                    "When finding the next possible actions to execute, use a heuristic " +
                    "to restrict which actions to attempt taking. This results in a much " +
                    "better macro at the cost of not finding an extremely creative one.",
                    config.StrictActions,
                    v => config = config with { StrictActions = v },
                    ref isDirty
                );
            }
        }

        if (isDirty)
            configRef = config;
    }

    private void DrawActionPool(ref ActionType[] actionPool, float poolWidth, out bool isDirty)
    {
        isDirty = false;

        var recipeData = _plugin.GetDefaultStats().Recipe;
        HashSet<ActionType> pool = [.. actionPool];

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
            using var panel = ImRaii2.GroupPanel(category.GetDisplayName(), poolWidth, out var availSpace);
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
                    var isEnabled = pool.Contains(actions[i]);
                    var isInefficient = SolverConfig.InefficientActions.Contains(actions[i]);
                    var isRisky = SolverConfig.RiskyActions.Contains(actions[i]);
                    var iconTint = Vector4.One;
                    if (!isEnabled)
                        iconTint = new(1, 1, 1, ImGui.GetStyle().DisabledAlpha);
                    else if (isInefficient)
                        iconTint = new(1, 1f, .5f, 1);
                    else if (isRisky)
                        iconTint = new(1, .5f, .5f, 1);
                    if (ImGui.ImageButton(actions[i].GetIcon(recipeData.ClassJob).Handle, new(imageSize), default, Vector2.One, 0, default, iconTint))
                    {
                        isDirty = true;
                        if (isEnabled)
                            pool.Remove(actions[i]);
                        else
                            pool.Add(actions[i]);
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        var s = new StringBuilder();
                        s.AppendLine(actions[i].GetName(recipeData.ClassJob));
                        if (isInefficient)
                            s.AppendLine(
                                "Not recommended. This action may be randomly used in a " +
                                "detrimental way to the rest of the craft. Always use " +
                                "your best judgement if enabling this action.");
                        if (isRisky)
                            s.AppendLine(
                                "Useless; the solver currently doesn't take any risks in " +
                                "its crafts. It only takes steps that have a 100% chance of " +
                                "succeeding. If you want have a moment where you want to take " +
                                "risks in your craft (like in expert recipes), don't rely " +
                                "on the solver during that time.");
                        ImGuiUtils.TooltipWrapped(s.ToString());
                    }
                }
                else
                    ImGui.Dummy(new(imageSize));
            }
        }

        if (isDirty)
        {
            bool InPool(BaseComboAction action)
            {
                if (action.ActionTypeA.Base() is BaseComboAction { } aCombo)
                {
                    if (!InPool(aCombo))
                        return false;
                }
                else
                {
                    if (!pool.Contains(action.ActionTypeA))
                        return false;
                }
                if (action.ActionTypeB.Base() is BaseComboAction { } bCombo)
                {
                    if (!InPool(bCombo))
                        return false;
                }
                else
                {
                    if (!pool.Contains(action.ActionTypeB))
                        return false;
                }
                return true;
            }

            foreach (var combo in ActionCategory.Combo.GetActions())
            {
                if (combo.Base() is BaseComboAction { } comboAction)
                {
                    if (!InPool(comboAction))
                        pool.Remove(combo);
                    else
                        pool.Add(combo);
                }
            }
            actionPool = [.. pool];
        }
    }
}
