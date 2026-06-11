using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class CosmicTrackerStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CosmicTracker";

    private static readonly string[] Estados = ["Sem dados", "Com dados"];

    private static readonly string[] TypeLabels =
        ["Type I", "Type II", "Type III", "Type IV", "Type V", "Type VI", "Type VII"];

    private readonly record struct TypeData(
        int Current, int Needed, int Max, ImGuiUtils.ResearchTypeState State);

    // Mock data — current / needed / max + state.
    private static readonly TypeData[] Types =
    [
        new(7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Maxed),    // Type I
        new(4200, 5000, 7500, ImGuiUtils.ResearchTypeState.Active),   // Type II
        new(7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Complete), // Type III
        new(   0, 5000, 7500, ImGuiUtils.ResearchTypeState.Locked),   // Type IV
        new(3100, 5000, 7500, ImGuiUtils.ResearchTypeState.Active),   // Type V
        new(7500, 5000, 7500, ImGuiUtils.ResearchTypeState.Maxed),    // Type VI
        new(1000, 5000, 7500, ImGuiUtils.ResearchTypeState.Active),   // Type VII
    ];

    private int  _estado;
    private bool _minimized;
    private bool _hideComplete;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawOverlay();
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
        ImGui.SameLine(0, 16);
        ImGui.Checkbox("Modo minimizado", ref _minimized);
        ImGui.SameLine(0, 16);
        ImGui.Checkbox("Ocultar concluídos", ref _hideComplete);
    }

    // ── Overlay principal ─────────────────────────────────────────────────────
    private void DrawOverlay()
    {
        var title = _estado == 1 ? "Cosmic Tool — Stage 3/4" : "Cosmic Tool";

        using (ImRaii2.GroupPanel(title, 320f, out _))
        {
            if (_estado == 0) // Sem dados
            {
                ImGuiUtils.DrawEmptyState(
                    FontAwesomeIcon.Star,
                    "Fora da Cosmic Exploration Zone",
                    "Entre em qualquer mapa de Cosmic Exploration para ver o progresso de pesquisa.");
                return;
            }

            var barWidth = 280f * UiServices.Current.GlobalScale;
            var mode = _minimized
                ? ImGuiUtils.ResearchTypeRowMode.Minimized
                : ImGuiUtils.ResearchTypeRowMode.Full;

            var anyRendered = false;
            for (var t = 0; t < Types.Length; t++)
            {
                var td = Types[t];
                if (_hideComplete && td.State is ImGuiUtils.ResearchTypeState.Complete
                                              or ImGuiUtils.ResearchTypeState.Maxed)
                    continue;

                anyRendered = true;
                ImGuiUtils.DrawResearchTypeRow(
                    TypeLabels[t], td.Current, td.Needed, td.Max, td.State, barWidth, mode);
            }

            if (!anyRendered)
                ImGuiUtils.DrawEmptyState(
                    FontAwesomeIcon.CheckCircle,
                    "Tudo concluído",
                    "Todos os tipos de pesquisa foram completados.\nDesative o filtro para visualizá-los.");
        }
    }
}
