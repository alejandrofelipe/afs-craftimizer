// Craftimizer.UI/ImRaii2.cs
using System;
using System.Numerics;

namespace Craftimizer.Utils;

public static partial class ImRaii2
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

    public static RaiiObject TextWrapPos(float wrap_local_pos_x)
    {
        ImGui.PushTextWrapPos(wrap_local_pos_x);
        return new RaiiObject(ImGui.PopTextWrapPos, true, false);
    }
}
