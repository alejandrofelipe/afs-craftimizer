using Craftimizer.Plugin;

namespace Craftimizer.Utils;

// Plugin-side members of ProgressBarComponent that depend on the Solver project.
// The pure-UI portion lives in Craftimizer.UI/ProgressBarComponent.cs.
public static partial class ProgressBarComponent
{
    #region Helpers para Migração

    /// <summary>
    /// Converte um objeto Solver para ProgressSnapshot.
    /// Helper de migração do sistema antigo DynamicBars para ProgressBarComponent.
    /// </summary>
    /// <param name="solver">Instância do solver em execução</param>
    /// <param name="nameOverride">Nome customizado para exibição (default: "Solver")</param>
    /// <returns>Snapshot imutável com estado atual do solver</returns>
    public static ProgressSnapshot FromSolver(Solver.Solver solver, string? nameOverride = null)
    {
        var state = solver.IsIndeterminate
            ? ProgressState.Indeterminate
            : (solver.ProgressValue >= solver.ProgressMax ? ProgressState.Completed : ProgressState.InProgress);

        return new ProgressSnapshot(
            Name: nameOverride ?? "Solver",
            CurrentValue: solver.ProgressValue,
            MaxValue: solver.ProgressMax,
            State: state,
            Stage: solver.ProgressStage
        );
    }

    /// <summary>
    /// Wrapper de compatibilidade com DynamicBars.DrawProgressBar existente.
    /// Mantido para transição gradual. Novo código deve usar DrawSingle ou DrawAggregated.
    /// </summary>
    /// <param name="solver">Instância do solver em execução</param>
    /// <param name="progressType">Tipo de barra de progresso (Colorful/Simple/None)</param>
    /// <param name="availSpace">Espaço disponível em pixels (null = usar espaço disponível)</param>
    public static void DrawProgressBarCompat(
        Solver.Solver solver,
        Configuration.ProgressBarType progressType,
        float? availSpace = null)
    {
        var snapshot = FromSolver(solver);
        var config = new VisualConfig(
            Mode: progressType == Configuration.ProgressBarType.None ? DisplayMode.Compact : DisplayMode.Horizontal,
            ColorTheme: progressType == Configuration.ProgressBarType.Simple ? ProgressBarType.Simple : ProgressBarType.Colorful,
            Width: availSpace
        );

        DrawSingle(snapshot, config);
    }

    #endregion
}
