using Artificer.Utils;
using ImGuiNET;

namespace Artificer.UIStudio.Stories;

internal sealed class TooltipsStory : IStory
{
    public string Category => "Atoms";
    public string Name     => "Tooltips";

    public void Draw()
    {
        Section("Tooltip direto");
        ImGui.TextDisabled("Passe o mouse aqui →");
        ImGui.SameLine();
        ImGui.TextUnformatted("Hover me");
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip("Tooltip simples sem quebra de linha.");

        Section("TooltipWrapped — texto longo");
        ImGui.TextDisabled("Passe o mouse aqui →");
        ImGui.SameLine();
        ImGui.TextUnformatted("Hover me (wrapped)");
        if (ImGui.IsItemHovered())
            ImGuiUtils.TooltipWrapped(
                "Este é um tooltip com texto longo que deve quebrar automaticamente " +
                "na largura configurada (padrão 300px). Útil para mensagens de ajuda detalhadas.");

        Section("HoveredTooltip — após item");
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Flag);
        ImGuiUtils.HoveredTooltip("Abrir no mapa");

        Section("HoveredTooltip — botão desabilitado (AllowWhenDisabled)");
        using (ImRaii.Disabled(true))
            ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Flag);
        ImGuiUtils.HoveredTooltip("Abrir no mapa", (int)ImGuiHoveredFlags.AllowWhenDisabled);
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
