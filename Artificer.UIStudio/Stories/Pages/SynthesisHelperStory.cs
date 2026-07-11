using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class SynthesisHelperStory : IStory
{
    public string Category => "Pages";
    public string Name     => "SynthesisHelper";

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
        using (ImRaii2.GroupPanel("Synth Helper", 320f, out _))
        {
            // Barras de stats (2 colunas, compacto) — mock manual (DynamicBars é do projeto plugin).
            var bars = new (string Name, Vector4 Color, float Value, float Max)[]
            {
                ("Progress",   Colors.Progress,   ProgressCur,   ProgressMax),
                ("Quality",    Colors.Quality,    QualityCur,    QualityMax),
                ("Durability", Colors.Durability, DurabilityCur, DurabilityMax),
                ("CP",         Colors.CP,         CpCur,         CpMax),
            };
            if (ImGui.BeginTable("synthbars", 2, ImGuiTableFlags.SizingStretchSame))
            {
                for (var i = 0; i < bars.Length; i++)
                {
                    if (i % 2 == 0) ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(i % 2);
                    var b = bars[i];
                    var frac = b.Max > 0 ? System.Math.Clamp(b.Value / b.Max, 0f, 1f) : 0f;
                    ImGuiUtils.ProgressBar(frac, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()), $"{b.Name} {b.Value:0}", b.Color);
                }
                ImGui.EndTable();
            }

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
