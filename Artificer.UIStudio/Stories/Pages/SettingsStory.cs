using Artificer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class SettingsStory : IStory
{
    public string Category => "Pages";
    public string Name     => "Settings";

    private static readonly string[] Abas =
    [
        "General", "MacroEditor", "RecipeNote", "SynthHelper", "Solver", "About", "Experimental",
    ];

    private static readonly string[] Algoritmos =
    [
        "MCTS", "Raphael", "MCTS + Raphael", "Stepwise",
    ];

    private int _aba;

    // ── General ──
    private bool _enableRecipeNote     = true;
    private bool _enableSynthHelper    = true;
    private bool _enableCosmicTracker  = true;
    private bool _enableCollectionList = false;

    // ── MacroEditor ──
    private bool  _conditionRandomness = true;
    private float _qualityTarget       = 1f;

    // ── RecipeNote ──
    private bool _anchorCraftingLog = true;
    private bool _showTypeBadges     = true;

    // ── SynthHelper ──
    private bool _anchorSynthesis  = true;
    private bool _hideWhenCollapsed = false;

    // ── Solver ──
    private int _iterations = 500000;
    private int _algoritmo;

    // ── Experimental ──
    private bool _experimentalA;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        switch (_aba)
        {
            case 0: DrawGeneral();      break;
            case 1: DrawMacroEditor();  break;
            case 2: DrawRecipeNote();   break;
            case 3: DrawSynthHelper();  break;
            case 4: DrawSolver();       break;
            case 5: DrawAbout();        break;
            case 6: DrawExperimental(); break;
        }
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Aba", ref _aba, Abas, Abas.Length);
    }

    // ── General ───────────────────────────────────────────────────────────────
    private void DrawGeneral()
    {
        using (ImRaii2.GroupPanel("Plugin", -1, out _))
        {
            ImGui.Checkbox("Habilitar Recipe Note", ref _enableRecipeNote);
            ImGui.Checkbox("Habilitar Synthesis Helper", ref _enableSynthHelper);
            ImGui.Checkbox("Habilitar Cosmic Tracker", ref _enableCosmicTracker);
            ImGui.Checkbox("Habilitar Lista de Coleta", ref _enableCollectionList);
        }
    }

    // ── MacroEditor ───────────────────────────────────────────────────────────
    private void DrawMacroEditor()
    {
        using (ImRaii2.GroupPanel("Comportamento", -1, out var inner))
        {
            ImGui.Checkbox("Condition Randomness", ref _conditionRandomness);
            ImGui.SetNextItemWidth(inner);
            ImGui.SliderFloat("Quality Target %", ref _qualityTarget, 0f, 1f, "%.2f");
        }
    }

    // ── RecipeNote ────────────────────────────────────────────────────────────
    private void DrawRecipeNote()
    {
        using (ImRaii2.GroupPanel("Overlay", -1, out _))
        {
            ImGui.Checkbox("Ancorar ao Crafting Log", ref _anchorCraftingLog);
            ImGui.Checkbox("Mostrar badges de tipo", ref _showTypeBadges);
        }
    }

    // ── SynthHelper ───────────────────────────────────────────────────────────
    private void DrawSynthHelper()
    {
        using (ImRaii2.GroupPanel("Overlay", -1, out _))
        {
            ImGui.Checkbox("Ancorar ao addon Synthesis", ref _anchorSynthesis);
            ImGui.Checkbox("Ocultar quando colapsado", ref _hideWhenCollapsed);
        }
    }

    // ── Solver ────────────────────────────────────────────────────────────────
    private void DrawSolver()
    {
        using (ImRaii2.GroupPanel("Algoritmo", -1, out var inner))
        {
            ImGui.SetNextItemWidth(inner);
            ImGui.InputInt("Iterações", ref _iterations);
            ImGui.SetNextItemWidth(inner);
            ImGui.Combo("Algoritmo", ref _algoritmo, Algoritmos, Algoritmos.Length);
        }
    }

    // ── About ─────────────────────────────────────────────────────────────────
    private void DrawAbout()
    {
        ImGui.TextColored(Colors.CosmicActive, "Artificer");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Plugin de otimização de crafting para Final Fantasy XIV. Simula sínteses, " +
            "calcula macros otimizadas via MCTS + Raphael e exibe overlays no jogo.");
        ImGui.Spacing();
        ImGui.TextDisabled("Autor: alejandrofelipe (fork de WorkingRobot/Craftimizer)");
        ImGui.Spacing();

        Theme.PushPrimaryButton();
        ImGui.Button("Ko-fi");
        Theme.PopPrimaryButton();
        ImGui.SameLine(0, 8);
        ImGui.Button("GitHub");
    }

    // ── Experimental ──────────────────────────────────────────────────────────
    private void DrawExperimental()
    {
        ImGui.BeginChild("##experimental-warn", new Vector2(0, 160), ImGuiChildFlags.None);
        ImGuiUtils.DrawEmptyState(
            FontAwesomeIcon.ExclamationTriangle,
            "Recursos experimentais",
            "Estas opções são instáveis e podem mudar ou quebrar a qualquer momento.");
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii2.GroupPanel("Experimental", -1, out _))
        {
            ImGui.Checkbox("Feature experimental A", ref _experimentalA);
        }
    }
}
