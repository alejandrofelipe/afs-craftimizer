using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class ThemeStory : IStory
{
    public string Category => "Atoms";
    public string Name     => "Theme";

    private static readonly string[] ComboOptions = ["Opção A", "Opção B", "Opção C"];

    private string _text = "exemplo de texto";
    private bool   _checked;
    private int    _combo;
    private float  _progress = 0.6f;

    public void Draw()
    {
        Section("Botões");
        ImGui.Button("Normal");
        ImGui.SameLine(0, 8);
        Theme.PushPrimaryButton();
        ImGui.Button("Primário");
        Theme.PopPrimaryButton();
        ImGui.SameLine(0, 8);
        Theme.PushDangerButton();
        ImGui.Button("Perigo");
        Theme.PopDangerButton();
        ImGui.SameLine(0, 8);
        ImGui.BeginDisabled(true);
        ImGui.Button("Disabled");
        ImGui.EndDisabled();

        Section("Inputs");
        ImGui.SetNextItemWidth(260);
        ImGui.InputText("##text", ref _text, 256);
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(140);
        if (ImGui.BeginCombo("##combo", ComboOptions[_combo]))
        {
            for (int i = 0; i < ComboOptions.Length; i++)
                if (ImGui.Selectable(ComboOptions[i], _combo == i))
                    _combo = i;
            ImGui.EndCombo();
        }

        Section("Seleção");
        ImGui.Checkbox("Desabilitado", ref _checked);
        ImGui.SameLine(0, 16);
        var t = true;
        ImGui.Checkbox("Marcado", ref t);

        Section("Barra de progresso");
        ImGui.SliderFloat("##prog", ref _progress, 0f, 1f);
        ImGui.ProgressBar(_progress, new Vector2(300, 0), $"{_progress:P0}");

        Section("GroupPanel");
        using (ImRaii2.GroupPanel("Painel de grupo", 340, out _))
        {
            ImGui.TextUnformatted("Conteúdo interno do painel.");
            ImGui.TextDisabled("Texto secundário.");
        }

        Section("Badge pills");
        ImGuiUtils.DrawBadgePill("Expert",     Colors.ActionSpecial);
        ImGui.SameLine(0, 6);
        ImGuiUtils.DrawBadgePill("Collectible", Colors.Collectability);
        ImGui.SameLine(0, 6);
        ImGuiUtils.DrawBadgePill("Splendorous", Colors.SpecialistGold);
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
