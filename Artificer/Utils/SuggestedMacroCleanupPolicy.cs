namespace Artificer.Utils;

internal static class SuggestedMacroCleanupPolicy
{
    public static bool ShouldClearForChangedInputs(bool inputsChanged, bool isCraftable) =>
        inputsChanged && !isCraftable;

    public static bool ShouldClearWhenAutomaticSuggestionChanges(bool wasEnabled, bool isEnabled) =>
        wasEnabled && !isEnabled;
}
