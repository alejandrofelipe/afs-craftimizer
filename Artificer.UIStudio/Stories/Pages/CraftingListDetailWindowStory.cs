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
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Equipamento Dawntrail");

        // toggle segmentado mock, à direita
        var toggleW = ImGui.CalcTextSize("Detalhada").X + ImGui.CalcTextSize("Simples").X
                      + ImGui.GetStyle().FramePadding.X * 4 + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine();
        ImGuiUtils.AlignRight(toggleW);
        Theme.PushPrimaryButton();
        ImGui.Button("Detalhada");
        Theme.PopPrimaryButton();
        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X);
        ImGui.Button("Simples");

        // linha 2: barra de progresso + %
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, Colors.Quality))
            ImGui.ProgressBar(0.72f, new Vector2(-1, 8f), string.Empty);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted("72%");
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
        ImGui.BeginChild("##ingredients", new Vector2(0, 260), ImGuiChildFlags.Border);

        for (var i = 0; i < Ingredients.Length; i++)
        {
            var (name, need, have, collected) = Ingredients[i];
            var isComplete = allCollected || collected;
            var collectedQty = isComplete ? need : have;
            var missing = System.Math.Max(0, need - collectedQty);
            var fraction = need > 0 ? System.Math.Clamp((float)collectedQty / need, 0f, 1f) : 1f;

            using (ImRaii.PushId(i))
            using (ImRaii.Group())
            {
                // placeholder de ícone (no plugin real: PluginImGuiUtils.DrawItemIcon)
                var iconSize = ImGui.GetFrameHeight();
                var p = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(
                    p, new Vector2(p.X + iconSize, p.Y + iconSize),
                    ImGui.GetColorU32(Colors.TextMuted), 4f);
                ImGui.Dummy(new Vector2(iconSize));

                ImGui.SameLine(0, 6f);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(name);
                ImGui.SameLine(0, 8f);
                if (missing > 0)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, collectedQty == 0 ? Colors.Bad : Colors.TextMuted))
                        ImGui.TextUnformatted($"{collectedQty}/{need}");
                    ImGui.SameLine(0, 6f);
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                        ImGui.TextUnformatted($"faltam {missing}");
                }
                else
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.Progress))
                        ImGui.TextUnformatted($"{collectedQty}/{need} ✓");
                }

                using (ImRaii.PushColor(ImGuiCol.PlotHistogram, fraction >= 1f ? Colors.Progress : Colors.Quality))
                    ImGui.ProgressBar(fraction, new Vector2(-1, 6f), string.Empty);
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
