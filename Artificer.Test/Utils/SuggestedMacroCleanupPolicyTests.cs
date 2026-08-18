using Artificer.Utils;

namespace Artificer.Test.Utils;

[TestClass]
public class SuggestedMacroCleanupPolicyTests
{
    [DataTestMethod]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(false, false, false)]
    public void ShouldClearForChangedInputs_ReturnsTrueOnlyWhenCraftIsNotCraftable(bool inputsChanged, bool isCraftable, bool expected)
    {
        Assert.AreEqual(expected, SuggestedMacroCleanupPolicy.ShouldClearForChangedInputs(inputsChanged, isCraftable));
    }

    [DataTestMethod]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(false, false, false)]
    public void ShouldClearWhenAutomaticSuggestionChanges_ReturnsTrueOnlyWhenDisabled(bool wasEnabled, bool isEnabled, bool expected)
    {
        Assert.AreEqual(expected, SuggestedMacroCleanupPolicy.ShouldClearWhenAutomaticSuggestionChanges(wasEnabled, isEnabled));
    }
}
