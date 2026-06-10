using Dalamud.Bindings.ImPlot;
using System.Numerics;

namespace Craftimizer.Utils;

internal static class ImRaiiPlot
{
    public static ImRaii2.RaiiObject Plot(string title_id, Vector2 size, ImPlotFlags flags)
    {
        var success = ImPlot.BeginPlot(title_id, size, flags);
        return new ImRaii2.RaiiObject(ImPlot.EndPlot, success, true);
    }

    public static ImRaii2.RaiiObject PushStyle(ImPlotStyleVar idx, Vector2 val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new ImRaii2.RaiiObject(ImPlot.PopStyleVar, true, false);
    }

    public static ImRaii2.RaiiObject PushStyle(ImPlotStyleVar idx, float val)
    {
        ImPlot.PushStyleVar(idx, val);
        return new ImRaii2.RaiiObject(ImPlot.PopStyleVar, true, false);
    }

    public static ImRaii2.RaiiObject PushColor(ImPlotCol idx, Vector4 col)
    {
        ImPlot.PushStyleColor(idx, col);
        return new ImRaii2.RaiiObject(ImPlot.PopStyleColor, true, false);
    }
}
