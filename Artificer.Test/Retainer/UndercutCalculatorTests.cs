using Artificer.Application.Retainer;

namespace Artificer.Test.Retainer;

[TestClass]
public class UndercutCalculatorTests
{
    [TestMethod]
    public void Fixed_SubtractsAmount()
        => Assert.AreEqual(99, UndercutCalculator.Compute(100, UndercutMode.FixedAmount, 1, lowestIsOwnRetainer: false, undercutSelf: false));

    [TestMethod]
    public void Percentage_TakesPercentOff()
        => Assert.AreEqual(95, UndercutCalculator.Compute(100, UndercutMode.Percentage, 5, false, false));

    [TestMethod]
    public void ClampsToFloor_NeverBelowOne()
        => Assert.AreEqual(1, UndercutCalculator.Compute(1, UndercutMode.FixedAmount, 1, false, false));

    [TestMethod]
    public void OwnRetainer_NoUndercutSelf_MatchesPrice()
        => Assert.AreEqual(100, UndercutCalculator.Compute(100, UndercutMode.FixedAmount, 1, lowestIsOwnRetainer: true, undercutSelf: false));

    [TestMethod]
    public void OwnRetainer_UndercutSelfEnabled_StillUndercuts()
        => Assert.AreEqual(99, UndercutCalculator.Compute(100, UndercutMode.FixedAmount, 1, lowestIsOwnRetainer: true, undercutSelf: true));

    [TestMethod]
    public void NoMarketData_ReturnsFloor()
        => Assert.AreEqual(1, UndercutCalculator.Compute(0, UndercutMode.FixedAmount, 1, false, false));
}
