using BepInEx.Configuration;
using BepInEx.Logging;

namespace Marioalexsan.ChatMacros;

internal static class Logging
{
    public static void LogFatal(object data, ConfigEntry<bool>? toggle = null) => Log(data, LogLevel.Fatal, toggle);
    public static void LogError(object data, ConfigEntry<bool>? toggle = null) => Log(data, LogLevel.Error, toggle);
    public static void LogWarning(object data, ConfigEntry<bool>? toggle = null) => Log(data, LogLevel.Warning, toggle);
    public static void LogMessage(object data, ConfigEntry<bool>? toggle = null) => Log(data, LogLevel.Message, toggle);
    public static void LogInfo(object data, ConfigEntry<bool>? toggle = null) => Log(data, LogLevel.Info, toggle);
    public static void LogDebug(object data, ConfigEntry<bool>? toggle = null) => Log(data, LogLevel.Debug, toggle);

    private static ManualLogSource InternalLogger => ChatMacros.Plugin.Logger;

    private static void Log(object data, LogLevel level = LogLevel.Info, ConfigEntry<bool>? toggle = null)
    {
        if (toggle != null && !toggle.Value)
            return;

        InternalLogger?.Log(level, data);
    }
}
