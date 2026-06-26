namespace Artificer.Simulator;

public sealed record RecipeInfo
{
    public bool IsExpert { get; init; }
    public int ClassJobLevel { get; init; }
    public ushort ConditionsFlag { get; init; }
    public int MaxDurability { get; init; }
    public int MaxQuality { get; init; }
    public int MaxProgress { get; init; }

    // Quality necessária para atingir o maior threshold de collectability da receita.
    // Depende do tipo de collectable (Cosmic Exploration sempre busca o máximo; Custom Deliveries
    // têm target fixo). Null = sem cap de collectability.
    public int? CollectableTargetQuality { get; init; }

    public int QualityModifier { get; init; }
    public int QualityDivider { get; init; }
    public int ProgressModifier { get; init; }
    public int ProgressDivider { get; init; }
}
