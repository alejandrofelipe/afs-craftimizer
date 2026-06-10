using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using System.Numerics;

namespace Craftimizer.Utils;

public static partial class ImRaii2
{
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
}
