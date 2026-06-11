using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class CraftingListDetailWindowStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CraftingListDetailWindow";

    private static readonly string[] Estados =
    [
        "Carregando", "Com ingredientes", "Coleta concluída",
    ];

    // Mock de ingredientes: (nome, necessário, possuído, coletado).
    private static readonly (string Name, int Need, int Have, bool Collected)[] Ingredients =
    [
        ("Linho",            12,  8, false),
        ("Couro de Bronze",   6,  6, true),
        ("Fio de Algodão",    4,  0, false),
        ("Cristal de Terra", 24, 24, true),
        ("Cristal de Vento", 24, 18, false),
        ("Bronze Ingot",      2,  2, true),
    ];

    private int _estado;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawHeader();
        ImGui.Spacing();

        switch (_estado)
        {
            case 0: // Carregando
                DrawLoadingState();
                break;
            case 1: // Com ingredientes
            case 2: // Coleta concluída
                DrawIngredientList(allCollected: _estado == 2);
                ImGui.Spacing();
                DrawFooter();
                break;
        }
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
    }

    // ── Cabeçalho ─────────────────────────────────────────────────────────────
    private static void DrawHeader()
    {
        ImGui.TextUnformatted("Equipamento Dawntrail");
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted("(72% concluída)");
    }

    // ── Estado carregando ─────────────────────────────────────────────────────
    private static void DrawLoadingState()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted("Carregando dados...");
        ImGui.Spacing();
        ProgressBarComponent.DrawSimple(0, 1, ProgressBarComponent.ProgressState.InProgress);
    }

    // ── Lista de ingredientes ─────────────────────────────────────────────────
    private static void DrawIngredientList(bool allCollected)
    {
        ImGui.BeginChild("##ingredients", new Vector2(0, 220), ImGuiChildFlags.Border);

        for (var i = 0; i < Ingredients.Length; i++)
        {
            var (name, need, have, collected) = Ingredients[i];
            var isComplete = allCollected || collected;

            using (ImRaii.PushId(i))
            {
                // Ícone de status (check / times).
                var icon  = isComplete ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                var color = isComplete ? Colors.Progress : Colors.Bad;

                using (ImRaii.PushFont(UiServices.Current.IconFont))
                using (ImRaii.PushColor(ImGuiCol.Text, color))
                    ImGui.TextUnformatted(icon.ToIconString());

                ImGui.SameLine();
                ImGui.TextUnformatted(name);

                // Contagem possuído / necessário, alinhada à direita.
                var displayHave = isComplete ? need : have;
                var countText   = $"{displayHave} / {need}";

                ImGui.SameLine();
                ImGuiUtils.AlignRight(ImGui.CalcTextSize(countText).X);
                using (ImRaii.PushColor(ImGuiCol.Text, isComplete ? Colors.HQ : Colors.TextMuted))
                    ImGui.TextUnformatted(countText);
            }
        }

        ImGui.EndChild();
    }

    // ── Rodapé com ações ──────────────────────────────────────────────────────
    private static void DrawFooter()
    {
        var avail   = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var btnW    = (avail - spacing * 2f) / 3f;

        ImGui.Button("Sync Inventário", new Vector2(btnW, 0));
        ImGui.SameLine();
        ImGui.Button("Ver Mercado", new Vector2(btnW, 0));
        ImGui.SameLine();

        Theme.PushPrimaryButton();
        ImGui.Button("Exportar", new Vector2(btnW, 0));
        Theme.PopPrimaryButton();
    }
}
