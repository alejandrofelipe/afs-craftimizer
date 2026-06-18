using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;

namespace Artificer.Test.UI;

[TestClass]
public class DrawCenteredIconTests
{
    [TestMethod]
    [DataRow(20f, 20f, 12f, 12f, 4f,  4f)]  // square icon in square area
    [DataRow(20f, 20f, 12f,  8f, 4f,  6f)]  // icon shorter than area height
    [DataRow(20f, 20f,  8f, 12f, 6f,  4f)]  // icon narrower than area width
    [DataRow(20f, 10f, 12f,  8f, 4f,  1f)]  // non-square area
    [DataRow(16f, 16f, 16f, 16f, 0f,  0f)]  // icon fills area exactly
    public void CenteredOffset_CentersWithinArea(
        float areaW, float areaH, float iconW, float iconH,
        float expectedX, float expectedY)
    {
        var result = ImGuiUtils.CenteredOffset(new Vector2(iconW, iconH), new Vector2(areaW, areaH));
        Assert.AreEqual(expectedX, result.X, 0.01f);
        Assert.AreEqual(expectedY, result.Y, 0.01f);
    }
}
