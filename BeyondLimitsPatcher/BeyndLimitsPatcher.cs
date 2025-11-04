using BepInEx;
using HarmonyLib;
using Lilly_s_Beyond_Limits;
using UnityEngine;

namespace Marioalexsan.BeyondLimitsPatcher;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("0a26c5bd-f173-47f8-8f50-006dd6806ce6")] // https://thunderstore.io/c/atlyss/p/ButteredLilly/ButteredLillysBeyondLimits
public class BeyondLimisPatcher : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    public void Awake()
    {
        _harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(BeyondCore), nameof(BeyondCore.findPlayer))]
static class DoNotPatchPlayerNames
{
    static bool Prefix() => false;
}


[HarmonyPatch(typeof(BeyondCore), nameof(BeyondCore.onMessage))]
static class DoNotAcceptCustomNamesOverNetwork
{
    static bool Prefix() => false;
}

[HarmonyPatch(typeof(BeyondCore.lillyCred), nameof(BeyondCore.lillyCred.Prefix))]
static class DoNotSendCustomNamesOverNetwork
{
    static bool Prefix() => false;
}