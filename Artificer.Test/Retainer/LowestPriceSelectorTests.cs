using System;
using System.Collections.Generic;
using Artificer.Application.Retainer;

namespace Artificer.Test.Retainer;

[TestClass]
public class LowestPriceSelectorTests
{
    private static readonly HashSet<ulong> Own = new() { 42UL };

    [TestMethod]
    public void PicksLowestMatchingQuality()
    {
        var offerings = new[]
        {
            new MarketOffering(200, IsHq: false, RetainerId: 1),
            new MarketOffering(150, IsHq: false, RetainerId: 2),
            new MarketOffering(100, IsHq: true,  RetainerId: 3), // qualidade errada
        };
        var r = LowestPriceSelector.SelectLowest(offerings, wantHq: false, Own);
        Assert.IsNotNull(r);
        Assert.AreEqual(150, r.Value.Price);
        Assert.IsFalse(r.Value.IsOwn);
    }

    [TestMethod]
    public void MarksOwnRetainerLowest()
    {
        var offerings = new[]
        {
            new MarketOffering(150, false, 1),
            new MarketOffering(120, false, 42), // seu
        };
        var r = LowestPriceSelector.SelectLowest(offerings, false, Own);
        Assert.IsNotNull(r);
        Assert.AreEqual(120, r.Value.Price);
        Assert.IsTrue(r.Value.IsOwn);
    }

    [TestMethod]
    public void ReturnsNullWhenNoMatchingQuality()
    {
        var offerings = new[] { new MarketOffering(100, true, 1) };
        Assert.IsNull(LowestPriceSelector.SelectLowest(offerings, wantHq: false, Own));
    }

    [TestMethod]
    public void ReturnsNullOnEmpty()
        => Assert.IsNull(LowestPriceSelector.SelectLowest(Array.Empty<MarketOffering>(), false, Own));
}
