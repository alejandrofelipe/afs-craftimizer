using Craftimizer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Craftimizer.UIStudio.Stories;

internal sealed class SinglePanelStory : IStory
{
    public string Category => "Templates";
    public string Name     => "Single Panel";

    public void Draw()
    {
        Section("Sem footer (ex: RecipeNote)");
        using (ImRaii2.GroupPanel("Notas da Receita", 420, out _))
        {
            ImGui.TextWrapped("Lembre-se de usar Careful Synthesis nos últimos passos para garantir o Progress sem desperdiçar Durability.");
            ImGui.Spacing();
            ImGui.TextWrapped("Craft com nível 90+ garante maior taxa de HQ.");
        }

        Section("Com footer (ex: MacroClipboard)");
        using (ImRaii2.GroupPanel("Macro Copiado", 420, out var panelW))
        {
            using (ImRaii2.TextWrapPos(panelW))
            {
                ImGui.TextUnformatted("/ac \"Muscle Memory\" <wait.3>");
                ImGui.TextUnformatted("/ac \"Careful Synthesis\" <wait.3>");
                ImGui.TextUnformatted("/ac \"Standard Touch\" <wait.3>");
                ImGui.TextUnformatted("/ac \"Byregot's Blessing\" <wait.3>");
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGuiUtils.AlignRight(100f, panelW);
            Theme.PushPrimaryButton();
            ImGui.Button("Copiar", new Vector2(100, 0));
            Theme.PopPrimaryButton();
        }
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
