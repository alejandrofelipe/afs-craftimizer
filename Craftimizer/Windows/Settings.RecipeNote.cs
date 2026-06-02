using Craftimizer.Solver;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Craftimizer.Windows;

public sealed partial class Settings
{
    private void DrawTabRecipeNote()
    {
        using var tab = ImRaii.TabItem("Crafting Log", ConsumeSelectedTab("Crafting Log"));
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawSectionTitle("GENERAL");

        DrawOption(
            "Pin Helper Window",
            "Pins the helper window to the right of your crafting log. Disabling this will " +
            "allow you to move it around.",
            Config.PinRecipeNoteToWindow,
            v => Config.PinRecipeNoteToWindow = v,
            ref isDirty,
            "Keeps the helper panel attached to the right of your crafting log."
        );

        DrawOption(
            "Always Collapse Helper Window",
            "Enabling this will cause the Helper Window to be collapsed whenever you start " +
            "a new craft, preventing the solver from running automatically.",
            Config.CollapseSynthHelper,
            v => Config.CollapseSynthHelper = v,
            ref isDirty,
            "Collapses the helper when switching recipes, preventing auto-solve."
        );

        DrawOption(
            "Automatically Suggest Macro",
            "(Can cause frame drops!) When navigating to a new recipe or changing your gear " +
            "stats, automatically suggest a new macro (equivalent to clicking \"Generate\" " +
            "in the Macro Editor). This can cause harsh frame drops on some computers or " +
            "recipes when underleveled while navigating the crafting log. Turning this off " +
            "provides a button to allow you to manually suggest a macro only when you need it.",
            Config.SuggestMacroAutomatically,
            v => Config.SuggestMacroAutomatically = v,
            ref isDirty,
            "Auto-generates a macro when a recipe changes. May cause frame drops."
        );

        DrawOption(
            "Enable Community Macros",
            "Use FFXIV Teamcraft's community rotations to search for and find the best possible " +
            "crowd-sourced macro for your craft. This sends a request to their servers to retrieve " +
            "a list of macros that apply to your craft's rlvl. Requests are only sent once per rlvl " +
            "and are always cached to reduce server load.",
            Config.ShowCommunityMacros,
            v => Config.ShowCommunityMacros = v,
            ref isDirty,
            "Fetches crowd-sourced macros from FFXIV Teamcraft for your recipe."
        );

        if (Config.ShowCommunityMacros)
        {
            DrawOption(
                "Automatically Search for Community Macro",
                "When navigating to a new recipe or changing your gear stats, automatically search " +
                "online for a new community macro.\n" +
                "This is turned off by default so you don't hammer their servers :)",
                Config.SearchCommunityMacroAutomatically,
                v => Config.SearchCommunityMacroAutomatically = v,
                ref isDirty,
                "Searches automatically when navigating to a new recipe."
            );
        }

        DrawSectionTitle("SOLVER CONFIGURATION");

        var solverConfig = Config.RecipeNoteSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.RecipeNoteDefault, false, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.RecipeNoteSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }
}
