using Craftimizer.Utils;
using ImGuiNET;
using System;
using System.Numerics;

namespace Craftimizer.UIStudio.Stories;

internal sealed class ColorsStory : IStory
{
    public string Category => "Atoms";
    public string Name     => "Colors";

    private static readonly (string Label, Vector4 Color)[] Swatches =
    [
        // Stat bars
        ("Progress",       Colors.Progress),
        ("Quality",        Colors.Quality),
        ("Durability",     Colors.Durability),
        ("CP",             Colors.CP),
        ("HQ",             Colors.HQ),
        ("Collectability", Colors.Collectability),

        // Actions
        ("ActionSynth",   Colors.ActionSynth),
        ("ActionTouch",   Colors.ActionTouch),
        ("ActionBuff",    Colors.ActionBuff),
        ("ActionSpecial", Colors.ActionSpecial),

        // Conditions
        ("ConditionNormal",    Colors.ConditionNormal),
        ("ConditionGood",      Colors.ConditionGood),
        ("ConditionExcellent", Colors.ConditionExcellent),
        ("ConditionPoor",      Colors.ConditionPoor),
        ("ConditionPliant",    Colors.ConditionPliant),
        ("ConditionMalleable", Colors.ConditionMalleable),
        ("ConditionSturdy",    Colors.ConditionSturdy),
        ("ConditionPrimed",    Colors.ConditionPrimed),

        // Cosmic
        ("CosmicActive",   Colors.CosmicActive),
        ("CosmicComplete", Colors.CosmicComplete),
        ("CosmicLocked",   Colors.CosmicLocked),
        ("CosmicMission",  Colors.CosmicMission),
        ("CosmicUpgrade",  Colors.CosmicUpgrade),
        ("CosmicMaxed",    Colors.CosmicMaxed),

        // Semantic
        ("Good",      Colors.Good),
        ("Bad",       Colors.Bad),
        ("Link",      Colors.Link),
        ("Disabled",  Colors.Disabled),
        ("TextMuted", Colors.TextMuted),
        ("SpecialistGold", Colors.SpecialistGold),
    ];

    public void Draw()
    {
        var dl      = ImGui.GetWindowDrawList();
        var swSize  = new Vector2(36, 36);
        var padding = new Vector2(8, 6);
        var itemW   = swSize.X + padding.X + 160f;
        var cols    = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / itemW));

        for (int i = 0; i < Swatches.Length; i++)
        {
            if (i % cols != 0) ImGui.SameLine(0, 16);

            var (label, color) = Swatches[i];

            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(itemW, swSize.Y));

            // colored square
            dl.AddRectFilled(pos, pos + swSize, ImGui.ColorConvertFloat4ToU32(color), 4f);
            dl.AddRect(pos, pos + swSize, ImGui.GetColorU32(ImGuiCol.Border), 4f, ImDrawFlags.None, 1f);

            // label + hex
            var textX = pos.X + swSize.X + padding.X;
            dl.AddText(new Vector2(textX, pos.Y + 2),
                ImGui.GetColorU32(ImGuiCol.Text), label);
            dl.AddText(new Vector2(textX, pos.Y + 2 + ImGui.GetTextLineHeight()),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), ToHex(color));
        }
    }

    private static string ToHex(Vector4 c) =>
        $"#{(int)(c.X * 255):X2}{(int)(c.Y * 255):X2}{(int)(c.Z * 255):X2}";
}
