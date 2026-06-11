using Artificer.Utils;
using ImGuiNET;
using System;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class ChartsStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "Charts";

    private float _fraction = 0.65f;

    public void Draw()
    {
        // ── DrawStatArc — tamanhos ────────────────────────────────────────────
        Section("DrawStatArc — tamanhos");
        var dl = ImGui.GetWindowDrawList();

        float[] sizes = [32, 48, 64, 80, 100, 128];
        foreach (var sz in sizes)
        {
            ImGui.SameLine(0, 16);
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(sz, sz));
            ImGuiUtils.DrawStatArc(dl, pos, sz, _fraction, Colors.Progress);
        }

        // ── DrawStatArc — frações ─────────────────────────────────────────────
        Section("DrawStatArc — frações");
        for (int pct = 0; pct <= 100; pct += 10)
        {
            if (pct > 0) ImGui.SameLine(0, 8);
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(56, 56));
            ImGuiUtils.DrawStatArc(dl, pos, 56, pct / 100f, Colors.Quality);

            var label = $"{pct}%";
            var lsz = ImGui.CalcTextSize(label);
            ImGui.GetWindowDrawList().AddText(
                pos + new Vector2((56 - lsz.X) * 0.5f, (56 - lsz.Y) * 0.5f),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), label);
        }

        // ── DrawStatArc — cores ───────────────────────────────────────────────
        Section("DrawStatArc — cores de stat");
        (string Name, Vector4 Color)[] stats =
        [
            ("Progress",    Colors.Progress),
            ("Quality",     Colors.Quality),
            ("Durability",  Colors.Durability),
            ("CP",          Colors.CP),
            ("ActionBuff",  Colors.ActionBuff),
            ("Good",        Colors.Good),
            ("Bad",         Colors.Bad),
        ];
        foreach (var (name, color) in stats)
        {
            ImGui.SameLine(0, 16);
            ImGui.BeginGroup();
            var pos2 = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(64, 64));
            ImGuiUtils.DrawStatArc(dl, pos2, 64, _fraction, color);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (64 - ImGui.CalcTextSize(name).X) * 0.5f);
            ImGui.TextDisabled(name);
            ImGui.EndGroup();
        }

        // ── Arcos múltiplos — simulação de stat rings ─────────────────────────
        Section("Simulação de stat rings (Progress / Quality / Durability / CP)");
        float[] fracs = [_fraction, _fraction * 0.8f, _fraction * 1.1f, _fraction * 0.5f];
        Vector4[] ringColors = [Colors.Progress, Colors.Quality, Colors.Durability, Colors.CP];
        string[] ringNames   = ["Progress", "Quality", "Durability", "CP"];

        const float RingStep = 20f;
        const float RingBase = 110f;

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(RingBase + RingStep * ringColors.Length * 2 + 20, RingBase));

        for (int i = 0; i < ringColors.Length; i++)
        {
            var offset = RingStep * i;
            var innerPos = origin + new Vector2(offset, offset);
            var innerSize = RingBase - offset * 2;
            if (innerSize < 16) break;
            ImGuiUtils.DrawStatArc(dl, innerPos, innerSize, fracs[i], ringColors[i]);
        }

        // ── Interativo ────────────────────────────────────────────────────────
        Section("Interativo");
        ImGui.SetNextItemWidth(300);
        ImGui.SliderFloat("##frac", ref _fraction, 0f, 1f, "%.2f");
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled("atualiza todos os arcos acima");
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
