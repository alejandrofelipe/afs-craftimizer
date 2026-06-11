using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class CraftingListAddWindowStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CraftingListAddWindow";

    private static readonly string[] Estados =
    [
        "Inicial", "Com resultados", "Sem resultados",
    ];

    // Mock de resultados de busca: (nome, categoria).
    private static readonly (string Name, string Category)[] Results =
    [
        ("Camisola de Linho +3",       "Armour"),
        ("Luvas de Couro Negro",       "Armour"),
        ("Capacete de Bronze",         "Armour"),
        ("Ração Mediana de Café",      "Food"),
        ("Poção de Força de Artesão",  "Medicine"),
        ("Adamantite Ingot",           "Material"),
    ];

    private int _estado;
    private int _selected;
    private int _quantity = 1;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawSearchInput();
        ImGui.Spacing();

        switch (_estado)
        {
            case 0: // Inicial
                DrawInitialState();
                break;
            case 1: // Com resultados
                DrawResults();
                break;
            case 2: // Sem resultados
                DrawNoResults();
                break;
        }
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
    }

    // ── Campo de busca ────────────────────────────────────────────────────────
    private void DrawSearchInput()
    {
        var query = _estado == 0 ? string.Empty : "linho";

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint(
            "##search", "Buscar receita...", ref query, 64,
            ImGuiInputTextFlags.ReadOnly);
    }

    // ── Estado inicial — sem busca ────────────────────────────────────────────
    private static void DrawInitialState()
    {
        ImGuiUtils.DrawEmptyState(
            FontAwesomeIcon.Search,
            "Busque uma receita",
            "Digite o nome de um item para encontrar receitas.");
    }

    // ── Estado sem resultados ─────────────────────────────────────────────────
    private static void DrawNoResults()
    {
        ImGui.BeginChild("##noresults", new Vector2(0, 240), ImGuiChildFlags.None);
        ImGuiUtils.DrawEmptyState(
            FontAwesomeIcon.Search,
            "Nenhum resultado",
            "Sem receitas para 'linho'.");
        ImGui.EndChild();
    }

    // ── Estado com resultados ─────────────────────────────────────────────────
    private void DrawResults()
    {
        ImGui.BeginChild("##results", new Vector2(0, 240), ImGuiChildFlags.Border);

        for (var i = 0; i < Results.Length; i++)
        {
            var (name, category) = Results[i];

            using (ImRaii.PushId(i))
            {
                var rowH = ImGui.GetTextLineHeightWithSpacing() * 1.6f;
                if (ImGui.Selectable($"##sel{i}", _selected == i, ImGuiSelectableFlags.None, new Vector2(0, rowH)))
                    _selected = i;

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var dl  = ImGui.GetWindowDrawList();
                var pad = ImGui.GetStyle().ItemInnerSpacing.X;

                // Nome do item.
                var namePos = new Vector2(min.X + pad, min.Y + 4f);
                dl.AddText(namePos, ImGui.GetColorU32(ImGuiCol.Text), name);

                // Categoria (linha de baixo).
                var catPos = new Vector2(min.X + pad, max.Y - ImGui.GetTextLineHeight() - 4f);
                dl.AddText(catPos, ImGui.GetColorU32(Colors.CosmicActive), category);
            }
        }

        ImGui.EndChild();

        ImGui.Spacing();

        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("Quantidade", ref _quantity))
            _quantity = System.Math.Max(1, _quantity);

        ImGui.Spacing();

        Theme.PushPrimaryButton();
        ImGui.Button("Adicionar à Lista", new Vector2(ImGui.GetContentRegionAvail().X, 0));
        Theme.PopPrimaryButton();
    }
}
