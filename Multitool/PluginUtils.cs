using BepInEx;
using BepInEx.Bootstrap;

namespace Marioalexsan.Multitool;

internal static class PluginUtils
{
    public static bool ValidatePlugin(BaseUnityPlugin plugin, out string guid)
    {
        var pluginInfo = Chainloader.PluginInfos.FirstOrDefault(x => x.Value.Instance == plugin);

        if (pluginInfo.Value is null)
        {
            Logging.LogWarning($"Couldn't validate that calling mod exists in the Chainloader.");
            guid = "";
            return false;
        }

        guid = pluginInfo.Key;
        return true;
    }
}
