using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Artificer.Utils;

/// <summary>
/// Componente robusto e altamente customizável de barras de progresso.
/// Suporta múltiplos modos: simples, agregado, multi-resultado, com tooltips ricos.
/// </summary>
public static partial class ProgressBarComponent
{
    #region Enums e Records

    /// <summary>
    /// Modos de exibição disponíveis para o componente de progresso.
    /// </summary>
    public enum DisplayMode
    {
        /// <summary>Barra horizontal tradicional com percentual à direita</summary>
        Horizontal,
        /// <summary>Arco circular estilo stat rings do jogo</summary>
        Arc,
        /// <summary>Layout compacto mostrando apenas percentual centralizado</summary>
        Compact,
        /// <summary>Múltiplas barras empilhadas verticalmente (uso com DrawAggregated)</summary>
        Stacked
    }

    /// <summary>
    /// Estado atual de um processo de progresso.
    /// </summary>
    public enum ProgressState
    {
        /// <summary>Processo em andamento com progresso determinado</summary>
        InProgress,
        /// <summary>Processo completado com sucesso</summary>
        Completed,
        /// <summary>Processo inicializando (progresso indeterminado)</summary>
        Indeterminate,
        /// <summary>Processo cancelado pelo usuário</summary>
        Cancelled,
        /// <summary>Processo falhou com erro</summary>
        Failed
    }

    /// <summary>
    /// Snapshot imutável de progresso de um único processo.
    /// Representa o estado de progresso em um ponto específico no tempo.
    /// </summary>
    /// <param name="Name">Nome descritivo do processo (exibido no tooltip)</param>
    /// <param name="CurrentValue">Valor atual de progresso</param>
    /// <param name="MaxValue">Valor máximo esperado</param>
    /// <param name="State">Estado atual do processo</param>
    /// <param name="Stage">Estágio opcional para coloração progressiva (usado em algoritmos iterativos)</param>
    /// <param name="CustomTooltip">Tooltip customizado que sobrescreve o padrão</param>
    /// <param name="CustomColor">Cor customizada da barra (sobrescreve esquema de cores do tema)</param>
    public readonly record struct ProgressSnapshot(
        string Name,
        int CurrentValue,
        int MaxValue,
        ProgressState State,
        int? Stage = null,
        string? CustomTooltip = null,
        Vector4? CustomColor = null)
    {
        /// <summary>Fração de progresso entre 0.0 e 1.0, sempre clamped</summary>
        public float Fraction => MaxValue > 0 ? Math.Clamp((float)CurrentValue / MaxValue, 0f, 1f) : 0f;

        /// <summary>Verdadeiro se o processo está no estado Completed</summary>
        public bool IsComplete => State == ProgressState.Completed;

        /// <summary>Verdadeiro se o processo está no estado Indeterminate</summary>
        public bool IsIndeterminate => State == ProgressState.Indeterminate;
    }

    /// <summary>
    /// Configuração visual do componente de progresso.
    /// Todos os parâmetros são opcionais e têm defaults sensatos.
    /// </summary>
    /// <param name="Mode">Modo de exibição da barra (Horizontal, Arc, Compact, Stacked)</param>
    /// <param name="ColorTheme">Esquema de cores (Colorful usa paleta progressiva, Simple usa monocromático)</param>
    /// <param name="Width">Largura customizada em pixels (null = usar espaço disponível)</param>
    /// <param name="Height">Altura customizada em pixels (null = usar ImGui.GetFrameHeight())</param>
    /// <param name="ShowPercentage">Se verdadeiro, exibe percentual de progresso</param>
    /// <param name="ShowDetailedTooltip">Se verdadeiro, exibe tooltip ao passar mouse</param>
    /// <param name="ShowSummaryWhenAggregated">Se verdadeiro, mostra "X de Y" em vez de "Progresso" em DrawAggregated</param>
    /// <param name="BackgroundColor">Cor de fundo customizada (sobrescreve tema)</param>
    /// <param name="ForegroundColor">Cor de preenchimento customizada (sobrescreve tema)</param>
    public record VisualConfig(
        DisplayMode Mode = DisplayMode.Horizontal,
        ProgressBarType ColorTheme = ProgressBarType.Colorful,
        float? Width = null,
        float? Height = null,
        bool ShowPercentage = true,
        bool ShowDetailedTooltip = true,
        bool ShowSummaryWhenAggregated = true,
        Vector4? BackgroundColor = null,
        Vector4? ForegroundColor = null)
    {
        /// <summary>Configuração padrão com valores sensatos</summary>
        public static VisualConfig Default => new();
    }

    #endregion

    #region API Pública - Modo Simples

    /// <summary>
    /// Desenha uma barra de progresso simples sem criar snapshot explicitamente.
    /// Útil para casos simples onde não é necessário rastreamento de estado.
    /// </summary>
    /// <param name="currentValue">Valor atual de progresso</param>
    /// <param name="maxValue">Valor máximo esperado</param>
    /// <param name="state">Estado do processo (default: InProgress)</param>
    /// <param name="config">Configuração visual opcional (null = usar padrões)</param>
    /// <param name="tooltipOverride">Tooltip customizado opcional</param>
    /// <example>
    /// <code>
    /// ProgressBarComponent.DrawSimple(75, 100, ProgressState.InProgress);
    /// </code>
    /// </example>
    public static void DrawSimple(
        int currentValue,
        int maxValue,
        ProgressState state = ProgressState.InProgress,
        VisualConfig? config = null,
        string? tooltipOverride = null)
    {
        var snapshot = new ProgressSnapshot(
            Name: "Progress",
            CurrentValue: currentValue,
            MaxValue: maxValue,
            State: state,
            CustomTooltip: tooltipOverride
        );

        DrawSingle(snapshot, config ?? VisualConfig.Default);
    }

    /// <summary>
    /// Desenha uma barra de progresso com base em um snapshot único.
    /// Use este método quando você tem um snapshot existente (ex: de CraftingSession.SolverSnapshots).
    /// </summary>
    /// <param name="snapshot">Snapshot de progresso contendo estado atual</param>
    /// <param name="config">Configuração visual opcional (null = usar padrões)</param>
    /// <example>
    /// <code>
    /// var snapshot = ProgressBarComponent.FromSolver(solver, "MCTS");
    /// var config = new VisualConfig(Width: 300f, ShowPercentage: true);
    /// ProgressBarComponent.DrawSingle(snapshot, config);
    /// </code>
    /// </example>
    public static void DrawSingle(ProgressSnapshot snapshot, VisualConfig? config = null)
    {
        config ??= VisualConfig.Default;

        switch (config.Mode)
        {
            case DisplayMode.Horizontal:
                DrawHorizontalBar(snapshot, config);
                break;
            case DisplayMode.Arc:
                DrawArcBar(snapshot, config);
                break;
            case DisplayMode.Compact:
                DrawCompactBar(snapshot, config);
                break;
            case DisplayMode.Stacked:
                DrawHorizontalBar(snapshot, config); // Fallback para modo único
                break;
        }
    }

    #endregion

    #region API Pública - Modo Agregado

    /// <summary>
    /// Desenha progresso agregado proporcional baseado em múltiplos snapshots.
    /// Cada snapshot completo contribui com sua fração (1/N) para o progresso total.
    /// Exemplo: 2 de 5 processos completos = 40% na barra agregada.
    /// Tooltip detalhado lista todos os snapshots individuais com ícones de estado.
    /// </summary>
    /// <param name="snapshots">Lista de snapshots de processos em andamento ou completos</param>
    /// <param name="config">Configuração visual opcional (null = usar padrões)</param>
    /// <remarks>
    /// Se a lista estiver vazia, exibe fallback com mensagem "Aguardando início dos cálculos...".
    /// Se todos os snapshots estão completos, a barra muda automaticamente para estado Completed.
    /// Tooltip agregado mostra cada snapshot com ícone colorido (✓/⏳/✗/⚠) e percentual individual.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Em CraftingSession, desenhar progresso de solver
    /// var config = new VisualConfig(
    ///     ColorTheme: Configuration.ProgressType,
    ///     Width: ImGui.GetContentRegionAvail().X
    /// );
    /// ProgressBarComponent.DrawAggregated(Session.SolverSnapshots, config);
    /// </code>
    /// </example>
    public static void DrawAggregated(
        IReadOnlyList<ProgressSnapshot> snapshots,
        VisualConfig? config = null)
    {
        config ??= VisualConfig.Default;

        if (snapshots.Count == 0)
        {
            // Fallback: barra vazia
            DrawSimple(0, 1, ProgressState.InProgress, config, "Aguardando início dos cálculos...");
            return;
        }

        var completedCount = snapshots.Count(s => s.IsComplete);
        var totalCount = snapshots.Count;
        var aggregateFraction = (float)completedCount / totalCount;

        // Criar snapshot agregado
        var aggregateSnapshot = new ProgressSnapshot(
            Name: config.ShowSummaryWhenAggregated ? $"{completedCount} de {totalCount}" : "Progresso",
            CurrentValue: completedCount,
            MaxValue: totalCount,
            State: completedCount == totalCount ? ProgressState.Completed : ProgressState.InProgress
        );

        // Desenhar baseado no modo
        if (config.Mode == DisplayMode.Stacked)
        {
            DrawStackedBars(snapshots, config);
        }
        else
        {
            DrawSingle(aggregateSnapshot, config);

            // Tooltip detalhado
            if (ImGui.IsItemHovered() && config.ShowDetailedTooltip)
            {
                DrawAggregatedTooltip(snapshots, completedCount, totalCount);
            }
        }
    }

    #endregion

    #region Implementações de Renderização

    private static void DrawHorizontalBar(ProgressSnapshot snapshot, VisualConfig config)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var availableWidth = config.Width ?? ImGui.GetContentRegionAvail().X;
        var fraction = snapshot.Fraction;

        var percentWidth = config.ShowPercentage ? ImGui.CalcTextSize("100%").X + spacing : 0f;
        var barWidth = availableWidth - percentWidth;
        var barHeight = config.Height ?? ImGui.GetFrameHeight();

        var (bgColor, fgColor) = GetProgressColors(snapshot, config);

        // Animação para progresso indeterminado
        if (snapshot.IsIndeterminate)
            fraction = (float)-ImGui.GetTime() * 0.5f;

        using (ImRaii.PushColor(ImGuiCol.FrameBg, bgColor))
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, fgColor))
        {
            ImGuiUtils.ProgressBar(fraction, new Vector2(barWidth, barHeight));
        }

        // Tooltip
        if (ImGui.IsItemHovered() && config.ShowDetailedTooltip)
        {
            DrawSingleTooltip(snapshot);
        }

        // Percentual à direita
        if (config.ShowPercentage && !snapshot.IsIndeterminate)
        {
            ImGui.SameLine(0, spacing);
            ImGui.AlignTextToFramePadding();
            ImGuiUtils.TextRight($"{fraction * 100:N0}%", percentWidth - spacing);
        }
    }

    private static void DrawArcBar(ProgressSnapshot snapshot, VisualConfig config)
    {
        var arcSize = config.Height ?? ImGui.GetFrameHeight() * 3f;
        var (_, fgColor) = GetProgressColors(snapshot, config);

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(arcSize, arcSize));

        if (ImGui.IsItemHovered() && config.ShowDetailedTooltip)
        {
            DrawSingleTooltip(snapshot);
        }

        var dl = ImGui.GetWindowDrawList();
        ImGuiUtils.DrawStatArc(dl, origin, arcSize, snapshot.Fraction, fgColor);

        // Percentual central
        if (config.ShowPercentage)
        {
            var center = origin + new Vector2(arcSize * 0.5f, arcSize * 0.5f);
            var font = ImGui.GetFont();
            var fontSize = arcSize * 0.22f;
            var text = snapshot.IsIndeterminate ? "..." : $"{snapshot.Fraction * 100:0}%";
            var textSize = ImGui.CalcTextSize(text) * (fontSize / ImGui.GetFontSize());
            var textPos = center - textSize * 0.5f;

            dl.AddText(font, fontSize, textPos, ImGui.GetColorU32(fgColor), text);
        }
    }

    private static void DrawCompactBar(ProgressSnapshot snapshot, VisualConfig config)
    {
        var width = config.Width ?? ImGui.GetContentRegionAvail().X;
        var text = snapshot.IsIndeterminate ? "..." : $"{snapshot.Fraction * 100:N0}%";

        ImGui.AlignTextToFramePadding();
        ImGuiUtils.TextCentered(text, width);

        if (ImGui.IsItemHovered() && config.ShowDetailedTooltip)
        {
            DrawSingleTooltip(snapshot);
        }
    }

    private static void DrawStackedBars(IReadOnlyList<ProgressSnapshot> snapshots, VisualConfig config)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var singleConfig = config with { Mode = DisplayMode.Horizontal };

        for (var i = 0; i < snapshots.Count; i++)
        {
            if (i > 0)
                ImGui.Dummy(new Vector2(0, spacing * 0.5f));

            DrawSingle(snapshots[i], singleConfig);
        }
    }

    #endregion

    #region Tooltips

    private static void DrawSingleTooltip(ProgressSnapshot snapshot)
    {
        using var _ = ImRaii.Tooltip();

        if (!string.IsNullOrEmpty(snapshot.CustomTooltip))
        {
            ImGui.TextUnformatted(snapshot.CustomTooltip);
            return;
        }

        var stateText = snapshot.State switch
        {
            ProgressState.Completed => "✓ Concluído",
            ProgressState.Indeterminate => "⏳ Inicializando",
            ProgressState.Cancelled => "✗ Cancelado",
            ProgressState.Failed => "⚠ Falhou",
            _ => "⏳ Em andamento"
        };

        ImGui.TextUnformatted($"{snapshot.Name}: {stateText}");

        if (!snapshot.IsIndeterminate && !snapshot.IsComplete)
        {
            ImGui.TextUnformatted($"Progresso: {snapshot.CurrentValue:N0} / {snapshot.MaxValue:N0}");

            if (snapshot.CurrentValue > snapshot.MaxValue)
            {
                ImGui.Spacing();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                {
                    ImGui.TextWrapped("Isto está demorando mais do que o esperado. Verifique suas configurações.");
                }
            }
        }
    }

    private static void DrawAggregatedTooltip(IReadOnlyList<ProgressSnapshot> snapshots, int completed, int total)
    {
        using var _ = ImRaii.Tooltip();

        ImGui.TextUnformatted($"Progresso Agregado: {completed} de {total} completos ({(float)completed / total * 100:N0}%)");
        ImGui.Separator();

        foreach (var snapshot in snapshots)
        {
            var icon = snapshot.State switch
            {
                ProgressState.Completed => "✓",
                ProgressState.Indeterminate => "⏳",
                ProgressState.Cancelled => "✗",
                ProgressState.Failed => "⚠",
                _ => "⏳"
            };

            var color = snapshot.State switch
            {
                ProgressState.Completed => Colors.Good,
                ProgressState.Failed => Colors.Bad,
                ProgressState.Cancelled => Colors.TextMuted,
                _ => Colors.Progress
            };

            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                if (snapshot.IsIndeterminate)
                {
                    ImGui.TextUnformatted($"{icon} {snapshot.Name}: Inicializando...");
                }
                else if (snapshot.IsComplete)
                {
                    ImGui.TextUnformatted($"{icon} {snapshot.Name}: 100%");
                }
                else
                {
                    var percent = snapshot.Fraction * 100;
                    ImGui.TextUnformatted($"{icon} {snapshot.Name}: {percent:N0}% ({snapshot.CurrentValue}/{snapshot.MaxValue})");
                }
            }
        }
    }

    #endregion

    #region Utilitários de Cores

    private static (Vector4 Background, Vector4 Foreground) GetProgressColors(
        ProgressSnapshot snapshot,
        VisualConfig config)
    {
        // Cores customizadas tem prioridade
        if (config.BackgroundColor.HasValue || config.ForegroundColor.HasValue)
        {
            return (
                config.BackgroundColor ?? ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight)),
                config.ForegroundColor ?? ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Text))
            );
        }

        // Cor custom do snapshot
        if (snapshot.CustomColor.HasValue)
        {
            var bg = ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight));
            return (bg, snapshot.CustomColor.Value);
        }

        // Cores baseadas em estado
        if (snapshot.State == ProgressState.Completed)
        {
            var bg = ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight));
            return (bg, Colors.Good);
        }

        if (snapshot.State == ProgressState.Failed)
        {
            var bg = ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight));
            return (bg, Colors.Bad);
        }

        if (snapshot.State == ProgressState.Cancelled)
        {
            var bg = ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.TableBorderLight));
            return (bg, Colors.TextMuted);
        }

        // Cores baseadas em stage (solver)
        return Colors.GetSolverProgressColors(snapshot.Stage, config.ColorTheme);
    }

    #endregion
}
