namespace Artificer.Utils;

public enum BestMacroSource
{
    None,
    Saved,
    Suggested,
}

public static partial class ImGuiUtils
{
    /// <summary>
    /// Escolhe qual fonte de macro exibir no card unificado "Best Macro".
    /// Maior score ganha; empate prefere a salva (não descarta trabalho salvo por ruído).
    /// null = fonte ausente.
    /// </summary>
    public static BestMacroSource PickBestMacroSource(float? savedScore, float? suggestedScore)
    {
        if (savedScore is null && suggestedScore is null)
            return BestMacroSource.None;
        if (suggestedScore is null)
            return BestMacroSource.Saved;
        if (savedScore is null)
            return BestMacroSource.Suggested;
        return suggestedScore.Value > savedScore.Value
            ? BestMacroSource.Suggested
            : BestMacroSource.Saved;
    }
}
