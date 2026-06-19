using System;
using System.Collections.Generic;

namespace Artificer.Utils;

public static partial class ImGuiUtils
{
    // Largura de conteúdo do painel ativo. BeginGroupPanel empilha sua largura
    // interna; EndGroupPanel desempilha. É um stack para suportar painéis aninhados.
    // Valores <= 0 (painel size-to-content) são tratados como "sem constraint".
    private static readonly Stack<float> ContentWidthStack = new();

    /// <summary>Largura interna do painel ativo, ou null fora de painel / painel sem largura fixa.</summary>
    public static float? CurrentContentWidth =>
        ContentWidthStack.Count > 0 && ContentWidthStack.Peek() > 0f
            ? ContentWidthStack.Peek()
            : null;

    internal static void PushContentWidth(float width) => ContentWidthStack.Push(width);

    internal static void PopContentWidth()
    {
        if (ContentWidthStack.Count > 0)
            ContentWidthStack.Pop();
    }

    /// <summary>Esvazia o stack. Apenas para testes (estado estático compartilhado).</summary>
    internal static void DrainContentWidthForTests() => ContentWidthStack.Clear();

    /// <summary>
    /// Resolução pura (testável sem contexto ImGui): largura explícita vence; senão, dentro
    /// de um painel, o MENOR entre a largura do painel e o <paramref name="fallback"/> (o
    /// content region atual) — assim o conteúdo não estoura nem o painel nem uma célula de
    /// tabela/child mais estreita; fora de painel, o <paramref name="fallback"/>.
    /// Convenção do projeto: <paramref name="explicitWidth"/> == 0f (default) significa
    /// "sem override explícito" — passar 0f como largura intencional não é suportado.
    /// </summary>
    internal static float ResolveAvailWidth(float explicitWidth, float fallback) =>
        explicitWidth != default ? explicitWidth
        : (CurrentContentWidth is { } w ? MathF.Min(w, fallback) : fallback);

    /// <summary>Versão de produção: lê o content region preguiçosamente só quando necessário.</summary>
    private static float ResolveAvailWidth(float explicitWidth)
    {
        if (explicitWidth != default)
            return explicitWidth;
        var avail = ImGui.GetContentRegionAvail().X;
        return CurrentContentWidth is { } w ? MathF.Min(w, avail) : avail;
    }
}
