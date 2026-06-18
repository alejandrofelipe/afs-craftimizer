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
    /// Resolução pura (testável sem contexto ImGui): largura explícita vence; senão a
    /// largura do painel ativo; senão o <paramref name="fallback"/> fornecido pelo chamador.
    /// </summary>
    internal static float ResolveAvailWidth(float explicitWidth, float fallback) =>
        explicitWidth != default ? explicitWidth : (CurrentContentWidth ?? fallback);

    /// <summary>Versão de produção: lê o content region preguiçosamente só quando necessário.</summary>
    private static float ResolveAvailWidth(float explicitWidth)
    {
        if (explicitWidth != default)
            return explicitWidth;
        return CurrentContentWidth ?? ImGui.GetContentRegionAvail().X;
    }
}
