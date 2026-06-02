using Craftimizer.Solver;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Craftimizer.Windows;

public sealed partial class Settings
{
    private void DrawTabSynthHelper()
    {
        using var tab = ImRaii.TabItem("Synthesis Helper", ConsumeSelectedTab("Synthesis Helper"));
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawSectionTitle("GENERAL");

        DrawOption(
            "Pin Helper Window",
            "Pins the synthesis helper to the right of your synthesis window. Disabling this will " +
            "allow you to move it around.",
            Config.PinSynthHelperToWindow,
            v => Config.PinSynthHelperToWindow = v,
            ref isDirty,
            "Keeps the helper anchored beside your synthesis window."
        );

        DrawOption(
            "Disable When Running Macro",
            "Disables itself when an in-game macro is running.",
            Config.DisableSynthHelperOnMacro,
            v => Config.DisableSynthHelperOnMacro = v,
            ref isDirty,
            "Pauses the overlay while an in-game macro is active."
        );

        DrawOption(
            "Auto-Save Successful Crafts",
            "Automatically saves the actions used when a craft completes successfully. " +
            "If a macro for that recipe already exists, it is only overwritten when the new result has higher quality.",
            Config.AutoSaveCraftMacro,
            v => Config.AutoSaveCraftMacro = v,
            ref isDirty,
            "Saves the action sequence when a craft completes successfully."
        );

        DrawOption(
            "Simulate Only First Step",
            "Only the first step is simulated by default. You can still " +
            "hover over the other steps to view their outcomes, but the " +
            "reliability trials (when hovering over the macro stats) are hidden.",
            Config.SynthHelperDisplayOnlyFirstStep,
            v => Config.SynthHelperDisplayOnlyFirstStep = v,
            ref isDirty,
            "Reduces CPU usage by only simulating the next step by default."
        );

        DrawOption(
            "Draw Ability Ants",
            "Turns your hotbar into a whack-a-mole game! Draws ants for " +
            "the next action that should be executed. Also disables ants " +
            "for things like combo actions and condition procs.",
            Config.SynthHelperAbilityAnts,
            v => Config.SynthHelperAbilityAnts = v,
            ref isDirty,
            "Highlights the suggested next action on your hotbar."
        );

        DrawOption(
            "Solver Step Count",
            "The minimum number of future steps to solve for during an in-game craft. " +
            "The solver may still give more than this amount if it's at no cost to you.",
            Config.SynthHelperStepCount,
            1,
            100,
            v => Config.SynthHelperStepCount = v,
            ref isDirty
        );

        DrawOption(
            "Max Step Display Count",
            "Enforces a maximum number of steps to display in the synth helper to " +
            "get rid of clutter.",
            Config.SynthHelperMaxDisplayCount,
            Config.SynthHelperStepCount,
            100,
            v => Config.SynthHelperMaxDisplayCount = v,
            ref isDirty
        );

        DrawSectionTitle("SOLVER CONFIGURATION");

        var solverConfig = Config.SynthHelperSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.SynthHelperDefault, true, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.SynthHelperSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }
}
