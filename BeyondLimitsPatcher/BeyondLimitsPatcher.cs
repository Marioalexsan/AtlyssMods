using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Lilly_s_Beyond_Limits;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.BeyondLimitsPatcher;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("0a26c5bd-f173-47f8-8f50-006dd6806ce6")] // https://thunderstore.io/c/atlyss/p/ButteredLilly/ButteredLillysBeyondLimits
public class BeyondLimitsPatcher : BaseUnityPlugin
{
    public static BeyondLimitsPatcher Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(BeyondLimitsPatcher)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static BeyondLimitsPatcher? _plugin;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    internal static ConfigEntry<bool> DisableFog { get; private set; } = null!;
    internal static ConfigEntry<bool> SuperFarRenderDistance { get; private set; } = null!;

    public BeyondLimitsPatcher()
    {
        _plugin = this;
        Logger = base.Logger;
        DisableFog = Config.Bind("General", "DisableFog", true, "By default, BeyondLimits disables fog in-game. Set this to false if you want to restore the fog.");
        SuperFarRenderDistance = Config.Bind("General", "SuperFarRenderDistance", true, "By default, BeyondLimits forces the render distance to 10x the value of 'Very Far'. Set this to false if you want to keep vanilla behaviour.");
    }

    public void Awake()
    {
        _harmony.PatchAll();
        if (EasySettings.IsAvailable)
        {
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddHeader("BeyondLimitsPatcher");
                EasySettings.AddToggle("Disable Fog", DisableFog);
                EasySettings.AddToggle("Super Far Render Distance", SuperFarRenderDistance);
            });
            EasySettings.OnApplySettings.AddListener(() =>
            {
                Config.Save();
            });
        }
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


static class ConfigureFog
{
    [HarmonyPatch(typeof(BeyondCore.cameraFog), nameof(BeyondCore.cameraFog.Prefix))]
    static class NoMessingAround
    {
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CameraFogDensity), nameof(CameraFogDensity.OnPreRender))]
    static class Before
    {
        static void Prefix(CameraFogDensity __instance)
        {
            if (BeyondLimitsPatcher.DisableFog.Value)
            {
                RenderSettings.fogDensity = 0.000001f;
                __instance.fogDensity = 0.000001f;
            }
        }
    }

    //[HarmonyPatch(typeof(CameraFogDensity), nameof(CameraFogDensity.OnPostRender))]
    //static class After
    //{
    //    static void Postfix(CameraFogDensity __instance)
    //    {
    //        if (BeyondLimitsPatcher.DisableFog.Value)
    //            __instance.fogDensity = __instance.previousFogDensity;
    //    }
    //}

    [HarmonyPatch(typeof(BeyondCore.cameraHeight), nameof(BeyondCore.cameraHeight.Prefix))]
    static class DontChangeTheFarClipPlane
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            var matcher = new CodeMatcher(code);

            matcher.MatchForward(false,
                new CodeMatch(x => x.Calls(AccessTools.PropertySetter(typeof(Camera), nameof(Camera.farClipPlane))))
                );

            matcher.SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DontChangeTheFarClipPlane), nameof(ManipulateFarClipPlane))));

            return matcher.InstructionEnumeration();
        }

        static void ManipulateFarClipPlane(float value)
        {
            if (BeyondLimitsPatcher.SuperFarRenderDistance.Value)
                CameraFunction._current._mainCamera.farClipPlane = value;

            // Leave unchanged otherwise
        }
    }
}