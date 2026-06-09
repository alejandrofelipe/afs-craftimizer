using Craftimizer.Solver;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Craftimizer.Windows;

public sealed partial class Settings
{
    private void DrawTabMacroEditor()
    {
        using var tab = ImRaii.TabItem("Macro Editor", ConsumeSelectedTab("Macro Editor"));
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawSectionTitle("SIMULATION");

        DrawOption(
            "Reliability Trial Count",
            "When testing for reliability of a macro in the editor, this many trials will be " +
            "run. You should set this value to at least 100 to get a reliable spread of data. " +
            "If it's too low, you may not find an outlier, and the average might be skewed.",
            Config.ReliabilitySimulationCount,
            5,
            5000,
            v => Config.ReliabilitySimulationCount = v,
            ref isDirty
        );

        DrawSectionTitle("SOLVER CONFIGURATION");

        var solverConfig = Config.EditorSolverConfig;
        DrawSolverConfig(ref solverConfig, SolverConfig.EditorDefault, false, out var isSolverDirty);
        if (isSolverDirty)
        {
            Config.EditorSolverConfig = solverConfig;
            isDirty = true;
        }

        if (isDirty)
            Config.Save();
    }
}
