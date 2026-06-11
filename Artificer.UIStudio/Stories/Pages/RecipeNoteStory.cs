using Artificer.Utils;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class RecipeNoteStory : IStory
{
    public string Category => "Pages";
    public string Name     => "RecipeNote";

    private static readonly string[] Estados =
    [
        "Sem receita", "Macro pronto", "Carregando macro",
    ];

    private static readonly string[] TiposReceita =
    [
        "Normal", "Expert", "Collectible", "Splendorous", "Specialist", "Cosmic",
    ];

    private static readonly string[] CraftableStatuses =
    [
        "OK", "WrongClassJob", "CraftsmanshipTooLow", "SpecialistRequired",
    ];

    private int _estado;
    private int _tipo;
    private int _craftableStatus;

    // Mock stats da receita selecionada.
    private const float ProgressMax   = 4100f;
    private const float QualityMax     = 7800f;
    private const float DurabilityMax  = 70f;
    private const float CpMax          = 590f;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawOverlay();
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Tipo", ref _tipo, TiposReceita, TiposReceita.Length);
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("CraftableStatus", ref _craftableStatus, CraftableStatuses, CraftableStatuses.Length);
    }

    // ── Overlay principal ─────────────────────────────────────────────────────
    private void DrawOverlay()
    {
        using (ImRaii2.GroupPanel("Crafting Helper", 360f, out var inner))
        {
            if (_estado == 0) // Sem receita
            {
                ImGuiUtils.DrawEmptyState(
                    FontAwesomeIcon.Book,
                    "Nenhuma receita aberta",
                    "Selecione uma receita no Crafting Log para ver sugestões.");
                return;
            }

            // Nome da receita + badge de tipo.
            ImGui.TextWrapped(RecipeName(_tipo));
            if (_tipo != 0)
            {
                ImGui.Spacing();
                ImGuiUtils.DrawBadgePill(TiposReceita[_tipo], TipoColor(_tipo));
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Validação de craftabilidade.
            if (_craftableStatus != 0)
            {
                ImGui.TextColored(Colors.Bad, CraftableStatusText(_craftableStatus));
                return;
            }

            // Barras de stats da receita.
            ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
            {
                new("Progress",   Colors.Progress,   ProgressMax,   ProgressMax),
                new("Quality",    Colors.Quality,    QualityMax,    QualityMax),
                new("Durability", Colors.Durability, DurabilityMax, DurabilityMax),
                new("CP",         Colors.CP,         CpMax,         CpMax),
            }, inner);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (_estado == 2) // Carregando macro
            {
                ImGui.TextDisabled("Calculando macro sugerido...");
            }
            else // Macro pronto
            {
                ImGui.TextUnformatted("Macro salvo: 8 ações (87% HQ)");
                ImGui.Spacing();
                Theme.PushPrimaryButton();
                ImGui.Button("Abrir no Editor", new Vector2(-1, 0));
                Theme.PopPrimaryButton();
            }
        }
    }

    // ── Mock helpers ──────────────────────────────────────────────────────────
    private static string RecipeName(int tipo) => tipo switch
    {
        0 => "Espada de Aço Ametista",
        1 => "Lâmina Expert de Cobalto",
        2 => "Engrenagem Coletável de Titânio",
        3 => "Anel Splendorous de Ouro",
        4 => "Manto de Especialista de Linho",
        5 => "Ferramenta Cósmica de Adamantita",
        _ => "Receita Desconhecida",
    };

    private static Vector4 TipoColor(int tipo) => tipo switch
    {
        1 => Colors.Durability,        // Expert
        2 => Colors.Collectability,    // Collectible
        3 => Colors.Quality,           // Splendorous
        4 => Colors.SpecialistGold,    // Specialist
        5 => Colors.CosmicActive,      // Cosmic
        _ => Colors.ActionBuff,        // Normal
    };

    private static string CraftableStatusText(int status) => status switch
    {
        1 => "Classe incorreta — troque para a classe da receita.",
        2 => "Craftsmanship insuficiente para esta receita.",
        3 => "Esta receita exige um Especialista (Soul of the Crafter).",
        _ => string.Empty,
    };
}
