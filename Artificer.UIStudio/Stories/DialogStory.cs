using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class DialogStory : IStory
{
    public string Category => "Templates";
    public string Name     => "Dialog";

    private const float CardH = 210f;
    private const float BtnW  = 110f;

    public void Draw()
    {
        Section("Variantes de diálogo (ex: ações destrutivas, CraftingListAdd)");

        var style = ImGui.GetStyle();
        var cardW = (ImGui.GetContentRegionAvail().X - style.ItemSpacing.X * 2f) / 3f;

        // ── Informativa ───────────────────────────────────────────────────────
        ImGui.BeginChild("##d0", new Vector2(cardW, CardH + 24), ImGuiChildFlags.Border);
        ImGui.TextDisabled("Informativa");
        ImGui.Separator();
        ImGui.BeginChild("##dc0", new Vector2(0, CardH));
        ImGuiUtils.DrawEmptyState(FontAwesomeIcon.InfoCircle,
            "Operação concluída", "Seus dados foram salvos.",
            primaryButton: ("OK", () => {}));
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.SameLine(0, style.ItemSpacing.X);

        // ── Confirmação ───────────────────────────────────────────────────────
        ImGui.BeginChild("##d1", new Vector2(cardW, CardH + 24), ImGuiChildFlags.Border);
        ImGui.TextDisabled("Confirmação");
        ImGui.Separator();
        ImGui.BeginChild("##dc1", new Vector2(0, CardH));
        ImGuiUtils.DrawEmptyState(FontAwesomeIcon.ExclamationTriangle,
            "Confirmar ação", "Esta ação não pode ser desfeita.",
            primaryButton:   ("Confirmar", () => {}),
            secondaryButton: ("Cancelar",  () => {}));
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.SameLine(0, style.ItemSpacing.X);

        // ── Destrutiva (layout manual — DrawEmptyState não suporta danger button) ──
        ImGui.BeginChild("##d2", new Vector2(cardW, CardH + 24), ImGuiChildFlags.Border);
        ImGui.TextDisabled("Destrutiva");
        ImGui.Separator();
        ImGui.BeginChild("##dc2", new Vector2(0, CardH));
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGuiUtils.TextCentered("Remover item");
        ImGui.Spacing();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGuiUtils.TextCentered("O item será removido permanentemente.");
        ImGui.Spacing();
        ImGui.Spacing();
        ImGuiUtils.AlignCentered(BtnW);
        ImGui.Button("Cancelar", new Vector2(BtnW, 0));
        ImGuiUtils.AlignCentered(BtnW);
        Theme.PushDangerButton();
        ImGui.Button("Remover", new Vector2(BtnW, 0));
        Theme.PopDangerButton();
        ImGui.EndChild();
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
