using Artificer.Utils;
using ImGuiNET;
using System;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class EmptyStateStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "Empty State";

    private static readonly (string Label, FontAwesomeIcon Icon, string Title, string? Subtitle,
        (string, Action)? Primary, (string, Action)? Secondary)[] Variants =
    [
        ("Ícone + título",    FontAwesomeIcon.Search,    "Sem resultados",       null, null, null),
        ("Com subtítulo",     FontAwesomeIcon.InfoCircle,"Nada por aqui",        "Tente outros filtros.", null, null),
        ("Com botão primário",FontAwesomeIcon.PlusCircle,"Nenhum item",          "Crie o primeiro item.", ("+ Criar", () => {}), null),
        ("Com dois botões",   FontAwesomeIcon.Trash,     "Lista vazia",          "Adicione itens.", ("+ Adicionar", () => {}), ("Importar", () => {})),
    ];

    private const float CardH = 180f;

    public void Draw()
    {
        var cardW = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * (Variants.Length - 1)) / Variants.Length;

        for (int i = 0; i < Variants.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X);

            var (label, icon, title, subtitle, primary, secondary) = Variants[i];

            ImGui.BeginChild($"##es{i}", new Vector2(cardW, CardH + 28), ImGuiChildFlags.Border);

            ImGui.TextDisabled(label);
            ImGui.Separator();

            ImGui.BeginChild($"##esc{i}", new Vector2(0, CardH));
            ImGuiUtils.DrawEmptyState(icon, title, subtitle, primary, secondary);
            ImGui.EndChild();

            ImGui.EndChild();
        }
    }
}
