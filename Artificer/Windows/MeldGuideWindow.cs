using Artificer.Application.MeldGuide;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using PluginClass = Artificer.Plugin.Plugin;

namespace Artificer.Windows;

/// <summary>
/// Melding guide window: lets the player pick a gear tier and (in a follow-up task)
/// compare it against currently-equipped melds. For now, just the tier selector
/// with persisted selection.
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

        // (Task 5 preenche o corpo: comparação por peça/slot + resumo)
        ImGui.TextUnformatted($"{tier.Name} — CMS {tier.Craftsmanship} · Ctrl {tier.Control} · CP {tier.Cp}");
    }

    public void Dispose() => _plugin.WindowSystem.RemoveWindow(this);
}
