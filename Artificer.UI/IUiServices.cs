// Artificer.UI/IUiServices.cs
using System;
using System.Numerics;

namespace Artificer.Utils;

// Neutral enum — only the style vars actually used by Artificer.UI.
// Each IUiServices impl maps these to the correct platform-specific enum value.
// Add a new entry here only if you need to PushStyleVar in Artificer.UI.
public enum ImGuiStyleVarId
{
    WindowPadding,  // Vector2
    FrameRounding,  // float
    ChildRounding,  // float
    FramePadding,   // Vector2
    ItemSpacing,    // Vector2
}

public interface IUiServices
{
    float GlobalScale { get; }
    ImFontPtr IconFont { get; }
    ImFontPtr DefaultFont { get; }
    void OpenLink(string url);

    // PushStyleVar routes through here so each runtime uses its own correct enum values.
    // Never call ImGui.PushStyleVar directly in Artificer.UI — use this instead.
    void PushStyleVar(ImGuiStyleVarId var, float val);
    void PushStyleVar(ImGuiStyleVarId var, Vector2 val);
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
