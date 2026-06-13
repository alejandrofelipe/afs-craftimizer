using Artificer.UIStudio;
using Artificer.UIStudio.Stories;

StudioApp.Run(
[
    new ColorsStory(),
    new ThemeStory(),
    new EmptyStateStory(),
    new ProgressBarStory(),
    new ChartsStory(),
    new BarsStory(),
    new TabbedWindowStory(),
    new ListWindowStory(),
    new StatDashboardStory(),
    new FloatingOverlayStory(),
    new DialogStory(),
    new SinglePanelStory(),
    // Pages
    new MacroEditorStory(),
    new SynthesisHelperStory(),
    new CraftingHelperStory(),
    new CosmicTrackerStory(),
    new MacroClipboardStory(),
    new MacroLibraryStory(),
    new SettingsStory(),
    new CraftingListWindowStory(),
    new CraftingListAddWindowStory(),
    new CraftingListDetailWindowStory(),
    new CraftingListMergeWindowStory(),
    new FeatureHubStory(),
]);
