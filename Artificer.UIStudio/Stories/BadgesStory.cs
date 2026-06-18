using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class BadgesStory : IStory
{
    public string Category => "Molecules";
    public string Name     => "Badges";

    public void Draw()
    {
        Section("DrawBadgePill — variantes");
        ImGuiUtils.DrawBadgePill("Progresso",    Colors.Progress);
        ImGui.SameLine(0, 8);
        ImGuiUtils.DrawBadgePill("Qualidade",    Colors.Quality);
        ImGui.SameLine(0, 8);
        ImGuiUtils.DrawBadgePill("Perigo",       Colors.Bad);
        ImGui.SameLine(0, 8);
        ImGuiUtils.DrawBadgePill("Durabilidade", Colors.Durability);

        Section("DrawBadge — textura (handle nulo em UIStudio)");
        ImGui.TextDisabled("(sem textura real — handle = 0 → caixa vazia esperada)");
        ImGui.Spacing();
        ImGuiUtils.DrawBadge(nint.Zero, new Vector2(32, 32), "Tooltip do badge");
        ImGui.SameLine(0, 8);
        ImGuiUtils.DrawBadge(nint.Zero, new Vector2(48, 48), "Badge maior com tint", Colors.Quality);

        Section("DrawCosmicStageBadge — estados");
        ImGui.TextDisabled("Ativo com max:");
        ImGuiUtils.DrawCosmicStageBadge(stage: 3, complete: false, maxStage: 4);
        ImGui.Spacing();
        ImGui.TextDisabled("Completo:");
        ImGuiUtils.DrawCosmicStageBadge(stage: 3, complete: true, maxStage: 4);
        ImGui.Spacing();
        ImGui.TextDisabled("Ativo sem max:");
        ImGuiUtils.DrawCosmicStageBadge(stage: 3, complete: false, maxStage: 0);
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}
