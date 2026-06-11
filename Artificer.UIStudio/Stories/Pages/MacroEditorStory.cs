using Artificer.Utils;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class MacroEditorStory : IStory
{
    public string Category => "Pages";
    public string Name     => "MacroEditor";

    private static readonly string[] Estados =
    [
        "Sem receita", "Com receita", "Solver rodando", "Solver pronto", "Solver error",
    ];

    private static readonly string[] TiposReceita =
    [
        "Normal", "Expert", "Collectible", "Splendorous", "Specialist", "Cosmic",
    ];

    // 8 placeholders de ações de macro.
    private static readonly FontAwesomeIcon[] MacroSteps =
    [
        FontAwesomeIcon.Tools, FontAwesomeIcon.Star, FontAwesomeIcon.Cog, FontAwesomeIcon.Sync,
        FontAwesomeIcon.Edit, FontAwesomeIcon.Tools, FontAwesomeIcon.Star, FontAwesomeIcon.Cog,
    ];

    private int  _estado;
    private int  _tipo;
    private bool _cosmicBtn;

    // Mock stats
    private int _craftsmanship = 4321;
    private int _control       = 4150;
    private int _cp            = 647;
    private int _level         = 100;

    private float _solverProgress = 0.62f;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_estado == 0) // Sem receita
        {
            DrawEmptyWindow();
            return;
        }

        var avail = ImGui.GetContentRegionAvail();
        const float LeftW  = 220f;
        const float RightW = 280f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var centerW = avail.X - LeftW - RightW - spacing * 2f;
        if (centerW < 160f)
            centerW = 160f;

        DrawLeftColumn(LeftW, avail.Y);
        ImGui.SameLine(0, spacing);
        DrawCenterColumn(centerW, avail.Y);
        ImGui.SameLine(0, spacing);
        DrawRightColumn(RightW, avail.Y);
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Estado", ref _estado, Estados, Estados.Length);
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Tipo receita", ref _tipo, TiposReceita, TiposReceita.Length);
        ImGui.SameLine(0, 16);
        ImGui.Checkbox("Cosmic btn (★ flag)", ref _cosmicBtn);
    }

    // ── Empty state ───────────────────────────────────────────────────────────
    private static void DrawEmptyWindow()
    {
        using (ImRaii2.GroupPanel("Editor de Macro", -1, out _))
        {
            ImGui.BeginChild("##empty", new Vector2(0, 220), ImGuiChildFlags.None);
            ImGuiUtils.DrawEmptyState(
                FontAwesomeIcon.Book,
                "Nenhuma receita selecionada",
                "Abra o crafting log e escolha uma receita para otimizar.");
            ImGui.EndChild();
        }
    }

    // ── Coluna esquerda — stats do personagem ─────────────────────────────────
    private void DrawLeftColumn(float width, float height)
    {
        ImGui.BeginChild("##left", new Vector2(width, height), ImGuiChildFlags.Border);

        using (ImRaii2.GroupPanel("Personagem", -1, out var inner))
        {
            ImGui.SetNextItemWidth(inner);
            ImGui.InputInt("Craftsmanship", ref _craftsmanship);
            ImGui.SetNextItemWidth(inner);
            ImGui.InputInt("Control", ref _control);
            ImGui.SetNextItemWidth(inner);
            ImGui.InputInt("CP", ref _cp);
            ImGui.SetNextItemWidth(inner);
            ImGui.InputInt("Level", ref _level);
        }

        ImGui.Spacing();

        using (ImRaii2.GroupPanel("Receita", -1, out _))
        {
            ImGui.TextWrapped(RecipeName(_tipo));
            ImGui.Spacing();
            ImGuiUtils.DrawBadgePill(TiposReceita[_tipo], TipoColor(_tipo));
            if (_cosmicBtn)
            {
                ImGui.SameLine(0, 6);
                ImGuiUtils.DrawBadgePill("★ Cosmic", Colors.CosmicActive);
            }
        }

        ImGui.EndChild();
    }

    // ── Coluna central — ações da macro ───────────────────────────────────────
    private void DrawCenterColumn(float width, float height)
    {
        ImGui.BeginChild("##center", new Vector2(width, height), ImGuiChildFlags.Border);

        ImGui.TextDisabled("MACRO");
        ImGui.Separator();
        ImGui.Spacing();

        switch (_estado)
        {
            case 2: // Solver rodando
                DrawSolverRunning(width);
                break;
            case 3: // Solver pronto
                DrawMacroActions(width);
                break;
            case 4: // Solver error
                ImGui.BeginChild("##err", new Vector2(0, 180));
                ImGuiUtils.DrawEmptyState(
                    FontAwesomeIcon.ExclamationTriangle,
                    "Falha no solver",
                    "Não foi possível gerar uma macro. Verifique os stats e tente novamente.",
                    ("Tentar novamente", () => { }));
                ImGui.EndChild();
                break;
            default: // 1 = Com receita (ainda sem macro)
                ImGui.BeginChild("##nomacro", new Vector2(0, 180));
                ImGuiUtils.DrawEmptyState(
                    FontAwesomeIcon.Cog,
                    "Sem macro ainda",
                    "Clique em \"Calcular Macro\" para gerar uma sequência otimizada.");
                ImGui.EndChild();
                break;
        }

        ImGui.EndChild();
    }

    private void DrawSolverRunning(float width)
    {
        ImGui.TextUnformatted("Calculando macro otimizada...");
        ImGui.Spacing();
        var cfg = new ProgressBarComponent.VisualConfig(Width: width - 24f);
        ProgressBarComponent.DrawSimple(
            (int)(_solverProgress * 100), 100,
            ProgressBarComponent.ProgressState.InProgress, cfg);

        ImGui.Spacing();
        ImGui.SetNextItemWidth(220);
        ImGui.SliderFloat("##prog", ref _solverProgress, 0f, 1f, "%.2f");
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled("(simula progresso)");
    }

    private static void DrawMacroActions(float width)
    {
        const float Btn = 44f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = (int)((width - 24f + spacing) / (Btn + spacing));
        if (perRow < 1) perRow = 1;

        for (var i = 0; i < MacroSteps.Length; i++)
        {
            if (i % perRow != 0)
                ImGui.SameLine(0, spacing);
            ImGuiUtils.IconButtonSquare(MacroSteps[i], Btn);
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"{MacroSteps.Length} passos");
    }

    // ── Coluna direita — resultados ───────────────────────────────────────────
    private void DrawRightColumn(float width, float height)
    {
        ImGui.BeginChild("##right", new Vector2(width, height), ImGuiChildFlags.Border);

        var done = _estado == 3;
        var f    = done ? 1f : _estado == 2 ? _solverProgress : 0f;

        ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
        {
            new("Progress",   Colors.Progress,   f * 4000f, 4000f),
            new("Quality",    Colors.Quality,    f * 3500f, 3500f),
            new("Durability", Colors.Durability,      70f,    80f),
            new("CP",         Colors.CP,         _cp - f * 540f, _cp),
        });

        ImGui.Spacing();

        using (ImRaii2.GroupPanel("Resultado", -1, out _))
        {
            ImGui.TextUnformatted("HQ%:");
            ImGui.SameLine();
            ImGui.TextColored(Colors.HQ, done ? "100%" : "—");

            ImGui.TextUnformatted("Steps:");
            ImGui.SameLine();
            ImGui.TextColored(Colors.Collectability, done ? MacroSteps.Length.ToString() : "—");
        }

        ImGui.Spacing();

        Theme.PushPrimaryButton();
        ImGui.Button("Calcular Macro", new Vector2(-1, 0));
        Theme.PopPrimaryButton();

        ImGui.Button("Reverter", new Vector2((width - 24f - ImGui.GetStyle().ItemSpacing.X) / 2f, 0));
        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X);
        ImGui.Button("Copiar", new Vector2(-1, 0));

        ImGui.EndChild();
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
}
