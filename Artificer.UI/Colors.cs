using System;
using System.Numerics;

namespace Artificer.Utils;

public static class Colors
{
    // ── Stat bars ─────────────────────────────────────────────────────────────
    public static readonly Vector4 Progress    = new(0.322f, 0.898f, 0.627f, 1f); // #52E5A0 teal-green
    public static readonly Vector4 Quality     = new(0.690f, 0.482f, 1.000f, 1f); // #B07BFF violet
    public static readonly Vector4 Durability  = new(1.000f, 0.722f, 0.290f, 1f); // #FFB84A amber
    public static readonly Vector4 HQ          = new(0.592f, 0.863f, 0.376f, 1f);
    public static readonly Vector4 Collectability = new(0.255f, 0.769f, 0.910f, 1f); // #42C4E8 sky
    public static readonly Vector4 CP          = new(1.000f, 0.424f, 0.541f, 1f); // #FF6C8A rose

    // ── Action category tints ─────────────────────────────────────────────────
    public static readonly Vector4 ActionSynth   = Progress;
    public static readonly Vector4 ActionTouch   = Quality;
    public static readonly Vector4 ActionBuff    = new(0.290f, 0.722f, 1.000f, 1f); // #4AB8FF accent blue
    public static readonly Vector4 ActionSpecial = Durability;

    // ── Condition colors ──────────────────────────────────────────────────────
    public static readonly Vector4 ConditionNormal    = new(0.78f, 0.78f, 0.78f, 1f);
    public static readonly Vector4 ConditionGood      = new(1.00f, 0.72f, 0.29f, 1f);
    public static readonly Vector4 ConditionExcellent = new(1.00f, 0.42f, 0.54f, 1f);
    public static readonly Vector4 ConditionPoor      = new(0.54f, 0.60f, 0.73f, 1f);
    public static readonly Vector4 ConditionPliant    = new(0.29f, 0.72f, 1.00f, 1f);
    public static readonly Vector4 ConditionMalleable = new(0.69f, 0.48f, 1.00f, 1f);
    public static readonly Vector4 ConditionSturdy    = new(0.32f, 0.90f, 0.63f, 1f);
    public static readonly Vector4 ConditionPrimed    = new(1.00f, 0.55f, 0.25f, 1f);

    // ── Badge tints ───────────────────────────────────────────────────────────
    public static readonly Vector4 SpecialistGold = new(0.99f, 0.97f, 0.62f, 1f);

    // ── Cosmic Exploration ────────────────────────────────────────────────────
    /// <summary>Research type currently being collected (active stage).</summary>
    public static readonly Vector4 CosmicActive   = new(0.69f, 0.48f, 1.00f, 1f); // #B07AFF violet
    /// <summary>Research type fully collected (upgrade threshold met).</summary>
    public static readonly Vector4 CosmicComplete = new(0.33f, 0.72f, 0.33f, 1f); // #55B855 green
    /// <summary>Research type not yet unlocked.</summary>
    public static readonly Vector4 CosmicLocked   = new(0.22f, 0.22f, 0.28f, 1f); // #383847 dim
    /// <summary>Stellar Mission active indicator.</summary>
    public static readonly Vector4 CosmicMission  = new(0.94f, 0.72f, 0.19f, 1f); // #F0B830 amber
    /// <summary>Upgrade threshold marker line on the research bar.</summary>
    public static readonly Vector4 CosmicUpgrade  = new(0.60f, 0.45f, 0.90f, 1f); // #9973E6 light violet
    /// <summary>Background tint for a recently-updated research row (10 s highlight).</summary>
    public static readonly Vector4 CosmicChanged  = new(0.94f, 0.72f, 0.19f, 0.12f); // amber, very transparent
    /// <summary>Research type at absolute maximum XP cap (current >= max).</summary>
    public static readonly Vector4 CosmicMaxed    = new(1.00f, 0.85f, 0.40f, 1f);    // #FFD966 gold

    // ── Semantic UI states ────────────────────────────────────────────────────
    /// <summary>Tint applied to disabled toggle-image buttons.</summary>
    public static readonly Vector4 Disabled = new(0.5f, 0.5f, 0.5f, 0.75f);
    /// <summary>Soft teal-green — stat/value is sufficient.</summary>
    public static readonly Vector4 Good     = Progress;
    /// <summary>Soft rose-red — stat/value is insufficient or action is invalid.</summary>
    public static readonly Vector4 Bad      = new(1.000f, 0.361f, 0.431f, 1f); // #FF5C6E
    /// <summary>Accent blue used for hyperlinks / support text.</summary>
    public static readonly Vector4 Link     = new(0.290f, 0.722f, 1.000f, 1f); // #4AB8FF
    /// <summary>Muted text color for secondary labels and subordinate values.</summary>
    public static readonly Vector4 TextMuted = new(0.314f, 0.376f, 0.478f, 1f); // #50607A

    // ── Feature Hub ───────────────────────────────────────────────────────────
    /// <summary>Cor do ícone anchor quando o hub está ancorado.</summary>
    public static readonly Vector4 FeatureHubAnchored = new(0.23f, 0.62f, 0.91f, 1f); // #3B9EE8 blue
    /// <summary>Cor do ícone anchor quando o hub está livre (não ancorado).</summary>
    public static readonly Vector4 FeatureHubFree     = new(0.35f, 0.35f, 0.35f, 1f); // #595959 grey
    /// <summary>Cor do ícone hammer (minimize) no hub.</summary>
    public static readonly Vector4 FeatureHubGold     = new(0.78f, 0.66f, 0.43f, 1f); // #C7A86E gold

    private static Vector4 SolverProgressBg => ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight));
    private static Vector4 SolverProgressFgBland => new(0.78f, 0.78f, 0.78f, 1f);

    private static readonly Vector4[] SolverProgressFgColorful =
    [
        new(0.87f, 0.19f, 0.30f, 1f),
        new(0.96f, 0.62f, 0.12f, 1f),
        new(0.97f, 0.84f, 0.00f, 1f),
        new(0.37f, 0.69f, 0.35f, 1f),
        new(0.21f, 0.30f, 0.98f, 1f),
        new(0.26f, 0.62f, 0.94f, 1f),
        new(0.70f, 0.49f, 0.88f, 1f),
    ];

    private static readonly Vector4[] SolverProgressFgMonochromatic =
    [
        new(0.33f, 0.33f, 0.33f, 1f),
        new(0.44f, 0.44f, 0.44f, 1f),
        new(0.56f, 0.56f, 0.56f, 1f),
        new(0.68f, 0.68f, 0.68f, 1f),
        new(0.81f, 0.81f, 0.81f, 1f),
        new(0.93f, 0.93f, 0.93f, 1f),
    ];

    public static readonly Vector4[] CollectabilityThreshold =
    [
        new(0.47f, 0.78f, 0.93f, 1f), // Blue
        new(0.99f, 0.79f, 0f, 1f), // Yellow
        new(0.75f, 1f, 0.75f, 1f), // Green
    ];

    public static (Vector4 Background, Vector4 Foreground) GetSolverProgressColors(int? stageValue, ProgressBarType progressType)
    {
        var fg = progressType switch
        {
            ProgressBarType.Colorful => SolverProgressFgColorful,
            ProgressBarType.Simple => SolverProgressFgMonochromatic,
            _ => throw new InvalidOperationException("No progress bar should be visible")
        };

        if (stageValue is not { } stage)
            return (SolverProgressBg, SolverProgressFgBland);

        if (stage == 0)
            return (SolverProgressBg, fg[0]);

        return (fg[(stage - 1) % fg.Length], fg[stage % fg.Length]);
    }
}
