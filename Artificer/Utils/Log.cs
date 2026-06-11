using Artificer.Plugin;
using System;

namespace Artificer.Utils;

public static class Log
{
    public static void Debug(string line) => Service.PluginLog.Debug(line);

    public static void Warning(string line) => Service.PluginLog.Warning(line);
    public static void Warning(Exception e, string line) => Service.PluginLog.Warning(e, line);
    public static void Error(string line) => Service.PluginLog.Error(line);
    public static void Error(Exception e, string line) => Service.PluginLog.Error(e, line);
}
