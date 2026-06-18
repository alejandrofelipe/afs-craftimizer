using Artificer.Utils;
using ImGuiNET;
using System.Collections.Generic;

namespace Artificer.UIStudio.Stories;

internal sealed class SearchableComboStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "SearchableCombo";

    private static readonly IReadOnlyList<string> Jobs =
    [
        "Carpenter", "Blacksmith", "Armorer", "Goldsmith",
        "Leatherworker", "Weaver", "Alchemist", "Culinarian",
    ];

    private string _selected = "Carpenter";

    public void Draw()
    {
        ImGui.TextDisabled("Item selecionado:");
        ImGui.SameLine();
        ImGui.TextUnformatted(_selected);

        ImGui.Spacing();
        ImGui.TextDisabled("Combo com busca fuzzy (digitar para filtrar):");
        ImGuiUtils.SearchableCombo(
            id:             "##job_combo",
            selectedItem:   ref _selected,
            items:          Jobs,
            selectableFont: UiServices.Current.DefaultFont,
            width:          200f,
            getString:      j => j,
            getId:          j => j,
            draw:           j => ImGui.TextUnformatted(j));
    }
}
