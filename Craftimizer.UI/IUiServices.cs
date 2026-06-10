// Craftimizer.UI/IUiServices.cs
using System;

namespace Craftimizer.Utils;

public interface IUiServices
{
    float GlobalScale { get; }
    ImFontPtr IconFont { get; }
    void OpenLink(string url);
}

public static class UiServices
{
    private static IUiServices? _current;

    public static IUiServices Current
    {
        get => _current ?? throw new InvalidOperationException("UiServices.Current não foi inicializado. Atribua antes do primeiro frame.");
        set => _current = value;
    }

    // For testing only
    internal static void Reset() => _current = null;
}
