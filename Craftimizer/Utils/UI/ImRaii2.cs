using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using System;
using System.Numerics;

namespace Craftimizer.Utils;

public static class ImRaii2
{
    public struct RaiiObject : IDisposable
    {
        private readonly Action _endAction;
        private readonly bool _conditionalEnd;
        public readonly bool Success;
        private bool _disposed;

        internal RaiiObject(Action endAction, bool success, bool conditionalEnd)
        {
            _endAction = endAction;
            _conditionalEnd = conditionalEnd;
            Success = success;
            _disposed = false;
        }

        public static implicit operator bool(RaiiObject obj) => obj.Success;
        public static bool operator true(RaiiObject obj) => obj.Success;
        public static bool operator false(RaiiObject obj) => !obj.Success;
        public static bool operator !(RaiiObject obj) => !obj.Success;

        public void Dispose()
        {
            if (!_disposed)
            {
                if (!_conditionalEnd || Success)
                    _endAction();
                _disposed = true;
            }
        }
    }

    public static RaiiObject GroupPanel(string name, float width, out float internalWidth, bool accentLabel = true)
    {
        internalWidth = ImGuiUtils.BeginGroupPanel(name, width, accentLabel);
        return new RaiiObject(ImGuiUtils.EndGroupPanel, true, false);
    }

    public static RaiiObject Plot(string title_id, Vector2 size, ImPlotFlags flags)
    {
        var success = ImPlot.BeginPlot(title_id, size, flags);
        return new RaiiObject(ImPlot.EndPlot, success, true);
    }

    public static RaiiObject PushStyle(ImPlotStyleVar idx, Vector2 val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new RaiiObject(ImPlot.PopStyleVar, true, false);
    }

    public static RaiiObject PushStyle(ImPlotStyleVar idx, float val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new RaiiObject(ImPlot.PopStyleVar, true, false);
    }

    public static RaiiObject PushColor(ImPlotCol idx, Vector4 col)
    {
        ImPlot.PushStyleColor(idx, col);
        return new RaiiObject(ImPlot.PopStyleColor, true, false);
    }

    public static RaiiObject TextWrapPos(float wrap_local_pos_x)
    {
        ImGui.PushTextWrapPos(wrap_local_pos_x);
        return new RaiiObject(ImGui.PopTextWrapPos, true, false);
    }
}
