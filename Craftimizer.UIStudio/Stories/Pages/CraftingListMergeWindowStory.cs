using Craftimizer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Craftimizer.UIStudio.Stories;

internal sealed class CraftingListMergeWindowStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CraftingListMergeWindow";

    private const float WindowWidth = 380f;

    private static readonly string[] Estados =
    [
        "Seleção de listas", "Confirmando merge",
    ];

    private static readonly string[] ListNames =
    [
        "Equipamento Dawntrail",
        "Materiais Housing",
        "Batch Crafting Semanal",
    ];

    private int _estado;
    private readonly bool[] _selected = [true, false, true];

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawSelectionPanel();
        ImGui.Spacing();

        if (_estado == 1)
        {
            DrawConfirmPanel();
        }
        else
        {
            Theme.PushPrimaryButton();
            ImGui.Button("Próximo →", new Vector2(WindowWidth, 0));
            Theme.PopPrimaryButton();
        }
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
    }

    // ── Painel de seleção de listas ───────────────────────────────────────────
    private void DrawSelectionPanel()
    {
        using (ImRaii2.GroupPanel("Listas para Merge", WindowWidth, out _))
        {
            for (var i = 0; i < ListNames.Length; i++)
            {
                using (ImRaii.PushId(i))
                    ImGui.Checkbox(ListNames[i], ref _selected[i]);
            }
        }
    }

    // ── Painel de confirmação ─────────────────────────────────────────────────
    private void DrawConfirmPanel()
    {
        using (ImRaii2.GroupPanel("Confirmar", WindowWidth, out var width))
        {
            var count = 0;
            for (var i = 0; i < ListNames.Length; i++)
            {
                if (!_selected[i])
                    continue;

                count++;
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextUnformatted(ListNames[i]);
            }

            ImGui.Spacing();

            using (ImRaii2.TextWrapPos(width))
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextWrapped(
                    $"Estas {count} listas serão unidas em uma nova lista. As originais serão mantidas.");

            ImGui.Spacing();

            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var btnW    = (width - spacing) / 2f;

            ImGui.Button("Cancelar", new Vector2(btnW, 0));
            ImGui.SameLine();

            Theme.PushPrimaryButton();
            ImGui.Button("Confirmar Merge", new Vector2(btnW, 0));
            Theme.PopPrimaryButton();
        }
    }
}
