using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class CraftingListWindowStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CraftingListWindow";

    private static readonly string[] Estados =
    [
        "Vazia", "Com listas", "Busca ativa", "Confirmação de delete",
    ];

    private static readonly string[] Sorts =
    [
        "MostRecent", "NameAZ", "PercentComplete",
    ];

    // Mock de listas de coleta: (nome, % completo, data).
    private static readonly (string Name, int Percent, string Date)[] Lists =
    [
        ("Equipamento Dawntrail",      72,  "2026-06-09"),
        ("Materiais Housing",          15,  "2026-06-07"),
        ("Receitas Cosmic Research",   100, "2026-06-05"),
        ("Batch Crafting Semanal",     45,  "2026-06-04"),
        ("Lista de Teste",             0,   "2026-06-01"),
    ];

    private const int DeleteConfirmRow = 2;

    private int _estado;
    private int _sort;
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

        DrawSearchRow();
        ImGui.Spacing();

        DrawList(260f);

        ImGui.Spacing();
        DrawFooter();
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Sort", ref _sort, Sorts, Sorts.Length);
    }

    // ── Empty state ───────────────────────────────────────────────────────────
    private static void DrawEmptyWindow()
    {
        using (ImRaii2.GroupPanel("Listas de Coleta", -1, out _))
        {
            ImGui.BeginChild("##empty", new Vector2(0, 240), ImGuiChildFlags.None);
            ImGuiUtils.DrawEmptyState(
                FontAwesomeIcon.List,
                "Nenhuma lista de coleta",
                "Crie uma lista para agrupar receitas e acompanhar o progresso.",
                ("+ Nova Lista", () => { }));
            ImGui.EndChild();
        }
    }

    // ── Campo de busca + botão Nova ───────────────────────────────────────────
    private void DrawSearchRow()
    {
        var query = _estado == 2 ? "cosmic" : string.Empty;

        const float BtnW = 100f;
        var searchW = ImGui.GetContentRegionAvail().X - BtnW - ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(searchW);
        ImGui.InputTextWithHint(
            "##search", "Buscar listas...", ref query, 64,
            ImGuiInputTextFlags.ReadOnly);

        ImGui.SameLine();
        Theme.PushPrimaryButton();
        ImGui.Button("+ Nova", new Vector2(BtnW, 0));
        Theme.PopPrimaryButton();
    }

    // ── Lista de listas ───────────────────────────────────────────────────────
    private void DrawList(float height)
    {
        ImGui.BeginChild("##list", new Vector2(0, height), ImGuiChildFlags.Border);

        for (var i = 0; i < Lists.Length; i++)
        {
            var (name, percent, date) = Lists[i];

            using (ImRaii.PushId(i))
            {
                var rowH = ImGui.GetTextLineHeightWithSpacing() * 1.6f;
                if (ImGui.Selectable($"##sel{i}", _selected == i, ImGuiSelectableFlags.None, new Vector2(0, rowH)))
                    _selected = i;

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var dl  = ImGui.GetWindowDrawList();
                var pad = ImGui.GetStyle().ItemInnerSpacing.X;

                // Nome da lista.
                var namePos = new Vector2(min.X + pad, min.Y + 4f);
                dl.AddText(namePos, ImGui.GetColorU32(ImGuiCol.Text), name);

                // Data + contagem de receitas (linha de baixo).
                var datePos = new Vector2(min.X + pad, max.Y - ImGui.GetTextLineHeight() - 4f);
                dl.AddText(datePos, ImGui.GetColorU32(Colors.CosmicActive), date);
                var metaText = $"  ·  {3 + i} receitas";
                var dateW = ImGui.CalcTextSize(date).X;
                dl.AddText(new Vector2(datePos.X + dateW, datePos.Y),
                    ImGui.GetColorU32(Colors.TextMuted), metaText);

                // % completo alinhado à direita.
                var pctText = $"{percent}%";
                var pctSize = ImGui.CalcTextSize(pctText);
                var pctCol  = percent >= 100 ? Colors.HQ : percent > 0 ? Colors.Progress : Colors.Bad;
                var pctPos  = new Vector2(max.X - pad - pctSize.X, (min.Y + max.Y - pctSize.Y) * 0.5f);
                dl.AddText(pctPos, ImGui.GetColorU32(pctCol), pctText);

                // Confirmação de delete inline na 3ª linha (índice 2).
                if (_estado == 3 && i == DeleteConfirmRow)
                    DrawInlineDeleteConfirm();
            }
        }

        ImGui.EndChild();
    }

    private static void DrawInlineDeleteConfirm()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
            ImGui.TextUnformatted("Remover esta lista?");
        ImGui.SameLine();
        Theme.PushDangerButton();
        ImGui.Button("Confirmar");
        Theme.PopDangerButton();
    }

    // ── Rodapé — contagem ─────────────────────────────────────────────────────
    private static void DrawFooter()
    {
        ImGui.TextDisabled($"{Lists.Length} listas de coleta");
    }
}
