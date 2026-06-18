using Artificer.Utils;
using ImGuiNET;

namespace Artificer.UIStudio.Stories;

internal sealed class FeatureHubStory : IStory
{
    public string Category => "Pages";
    public string Name     => "FeatureHub";

    private static readonly string[] Estados =
    [
        "Botão", "Popup aberto",
    ];

    private int  _estado;
    private bool _listaDesabilitada;
    private bool _anchored = true;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextWrapped(
                "(FeatureHub é uma janela flutuante no canto inferior direito do jogo)");

        ImGui.Spacing();

        // Âncora — cor varia conforme estado anchored/free
        using (ImRaii.PushColor(ImGuiCol.Text,
            _anchored ? Colors.FeatureHubAnchored : Colors.FeatureHubFree))
            ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Thumbtack);
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip(_anchored ? "Ancorado ao NaviMap" : "Flutuante (livre)");
        ImGui.SameLine(0, 4);

        // Botão de ícone (launcher flutuante).
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Boxes);
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Ferramentas Artificer");

        if (_estado != 1)
            return;

        // Popup simulado ao lado do botão.
        ImGui.SameLine();
        DrawPopup();
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
        ImGui.Checkbox("Lista de Coleta desabilitada", ref _listaDesabilitada);
        ImGui.SameLine(0, 16);
        ImGui.Checkbox("Ancorado", ref _anchored);
    }

    // ── Popup simulado ────────────────────────────────────────────────────────
    private void DrawPopup()
    {
        using (ImRaii2.GroupPanel("Popup", 220f, out _))
        {
            ImGui.BeginDisabled(_listaDesabilitada);
            ImGui.MenuItem("Lista de Coleta");
            ImGui.EndDisabled();

            if (_listaDesabilitada &&
                ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGuiUtils.Tooltip("Lista de Coleta está desabilitada nas configurações.");

            ImGui.MenuItem("Configurações");
        }
    }
}
