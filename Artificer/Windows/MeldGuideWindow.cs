using Artificer.Application.MeldGuide;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PluginClass = Artificer.Plugin.Plugin;

namespace Artificer.Windows;

/// <summary>
/// Melding guide window: lets the player pick a gear tier and compare it against
/// currently-equipped melds, piece by piece.
/// </summary>
public sealed class MeldGuideWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;

    public MeldGuideWindow(PluginClass plugin) : base("Meld Guide###Artificer-meldguide")
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
    }

    public void OpenAndFocus() { IsOpen = true; BringToFront(); }

    public override void PreDraw() => Theme.Push();
    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    private MeldTier CurrentTier()
    {
        var id = _plugin.Configuration.MeldGuideSelectedTier;
        return MeldGuideData.Tiers.FirstOrDefault(t => t.Id == id) ?? MeldGuideData.Tiers[0];
    }

    public override void Draw()
    {
        var tier = CurrentTier();

        // ── Seletor de tier (persistido) ─────────────────────────────────────────
        foreach (var t in MeldGuideData.Tiers)
        {
            using (ImRaii.PushId(t.Id))
                if (ImGui.RadioButton(t.Name, t.Id == tier.Id) && t.Id != tier.Id)
                {
                    _plugin.Configuration.MeldGuideSelectedTier = t.Id;
                    _plugin.Configuration.Save();
                }
            ImGui.SameLine();
        }
        ImGui.NewLine();

        ImGui.Separator();

        var equipped = EquippedMelds.Read();
        if (equipped.Count == 0)
        {
            ImGuiUtils.DrawEmptyState((int)FontAwesomeIcon.Hammer, "Nenhuma gear equipada",
                "Equipe uma ferramenta/gear de crafter para comparar.");
            return;
        }

        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted($"{tier.Name} — CMS {tier.Craftsmanship} · Ctrl {tier.Control} · CP {tier.Cp}");
        ImGui.Spacing();

        // ── Comparação por peça ──────────────────────────────────────────────
        var missingGuaranteed = new Dictionary<(CraftStat Stat, int Grade), int>();
        var missingOvermeld   = new Dictionary<(CraftStat Stat, int Grade), int>();

        foreach (var piece in tier.Pieces)
        {
            equipped.TryGetValue(piece.Slot, out var eq);
            var current = eq.Melds ?? [];
            var matches = MeldCompare.CompareMelds(
                piece.Target.Select(t => (t.Stat, t.Grade)).ToList(),
                piece.GuaranteedCount,
                current.ToList());

            using var _id = ImRaii.PushId((int)piece.Slot);

            if (piece.RecommendedItemId != 0)
            {
                PluginImGuiUtils.DrawItemIcon(piece.RecommendedItemId, ImGui.GetFrameHeight());
                ImGui.SameLine();
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(SlotLabel(piece.Slot));

            if (piece.RecommendedItemId != 0 && eq.ItemId != 0 && eq.ItemId != piece.RecommendedItemId)
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

        // ── Resumo: o que ainda falta ────────────────────────────────────────
        if (missingGuaranteed.Count > 0 || missingOvermeld.Count > 0)
        {
            ImGuiUtils.DrawSectionHeader("Ainda falta");
            DrawMissingGroup(missingGuaranteed, isOvermeld: false);
            DrawMissingGroup(missingOvermeld, isOvermeld: true);
        }

        // ── Link do Teamcraft ─────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(tier.TeamcraftUrl))
        {
            ImGui.Separator();
            if (ImGui.Button("Open in Teamcraft"))
                Util.OpenLink(tier.TeamcraftUrl);
        }
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

    private static string SlotLabel(EquipSlot slot) => slot switch
    {
        EquipSlot.MainHand => "Main Hand",
        EquipSlot.OffHand  => "Off Hand",
        EquipSlot.Head     => "Head",
        EquipSlot.Body     => "Body",
        EquipSlot.Hands    => "Hands",
        EquipSlot.Legs     => "Legs",
        EquipSlot.Feet     => "Feet",
        EquipSlot.Ears     => "Ears",
        EquipSlot.Neck     => "Neck",
        EquipSlot.Wrists   => "Wrists",
        EquipSlot.Ring1    => "Ring 1",
        EquipSlot.Ring2    => "Ring 2",
        _                  => slot.ToString(),
    };

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

    public void Dispose() => _plugin.WindowSystem.RemoveWindow(this);
}
