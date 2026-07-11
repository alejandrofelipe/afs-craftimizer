using Artificer.Simulator.Actions;
using Artificer.Solver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Artificer.Test.Solver;

[TestClass]
public class SolverConfigTests
{
    private static SolverConfig WithSpecialist() =>
        new() { MaxStepCount = 30, ActionPool = [ActionType.HeartAndSoul, ActionType.BasicSynthesis] };

    [TestMethod]
    public void ForDelineations_CheckOff_KeepsSpecialistActions()
    {
        // CheckDelineations desligado → nunca filtra, independentemente de ter delineations.
        var c = WithSpecialist();
        Assert.IsTrue(c.ForDelineations(checkDelineations: false, hasDelineations: false).ActionPool.Contains(ActionType.HeartAndSoul));
        Assert.IsTrue(c.ForDelineations(checkDelineations: false, hasDelineations: true).ActionPool.Contains(ActionType.HeartAndSoul));
    }

    [TestMethod]
    public void ForDelineations_CheckOn_HasDelineations_KeepsSpecialistActions()
    {
        var c = WithSpecialist();
        Assert.IsTrue(c.ForDelineations(checkDelineations: true, hasDelineations: true).ActionPool.Contains(ActionType.HeartAndSoul));
    }

    [TestMethod]
    public void ForDelineations_CheckOn_NoDelineations_FiltersSpecialistActions()
    {
        // check ligado E sem delineations → filtra (== FilterSpecialistActions()).
        var c = WithSpecialist();
        var filtered = c.ForDelineations(checkDelineations: true, hasDelineations: false);
        CollectionAssert.AreEqual(c.FilterSpecialistActions().ActionPool.ToArray(), filtered.ActionPool.ToArray());
        Assert.IsFalse(filtered.ActionPool.Contains(ActionType.HeartAndSoul));
    }
}
