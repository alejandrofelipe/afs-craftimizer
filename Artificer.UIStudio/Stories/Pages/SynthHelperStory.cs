using Artificer.Utils;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class SynthHelperStory : IStory
{
    public string Category => "Pages";
    public string Name     => "SynthHelper";

    private static readonly string[] Estados =
    [
        "Calculando", "Sugestão pronta", "Collapsed",
    ];

    private static readonly string[] Condicoes =
    [
        "Normal", "Good", "Excellent", "Poor",
    ];

    private int  _estado;
    private int  _condicao;
    private bool _cosmicBtn;

    // Mock stats da síntese em andamento.
    private const float ProgressCur   = 1850f;
    private const float ProgressMax   = 3400f;
    private const float QualityCur    = 2100f;
    private const float QualityMax    = 6500f;
    private const float DurabilityCur = 45f;
    private const float DurabilityMax = 80f;
    private const float CpCur         = 312f;
    private const float CpMax         = 540f;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_estado == 2) // Collapsed
        {
            DrawCollapsed();
            return;
        }

        DrawOverlay();
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Condição", ref _condicao, Condicoes, Condicoes.Length);
        ImGui.SameLine(0, 16);
        ImGui.Checkbox("Cosmic btn", ref _cosmicBtn);
    }

    // ── Estado minimizado ─────────────────────────────────────────────────────
    private static void DrawCollapsed()
    {
        using (ImRaii2.GroupPanel("Synth Helper", 200f, out _))
        {
            ImGui.TextDisabled("— minimizado —");
        }
    }

    // ── Overlay principal ─────────────────────────────────────────────────────
    private void DrawOverlay()
    {
        using (ImRaii2.GroupPanel("Synth Helper", 320f, out var inner))
        {
            // Barras de stats da síntese.
            ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
            {
                new("Progress",   Colors.Progress,   ProgressCur,   ProgressMax),
                new("Quality",    Colors.Quality,    QualityCur,    QualityMax),
                new("Durability", Colors.Durability, DurabilityCur, DurabilityMax),
                new("CP",         Colors.CP,         CpCur,         CpMax),
            }, inner);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Condição atual.
            ImGui.TextUnformatted("Condição:");
            ImGui.SameLine();
            ImGui.TextColored(ConditionColor(_condicao), Condicoes[_condicao]);

            ImGui.Spacing();

            // Próxima ação recomendada.
            if (_estado == 0) // Calculando
            {
                ImGui.TextDisabled("Calculando próxima ação...");
            }
            else // Sugestão pronta
            {
                ImGui.TextUnformatted("Próxima ação:");
                ImGui.Spacing();
                ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Tools, 44f);
                ImGui.SameLine(0, 8);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted("Preparação Básica");
            }

            if (_cosmicBtn)
            {
                ImGui.Spacing();
                ImGuiUtils.DrawBadgePill("★ Cosmic", Colors.CosmicActive);
            }
        }
    }

    // ── Mock helpers ──────────────────────────────────────────────────────────
    private static Vector4 ConditionColor(int condicao) => condicao switch
    {
        1 => Colors.ConditionGood,      // Good
        2 => Colors.ConditionExcellent, // Excellent
        3 => Colors.ConditionPoor,      // Poor
        _ => Colors.ConditionNormal,    // Normal
    };
}
