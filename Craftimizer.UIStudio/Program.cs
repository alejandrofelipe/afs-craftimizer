using Craftimizer.UIStudio;
using Craftimizer.UIStudio.Stories;

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
    new SynthHelperStory(),
    new RecipeNoteStory(),
    new CosmicTrackerStory(),
]);
