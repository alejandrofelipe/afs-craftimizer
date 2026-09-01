using Artificer.Application.Fishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.Application.Fishing;

[TestClass]
public sealed class CosmicFishFormatTests
{
    [DataTestMethod]
    [DataRow(FishTug.Weak, "!")]
    [DataRow(FishTug.Strong, "!!")]
    [DataRow(FishTug.Legendary, "!!!")]
    public void TugText_MapsAllValues(FishTug tug, string expected)
        => Assert.AreEqual(expected, CosmicFishFormat.TugText(tug));

    [DataTestMethod]
    [DataRow(FishHookset.Regular, "Hook")]
    [DataRow(FishHookset.Precise, "Precision Hookset")]
    [DataRow(FishHookset.Powerful, "Powerful Hookset")]
    [DataRow(FishHookset.Stellar, "Stellar Hookset")]
    [DataRow(FishHookset.Unknown, "?")]
    public void HooksetName_MapsAllValues(FishHookset h, string expected)
        => Assert.AreEqual(expected, CosmicFishFormat.HooksetName(h));
}
