// Craftimizer.UI/ImRaiiShim.cs
// Provides ImRaii RAII helpers equivalent to Dalamud.Interface.Utility.Raii.ImRaii.
// Put in ImGuiNET namespace so 'global using ImGuiNET' makes it available project-wide.
using System;
using System.Numerics;

namespace ImGuiNET;

public static class ImRaii
{
    public struct FontDisposable : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { ImGui.PopFont(); _disposed = true; } }
    }

    public struct ColorDisposable : IDisposable
    {
        private readonly int _count;
        private bool _disposed;
        internal ColorDisposable(int count) { _count = count; _disposed = false; }
        public static implicit operator bool(ColorDisposable _) => true;
        public void Dispose() { if (!_disposed) { ImGui.PopStyleColor(_count); _disposed = true; } }
    }

    public struct StyleDisposable : IDisposable
    {
        private readonly int _count;
        private bool _disposed;
        internal StyleDisposable(int count) { _count = count; _disposed = false; }
        public void Dispose() { if (!_disposed) { ImGui.PopStyleVar(_count); _disposed = true; } }
    }

    public struct IdDisposable : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { ImGui.PopID(); _disposed = true; } }
    }

    public struct GroupDisposable : IDisposable
    {
        private bool _disposed;
        public static implicit operator bool(GroupDisposable _) => true;
        public void Dispose() { if (!_disposed) { ImGui.EndGroup(); _disposed = true; } }
    }

    public struct TooltipDisposable : IDisposable
    {
        private bool _disposed;
        public static implicit operator bool(TooltipDisposable _) => true;
        public void Dispose() { if (!_disposed) { ImGui.EndTooltip(); _disposed = true; } }
    }

    public static FontDisposable PushFont(ImFontPtr font)
    {
        ImGui.PushFont(font);
        return new FontDisposable();
    }

    public static ColorDisposable PushColor(ImGuiCol idx, Vector4 col)
    {
        ImGui.PushStyleColor(idx, col);
        return new ColorDisposable(1);
    }

    public static ColorDisposable PushColor(ImGuiCol idx, uint col)
    {
        ImGui.PushStyleColor(idx, col);
        return new ColorDisposable(1);
    }

    public static StyleDisposable PushStyle(ImGuiStyleVar idx, float val)
    {
        ImGui.PushStyleVar(idx, val);
        return new StyleDisposable(1);
    }

    public static StyleDisposable PushStyle(ImGuiStyleVar idx, Vector2 val)
    {
        ImGui.PushStyleVar(idx, val);
        return new StyleDisposable(1);
    }

    public static IdDisposable PushId(string id)
    {
        ImGui.PushID(id);
        return new IdDisposable();
    }

    public static IdDisposable PushId(int id)
    {
        ImGui.PushID(id);
        return new IdDisposable();
    }

    public static GroupDisposable Group()
    {
        ImGui.BeginGroup();
        return new GroupDisposable();
    }

    public static TooltipDisposable Tooltip()
    {
        ImGui.BeginTooltip();
        return new TooltipDisposable();
    }
}
