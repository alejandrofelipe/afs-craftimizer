namespace Artificer.Utils;

public static partial class ImGuiUtils
{
    /// <summary>
    /// Formats the gear durability percentage into the "repair needed" message.
    /// Used when tracking is disabled or recipe data is unavailable.
    /// </summary>
    public static string FormatGearRepairMessage(float pct) =>
        $"{pct:0}% — Repair gear before continuing!";
}
