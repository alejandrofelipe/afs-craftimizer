using Craftimizer.Utils;
using ImGuiNET;

namespace Craftimizer.UIStudio.Stories;

internal sealed class FeatureHubWindowStory : IStory
{
    public string Category => "Pages";
    public string Name     => "FeatureHubWindow";

    private static readonly string[] Estados =
    [
        "Botão", "Popup aberto",
    ];

    private int  _estado;
    private bool _listaDesabilitada;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextWrapped(
                "(FeatureHubWindow é uma janela flutuante no canto inferior direito do jogo)");

        ImGui.Spacing();

        // Botão de ícone (launcher flutuante).
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Boxes);
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Ferramentas Craftimizer");

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
