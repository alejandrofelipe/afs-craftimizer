using SolverEngine = Artificer.Solver.Solver;

namespace Artificer.Test.Solver;

/// <summary>
/// Smoke de integração do solver Raphael nativo (raphael.dll via Raphael.Net 5.0.0):
/// roda o solver óptimo end-to-end e confirma que a lib nativa carrega e produz uma solução
/// válida e executável. Cobre o caminho que os testes de mapeamento (RaphaelUtilsTests) não tocam.
/// </summary>
[TestClass]
public class RaphaelSolverIntegrationTests
{
    [TestMethod]
    public async Task RaphaelSolver_ProducesExecutableSolution()
    {
        var input = new SimulationInput(
            new CharacterStats { Craftsmanship = 3304, Control = 3374, CP = 575, Level = 90, CanUseManipulation = true },
            new RecipeInfo
            {
                ClassJobLevel = 90, MaxDurability = 60, MaxQuality = 1000, MaxProgress = 300,
                QualityModifier = 80, QualityDivider = 115, ProgressModifier = 90, ProgressDivider = 130,
            });
        var state = new SimulationState(input);
        var config = new SolverConfig() with { Algorithm = SolverAlgorithm.Raphael };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var solver = new SolverEngine(config, state) { Token = cts.Token };
        solver.Start();
        var solution = await solver.GetTask();

        Assert.IsTrue(solution.Actions.Count > 0,
            "O solver Raphael (nativo 5.0.0) deve produzir uma solução não-vazia.");

        // A solução deve ser executável no simulador e completar o craft.
        var (resp, _, failedIdx) = new SimulatorNoRandom().ExecuteMultiple(state, solution.Actions);
        Assert.AreEqual(-1, failedIdx, "Nenhuma ação da solução deve falhar ao executar.");
    }
}
