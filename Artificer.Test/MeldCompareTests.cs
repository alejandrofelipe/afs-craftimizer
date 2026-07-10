using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Artificer.Test;

[TestClass]
public class MeldCompareTests
{
    private static (CraftStat, int) M(CraftStat s, int g) => (s, g);

    [TestMethod]
    public void AllSatisfied_WhenCurrentMatchesExactly()
    {
        var target  = new[] { M(CraftStat.Control, 12), M(CraftStat.CP, 12) };
        var current = new[] { M(CraftStat.Control, 12), M(CraftStat.CP, 12) };
        var r = MeldCompare.CompareMelds(target, 2, current);
        Assert.IsTrue(r[0].Satisfied);
        Assert.IsTrue(r[1].Satisfied);
    }

    [TestMethod]
    public void HigherGradeSatisfiesLowerTarget()
    {
        var target  = new[] { M(CraftStat.Control, 10) };
        var current = new[] { M(CraftStat.Control, 12) };
        Assert.IsTrue(MeldCompare.CompareMelds(target, 1, current)[0].Satisfied);
    }

    [TestMethod]
    public void LowerGradeDoesNotSatisfy()
    {
        var target  = new[] { M(CraftStat.Control, 12) };
        var current = new[] { M(CraftStat.Control, 10) };
        Assert.IsFalse(MeldCompare.CompareMelds(target, 1, current)[0].Satisfied);
    }

    [TestMethod]
    public void WrongStatDoesNotSatisfy()
    {
        var target  = new[] { M(CraftStat.CP, 10) };
        var current = new[] { M(CraftStat.Control, 12) };
        Assert.IsFalse(MeldCompare.CompareMelds(target, 1, current)[0].Satisfied);
    }

    [TestMethod]
    public void MarksOvermeldByIndex()
    {
        var target  = new[] { M(CraftStat.Control, 12), M(CraftStat.CP, 10) };
        var r = MeldCompare.CompareMelds(target, 1, Array.Empty<(CraftStat, int)>());
        Assert.IsFalse(r[0].IsOvermeld);
        Assert.IsTrue(r[1].IsOvermeld);
    }

    [TestMethod]
    public void OneCurrentSatisfiesOnlyOneTarget()
    {
        var target  = new[] { M(CraftStat.CP, 10), M(CraftStat.CP, 10) };
        var current = new[] { M(CraftStat.CP, 12) };
        var r = MeldCompare.CompareMelds(target, 2, current);
        Assert.AreEqual(1, (r[0].Satisfied ? 1 : 0) + (r[1].Satisfied ? 1 : 0));
    }

    [TestMethod]
    public void OptimalMatching_HighTargetTakesHighCurrent()
    {
        var target  = new[] { M(CraftStat.CP, 12), M(CraftStat.CP, 10) };
        var current = new[] { M(CraftStat.CP, 10), M(CraftStat.CP, 12) };
        var r = MeldCompare.CompareMelds(target, 2, current);
        Assert.IsTrue(r[0].Satisfied);
        Assert.IsTrue(r[1].Satisfied);
    }
}
