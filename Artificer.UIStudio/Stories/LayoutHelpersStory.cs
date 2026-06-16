using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class LayoutHelpersStory : IStory
{
    public string Category => "Atoms";
    public string Name     => "Layout Helpers";

    public void Draw()
    {
        Section("DrawSectionHeader — simples");
        ImGuiUtils.DrawSectionHeader("Estatísticas");
        ImGui.TextUnformatted("Conteúdo da seção.");

        Section("DrawSectionHeader — com ação à direita");
        ImGuiUtils.DrawSectionHeader("Buffs", () =>
        {
            ImGuiUtils.AlignRight(ImGui.GetFrameHeight());
            ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Sync);
        });
        ImGui.TextUnformatted("Conteúdo com botão no cabeçalho.");

        Section("DrawStatRow — variantes");
        ImGui.TextDisabled("SEM COR:");
        ImGuiUtils.DrawStatRow("Craftsmanship", "4500");
        ImGuiUtils.DrawStatRow("Control", "3800");

        ImGui.Spacing();
        ImGui.TextDisabled("COM COR POSITIVA:");
        ImGuiUtils.DrawStatRow("Progress",   "3500",  Colors.Progress);
        ImGuiUtils.DrawStatRow("HQ%",        "100%",  Colors.Progress);

        ImGui.Spacing();
        ImGui.TextDisabled("COM COR NEGATIVA:");
        ImGuiUtils.DrawStatRow("Durability", "0",     Colors.Bad);
        ImGuiUtils.DrawStatRow("CP",         "0",     Colors.Bad);

        ImGui.Spacing();
        ImGui.TextDisabled("VALOR LONGO:");
        ImGuiUtils.DrawStatRow("Research Type III", "12.345 / 20.000");
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
