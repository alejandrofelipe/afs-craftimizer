namespace Craftimizer.Utils;

using System.Numerics;

/// <summary>
/// Centralised numeric constants for UI layout, stat clamping, and crafting
/// rules that previously existed as inline magic numbers scattered across
/// the Windows classes.
/// </summary>
internal static class UIConstants
{
    // ── Window sizing ─────────────────────────────────────────────────────────
    public const float SynthHelperWidth   = 494f;
    public const float MacroListMinWidth  = 465f;
    public const float MacroListMinHeight = 520f;

    // ── Character stat clamping ───────────────────────────────────────────────
    public const int MaxCraftStat = 9000;
    public const int MinCP        = 180;
    public const int MaxCP        = 1000;

    // ── Job-level thresholds ──────────────────────────────────────────────────
    public const int SpecialistMinLevel   = 55;
    public const int SplendorousMinLevel  = 90;

    // ── Macro list pagination ─────────────────────────────────────────────────
    public const int MacrosPerPage = 20;

    // ── MacroEditor window ────────────────────────────────────────────────────
    public const int MacroEditorMaxWidth = 2184;

    // ── Buff/status IDs (verified patch 7.x) ─────────────────────────────────
    public const ushort StatusIdWellFed           = 48;
    public const ushort StatusIdMedicated         = 49;
    public const ushort StatusIdFCCraftsmanship   = 356;
    public const ushort StatusIdFCControl         = 357;

    // ── Job icon UV (48×48 sprite within 56×56 atlas, 3px top / 6px left pad) ─
    public static readonly Vector2 ItemIconUv0 = new Vector2(6, 3) / 56f;
    public static readonly Vector2 ItemIconUv1 = new Vector2(50, 47) / 56f;

    // ── Settings / About page ──────────────────────────────────────────────────
    public const float PluginIconSize = 128f;
}
