using Craftimizer.Plugin;

namespace Craftimizer.Utils;

// Solver-specific progress bar helpers.
// Uses ProgressBarComponent from Craftimizer.UI via project reference.
internal static class SolverProgressBar
{
    public static ProgressBarComponent.ProgressSnapshot FromSolver(Solver.Solver solver, string? nameOverride = null)
    {
        var state = solver.IsIndeterminate
            ? ProgressBarComponent.ProgressState.Indeterminate
            : (solver.ProgressValue >= solver.ProgressMax ? ProgressBarComponent.ProgressState.Completed : ProgressBarComponent.ProgressState.InProgress);

        return new ProgressBarComponent.ProgressSnapshot(
            Name: nameOverride ?? "Solver",
            CurrentValue: solver.ProgressValue,
            MaxValue: solver.ProgressMax,
            State: state,
            Stage: solver.ProgressStage
        );
    }

    public static void DrawProgressBarCompat(
        Solver.Solver solver,
        ProgressBarType progressType,
        float? availSpace = null)
    {
        var snapshot = FromSolver(solver);
        var config = new ProgressBarComponent.VisualConfig(
            Mode: progressType == ProgressBarType.None ? ProgressBarComponent.DisplayMode.Compact : ProgressBarComponent.DisplayMode.Horizontal,
            ColorTheme: progressType == ProgressBarType.Simple ? ProgressBarType.Simple : ProgressBarType.Colorful,
            Width: availSpace
        );
        ProgressBarComponent.DrawSingle(snapshot, config);
    }
}
