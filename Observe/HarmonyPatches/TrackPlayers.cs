using HarmonyLib;

namespace Marioalexsan.Observe.HarmonyPatches;

[HarmonyPatch(typeof(Player), nameof(Player.Update))]
static class Trackplayers
{
    static void Postfix(Player __instance)
    {
        ObservePlugin.Players.Add(__instance);
    }
}