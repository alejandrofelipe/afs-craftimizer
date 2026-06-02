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
