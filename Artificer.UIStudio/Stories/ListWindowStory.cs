using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class ListWindowStory : IStory
{
    public string Category => "Templates";
    public string Name     => "List Window";

    private static readonly string[] Items =
    [
        "Titânio Polished",
        "Ração Mediana de Café",
        "Luvas de Couro Negro",
        "Capacete de Bronze",
        "Poção de Vitalidade",
        "Cristal de Fogo",
    ];

    private string _search = "";

    public void Draw()
    {
        const float ListH = 160f;

        Section("Com itens (ex: CraftingListWindow)");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputText("##search", ref _search, 128);
        ImGui.BeginChild("##list1", new Vector2(0, ListH), ImGuiChildFlags.Border);
        foreach (var item in Items)
            ImGui.Selectable(item);
        ImGui.EndChild();
        ImGui.Separator();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"{Items.Length} itens");
        ImGui.SameLine();
        ImGuiUtils.AlignRight(80f + 8f + 88f);
        ImGui.Button("Cancelar", new Vector2(80, 0));
        ImGui.SameLine(0, 8);
        Theme.PushPrimaryButton();
        ImGui.Button("Confirmar", new Vector2(88, 0));
        Theme.PopPrimaryButton();

        Section("Sem itens — empty state");
        ImGui.BeginChild("##list2", new Vector2(0, ListH), ImGuiChildFlags.Border);
        ImGuiUtils.DrawEmptyState(FontAwesomeIcon.Search, "Sem resultados",
            "Tente outros termos de busca.");
        ImGui.EndChild();
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
