using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib.Tools;
using Homebrewery.Code.Component;

namespace Marioalexsan.Observe;

public static class CameraChecks
{
    private static bool _homebreweryIsFaulty;
    
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool IsUsingFreecam()
    {
        if (Chainloader.PluginInfos.ContainsKey("Homebrewery") && !_homebreweryIsFaulty)
        {
            try
            {
                if (HomebreweryFreecamActive())
                    return true;
            }
            catch (Exception e)
            {
                _homebreweryIsFaulty = true;
                ObservePlugin.Logger.LogWarning("Failed to get Homebrewery's freecam state! Please report this to Observe's mod author!");
                ObservePlugin.Logger.LogWarning($"Exception: {e}");
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool HomebreweryFreecamActive() => CameraDX._current && CameraDX._current._freecam;
}