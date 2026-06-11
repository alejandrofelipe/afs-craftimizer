using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Thin wrapper around the Teleporter plugin's IPC "Teleport" gate.
/// Availability is only known for sure once an invocation succeeds or fails.
/// </summary>
public sealed class TeleportHelper : IDisposable
{
    private ICallGateSubscriber<uint, byte, bool>? _ipc;

    public bool IsAvailable { get; private set; }

    public TeleportHelper(IDalamudPluginInterface pi)
    {
        try
        {
            _ipc = pi.GetIpcSubscriber<uint, byte, bool>("Teleport");
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public bool TeleportTo(uint aetheryteId, byte subIndex = 0)
    {
        if (_ipc == null)
            return false;
        try
        {
            return _ipc.InvokeFunc(aetheryteId, subIndex);
        }
        catch
        {
            IsAvailable = false;
            return false;
        }
    }

    public void Dispose() => _ipc = null;
}
