using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.UI;

// Note: PluginImGuiUtils.BuildGearMessage lives in the Artificer (plugin) project which the
// test project cannot reference (Dalamud.NET.Sdk dependency). The early-exit formatting logic
// was extracted into ImGuiUtils.FormatGearRepairMessage (Artificer.UI) to keep it testable.
// These tests verify the format contract: pct rounded to integer + em-dash + repair message.

[TestClass]
public class GearMessageTests
{
    [TestMethod]
    public void FormatGearRepairMessage_ReturnsPercentWithRepairText()
    {
        var result = ImGuiUtils.FormatGearRepairMessage(27f);
        Assert.AreEqual("27% — Repair gear before continuing!", result);
    }

    [TestMethod]
    public void FormatGearRepairMessage_FullDurability_ReturnsRepairMessage()
    {
        var result = ImGuiUtils.FormatGearRepairMessage(100f);
        Assert.AreEqual("100% — Repair gear before continuing!", result);
    }

    [TestMethod]
    public void FormatGearRepairMessage_NoParenNoData()
    {
        var result = ImGuiUtils.FormatGearRepairMessage(50f);
        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex(@"\(no data\)"));
    }

    [TestMethod]
    public void FormatGearRepairMessage_RoundsToInteger()
    {
        // 27.6 should round to 28, not truncate to 27
        var result = ImGuiUtils.FormatGearRepairMessage(27.6f);
        Assert.AreEqual("28% — Repair gear before continuing!", result);
    }
}
