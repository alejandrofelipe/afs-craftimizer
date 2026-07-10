using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.UI;

[TestClass]
public class BestMacroSelectionTests
{
    [TestMethod]
    public void BothNull_ReturnsNone()
        => Assert.AreEqual(BestMacroSource.None, ImGuiUtils.PickBestMacroSource(null, null));

    [TestMethod]
    public void OnlySaved_ReturnsSaved()
        => Assert.AreEqual(BestMacroSource.Saved, ImGuiUtils.PickBestMacroSource(0.5f, null));

    [TestMethod]
    public void OnlySuggested_ReturnsSuggested()
        => Assert.AreEqual(BestMacroSource.Suggested, ImGuiUtils.PickBestMacroSource(null, 0.5f));

    [TestMethod]
    public void SuggestedHigher_ReturnsSuggested()
        => Assert.AreEqual(BestMacroSource.Suggested, ImGuiUtils.PickBestMacroSource(0.80f, 0.92f));

    [TestMethod]
    public void SavedHigher_ReturnsSaved()
        => Assert.AreEqual(BestMacroSource.Saved, ImGuiUtils.PickBestMacroSource(0.92f, 0.80f));

    [TestMethod]
    public void Tie_PrefersSaved()
        => Assert.AreEqual(BestMacroSource.Saved, ImGuiUtils.PickBestMacroSource(0.85f, 0.85f));
}
