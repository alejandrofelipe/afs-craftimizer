using Artificer.Utils;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

/// <summary>
/// Mock do /meldguide: compara melds recomendados (por tier) vs. equipados atualmente, por peça.
/// MeldGuideData/EquippedMelds/EquipSlot são do projeto plugin (não disponíveis no UIStudio) —
/// esta story usa dados mock locais, mas exercita a lógica real de MeldCompare.CompareMelds.
/// </summary>
internal sealed class MeldGuideStory : IStory
{
    public string Category => "Pages";
    public string Name     => "MeldGuide";

    private static readonly string[] TierNames = ["High", "Mid", "Budget"];
    private static readonly (int Cms, int Ctrl, int Cp)[] TierStats =
    [
        (4700, 4800, 690),
        (4300, 4400, 620),
        (3900, 4000, 560),
    ];

    private int _tier;

    // Mock de uma peça: target recomendado (guaranteed-first) vs. melds atualmente equipados.
    private sealed record MockPiece(
        string Slot,
        IReadOnlyList<(CraftStat Stat, int Grade)> Target,
        int GuaranteedCount,
        IReadOnlyList<(CraftStat Stat, int Grade)> Current,
        bool ItemDiffers = false);

    private static readonly MockPiece[] Pieces =
    [
        // Head — completa: todos os melds (guaranteed + overmeld) satisfeitos.
        new("Head",
            Target:  [(CraftStat.Control, 12), (CraftStat.CP, 12), (CraftStat.Control, 10), (CraftStat.CP, 10), (CraftStat.Control, 8)],
            GuaranteedCount: 2,
            Current: [(CraftStat.Control, 12), (CraftStat.CP, 12), (CraftStat.Control, 10), (CraftStat.CP, 10), (CraftStat.Control, 8)]),

        // Body — falta um guaranteed (Control 12 não coberto; só há grade 10 equipado).
        new("Body",
            Target:  [(CraftStat.Control, 12), (CraftStat.CP, 12)],
            GuaranteedCount: 2,
            Current: [(CraftStat.Control, 10), (CraftStat.CP, 12)]),

        // Ears — guaranteed ok, falta um overmeld (só 2 dos 3 slots melded).
        new("Ears",
            Target:  [(CraftStat.CP, 12), (CraftStat.CP, 10), (CraftStat.CP, 8)],
            GuaranteedCount: 1,
            Current: [(CraftStat.CP, 12), (CraftStat.CP, 10)]),

        // Main Hand — melds completos, mas o item equipado difere do recomendado.
        new("Main Hand",
            Target:  [(CraftStat.CP, 12), (CraftStat.CP, 12)],
            GuaranteedCount: 2,
            Current: [(CraftStat.CP, 12), (CraftStat.CP, 12)],
            ItemDiffers: true),
    ];

    public void Draw()
    {
        // ── Seletor de tier (mock — visual only, não recarrega peças) ────────────
        for (var i = 0; i < TierNames.Length; i++)
        {
            using (ImRaii.PushId(i))
                ImGui.RadioButton(TierNames[i], ref _tier, i);
            ImGui.SameLine();
        }
        ImGui.NewLine();

        ImGui.Separator();

        var (cms, ctrl, cp) = TierStats[_tier];
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted($"{TierNames[_tier]} (mock) — CMS {cms} · Ctrl {ctrl} · CP {cp}");
        ImGui.Spacing();

        // ── Comparação por peça ───────────────────────────────────────────────
        var missingGuaranteed = new Dictionary<(CraftStat Stat, int Grade), int>();
        var missingOvermeld   = new Dictionary<(CraftStat Stat, int Grade), int>();

        foreach (var piece in Pieces)
        {
            using var _id = ImRaii.PushId(piece.Slot);

            var matches = MeldCompare.CompareMelds(piece.Target, piece.GuaranteedCount, piece.Current);

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(piece.Slot);

            if (piece.ItemDiffers)
            {
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                    ImGui.TextUnformatted("⚠");
                ImGuiUtils.HoveredTooltip("Item difere do recomendado");
            }

            var missCount = 0;
            foreach (var m in matches)
            {
                ImGui.SameLine();
                DrawMeldChip(m);
                if (!m.Satisfied)
                {
                    missCount++;
                    var dict = m.IsOvermeld ? missingOvermeld : missingGuaranteed;
                    var key  = (m.Stat, m.Grade);
                    dict[key] = dict.GetValueOrDefault(key) + 1;
                }
            }

            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, missCount == 0 ? Colors.Good : Colors.Bad))
                ImGui.TextUnformatted(missCount == 0 ? "✓" : $"falta {missCount}");
        }

        // ── Resumo: o que ainda falta ─────────────────────────────────────────
        if (missingGuaranteed.Count > 0 || missingOvermeld.Count > 0)
        {
            ImGuiUtils.DrawSectionHeader("Ainda falta");
            DrawMissingGroup(missingGuaranteed, isOvermeld: false);
            DrawMissingGroup(missingOvermeld, isOvermeld: true);
        }

        // ── Link do Teamcraft (no-op na story) ───────────────────────────────
        ImGui.Separator();
        if (ImGui.Button("Open in Teamcraft"))
            _ = 0;
    }

    private static void DrawMissingGroup(Dictionary<(CraftStat Stat, int Grade), int> missing, bool isOvermeld)
    {
        foreach (var (key, count) in missing.OrderByDescending(kv => kv.Key.Grade).ThenBy(kv => kv.Key.Stat))
            using (ImRaii.PushColor(ImGuiCol.Text, StatColor(key.Stat)))
                ImGui.TextUnformatted($"{StatAbbrev(key.Stat)} {key.Grade}{(isOvermeld ? "°" : "")} ×{count}");
    }

    private static void DrawMeldChip(MeldMatch m)
    {
        var color  = m.Satisfied ? StatColor(m.Stat) : Colors.TextMuted;
        var mark   = m.Satisfied ? "✓" : "✗";
        var suffix = m.IsOvermeld ? "°" : "";
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGui.TextUnformatted($"{mark}{StatAbbrev(m.Stat)}{m.Grade}{suffix}");
    }

    private static Vector4 StatColor(CraftStat stat) => stat switch
    {
        CraftStat.Craftsmanship => Colors.Progress,
        CraftStat.Control       => Colors.Quality,
        CraftStat.CP            => Colors.CP,
        _                       => Colors.TextMuted,
    };

    private static string StatAbbrev(CraftStat stat) => stat switch
    {
        CraftStat.Craftsmanship => "CMS",
        CraftStat.Control       => "Ctrl",
        CraftStat.CP            => "CP",
        _                       => stat.ToString(),
    };
}
