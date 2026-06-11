using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class MacroListStory : IStory
{
    public string Category => "Pages";
    public string Name     => "MacroList";

    private static readonly string[] Estados =
    [
        "Vazia", "Com macros", "Busca ativa", "Busca sem resultado",
    ];

    // Mock de macros salvos: (nome, job, HQ%).
    private static readonly (string Name, string Job, int Hq)[] Macros =
    [
        ("Macro Lv100 4★ (Indústria)", "CRP", 92),
        ("Macro Collectible Universal",  "BSM", 100),
        ("Macro Expert Dawntrail",       "ARM", 87),
        ("Macro Rápido 2★",             "GSM", 95),
        ("Macro Cosmic Research",        "LTW", 88),
        ("Macro Synthesis Helper",       "WVR", 91),
    ];

    private int _estado;
    private int _selected;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_estado == 0) // Vazia
        {
            DrawEmptyWindow();
            return;
        }

        DrawSearchField();
        ImGui.Spacing();

        var avail = ImGui.GetContentRegionAvail();
        var childH = avail.Y - ImGui.GetFrameHeightWithSpacing() - ImGui.GetTextLineHeightWithSpacing();
        if (childH < 120f)
            childH = 120f;

        if (_estado == 3) // Busca sem resultado
        {
            ImGui.BeginChild("##list", new Vector2(0, childH), ImGuiChildFlags.Border);
            ImGuiUtils.DrawEmptyState(
                FontAwesomeIcon.Search,
                "Nenhum resultado",
                "Nenhum macro corresponde à busca atual.");
            ImGui.EndChild();
        }
        else
        {
            DrawList(childH);
        }

        ImGui.Spacing();
        DrawFooter();
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
    }

    // ── Empty state ───────────────────────────────────────────────────────────
    private static void DrawEmptyWindow()
    {
        using (ImRaii2.GroupPanel("Macros Salvos", -1, out _))
        {
            ImGui.BeginChild("##empty", new Vector2(0, 220), ImGuiChildFlags.None);
            ImGuiUtils.DrawEmptyState(
                FontAwesomeIcon.Book,
                "Nenhum macro salvo",
                "Gere e salve macros no editor para encontrá-los aqui.");
            ImGui.EndChild();
        }
    }

    // ── Campo de busca (mock read-only) ───────────────────────────────────────
    private void DrawSearchField()
    {
        var query = _estado == 2 ? "cosmic" : string.Empty;
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint(
            "##search", "Buscar macros...", ref query, 64,
            ImGuiInputTextFlags.ReadOnly);
    }

    // ── Lista de macros ───────────────────────────────────────────────────────
    private void DrawList(float height)
    {
        ImGui.BeginChild("##list", new Vector2(0, height), ImGuiChildFlags.Border);

        for (var i = 0; i < Macros.Length; i++)
        {
            var (name, job, hq) = Macros[i];

            using (ImRaii.PushId(i))
            {
                if (ImGui.Selectable($"##sel{i}", _selected == i, ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 1.6f)))
                    _selected = i;

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var dl  = ImGui.GetWindowDrawList();
                var pad = ImGui.GetStyle().ItemInnerSpacing.X;

                // Nome do macro.
                var namePos = new Vector2(min.X + pad, min.Y + 4f);
                dl.AddText(namePos, ImGui.GetColorU32(ImGuiCol.Text), name);

                // Job abbreviation.
                var jobPos = new Vector2(min.X + pad, max.Y - ImGui.GetTextLineHeight() - 4f);
                dl.AddText(jobPos, ImGui.GetColorU32(Colors.CosmicActive), job);

                // HQ% alinhado à direita.
                var hqText = $"HQ {hq}%";
                var hqSize = ImGui.CalcTextSize(hqText);
                var hqCol  = hq >= 100 ? Colors.HQ : hq >= 90 ? Colors.Quality : Colors.Bad;
                var hqPos  = new Vector2(max.X - pad - hqSize.X, (min.Y + max.Y - hqSize.Y) * 0.5f);
                dl.AddText(hqPos, ImGui.GetColorU32(hqCol), hqText);
            }
        }

        ImGui.EndChild();
    }

    // ── Rodapé — contagem + botão ─────────────────────────────────────────────
    private void DrawFooter()
    {
        ImGui.TextDisabled($"{Macros.Length} macros salvos");
        ImGui.SameLine();

        const float BtnW = 130f;
        ImGuiUtils.AlignRight(BtnW);
        Theme.PushPrimaryButton();
        ImGui.Button("Usar Macro", new Vector2(BtnW, 0));
        Theme.PopPrimaryButton();
    }
}
