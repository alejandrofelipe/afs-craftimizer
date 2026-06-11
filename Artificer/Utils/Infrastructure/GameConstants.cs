namespace Artificer.Utils;

/// <summary>
/// FFXIV game constants derived from game data. Verify these after major patches.
/// </summary>
internal static class GameConstants
{
    /// <summary>
    /// Memory layout offsets for game structs. Discovered via CheatEngine/ReClass.NET.
    /// Re-verify with each FFXIV patch using ffxiv-memory-offset-debug skill if CSRecipeNote breaks.
    /// </summary>
    internal static class Offsets
    {
        /// <summary>Offset of ActiveCraftRecipeId within the RecipeNote struct. Struct total size: 2880 bytes.</summary>
        public const int RecipeNoteActiveCraftRecipeId = 0x118;
    }

    /// <summary>
    /// Status effect IDs that affect crafting stats.
    /// Source: Lumina Status sheet rows, verified via StatusList inspection.
    /// </summary>
    internal static class CrafterStatusIds
    {
        /// <summary>Well Fed — food buff granting Craftsmanship/Control/CP bonuses.</summary>
        public const uint WellFed = 48;

        /// <summary>Medicated — medicine buff granting Craftsmanship/Control/CP bonuses.</summary>
        public const uint Medicated = 49;

        /// <summary>In Control — FC buff granting Craftsmanship bonus.</summary>
        public const uint InControl = 356;

        /// <summary>Eat from the Hand — FC buff granting Control bonus.</summary>
        public const uint EatFromTheHand = 357;
    }
}
