using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.CraptopMode.SoftDependencies;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.CraptopMode;

//[HarmonyPatch(typeof(MenuElement), nameof(MenuElement.Init_StartElement))]
//static class NoSlicing
//{
//    static void Prefix(MenuElement __instance)
//    {
//        var images = __instance.GetComponentsInChildren<Image>();

//        for (int i = 0; i < images.Length; i++)
//        {
//            if (images[i].type == Image.Type.Sliced)
//                images[i].type = Image.Type.Simple;
//        }
//    }
//}

[HarmonyPatch(typeof(AtlyssNetworkManager), nameof(AtlyssNetworkManager.Console_GetInput))]
static class DisableConsoleInput
{
    static bool Prefix(AtlyssNetworkManager __instance)
    {
        // Optimize: do not poll for Console.KeyAvailable if console input is not needed
        // Polling is more expensive than expected
        if (CraptopMode.OptimizeOSConsoleInput.Value && !__instance._allowConsoleInput)
            return false;

        return true;
    }
}

[HarmonyPatch(typeof(ItemMenuCell), nameof(ItemMenuCell.Handle_CellUpdate))]
static class ItemMenuCellOptimize
{
    static bool Prefix(ItemMenuCell __instance)
    {
        if (!CraptopMode.OptimizeItemMenu.Value)
            return true;

        // Optimize: do not process unnecessary stuff if this is not the selected cell

        return TabMenu._current._isOpen && TabMenu._current._selectedMenuCell == __instance;
    }
}

[HarmonyPatch(typeof(RotateObject), nameof(RotateObject.Update))]
static class RotateObjectOptimize
{
    static bool Prefix(RotateObject __instance)
    {
        if (!CraptopMode.OptimizeObjectRotations.Value)
            return true;

        // Optimize: preserve behaviour while trying to reduce number of operations required

        if (__instance._alwaysRun || !Player._mainPlayer || !Player._mainPlayer._isHostPlayer || !(Player._mainPlayer.gameObject.scene != __instance.gameObject.scene))
        {
            if (__instance._lerpRotation)
            {
                // Compute lerp time once
                //var lerpTime = __instance._lerpTime * Time.deltaTime;
                //__instance._rotX = Mathf.Lerp(__instance._rotX, __instance.endRotX, lerpTime);
                //__instance._rotY = Mathf.Lerp(__instance._rotY, __instance.endRotY, lerpTime);
                //__instance._rotZ = Mathf.Lerp(__instance._rotZ, __instance.endRotZ, lerpTime);

                var lerpTime = __instance._lerpTime * Time.deltaTime;
                lerpTime = lerpTime < 0f ? 0 : lerpTime > 1f ? 1 : lerpTime;

                __instance._rotX = LerpUnclamped(__instance._rotX, __instance.endRotX, lerpTime);
                __instance._rotY = LerpUnclamped(__instance._rotY, __instance.endRotY, lerpTime);
                __instance._rotZ = LerpUnclamped(__instance._rotZ, __instance.endRotZ, lerpTime);
            }

            if (__instance._ignoreX)
                __instance._rotX = __instance.transform.rotation.x;

            if (__instance._ignoreY)
                __instance._rotY = __instance.transform.rotation.y;

            if (__instance._ignoreZ)
                __instance._rotZ = __instance.transform.rotation.z;

            // Compute rotation multiplier once
            var rotTime = 25.5f * Time.deltaTime;

            __instance.transform.Rotate(__instance._rotX * rotTime, __instance._rotY * rotTime, __instance._rotZ * rotTime, Space.Self);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
}

[HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.LateUpdate))]
static class ControlDynamicBoneLateUpdates
{
    static bool Prefix() => CraptopMode.ShouldUpdateBones;
}

[HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.LateUpdate))]
static class ControlDynamicBoneUpdates
{
    static bool Prefix() => CraptopMode.ShouldUpdateBones;
}

[HarmonyPatch(typeof(DialogTrigger), nameof(DialogTrigger.Handle_NametagDistance))]
static class DialogTriggerNametagHandling
{
    static bool Prefix(DialogTrigger __instance)
    {
        // Fast: Nametags disabled
        if (CraptopMode.DialogTriggerPerformance.Value == BasicQualityLevel.Fast)
        {
            if (__instance._nameTagRenderer)
                __instance._nameTagRenderer.enabled = false;

            if (__instance._subNameTagRenderer)
                __instance._subNameTagRenderer.enabled = false;

            return false;
        }

        return true;
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        var targetMethod = AccessTools.Method(typeof(PhysicsScene), nameof(PhysicsScene.Raycast), [typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction)]);
        matcher.MatchForward(false, new CodeMatch(x => x.Calls(targetMethod)));

        if (!matcher.IsValid)
        {
            Logging.LogWarning($"Failed to transpile {nameof(DialogTriggerNametagHandling)}! Please notify the mod developer about this!");
        }
        else
        {
            matcher.RemoveInstruction();
            matcher.Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                CodeInstruction.Call(typeof(DialogTriggerNametagHandling), nameof(Raycast))
                );
        }

        return matcher.InstructionEnumeration();
    }

    private static bool Raycast(ref PhysicsScene scene, Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, DialogTrigger __instance)
    {
        // Optimization: Do the vanilla distance check before the raycast and shortcircuit if needed - this saves on unnecessary raycasts
        if (CraptopMode.OptimizeRaycasts.Value)
        {
            if (Vector3.Distance(__instance._mainPlayer.transform.position, __instance.transform.position) > 20.5f)
            {
                hitInfo = default;
                return false;
            }
        }

        // Balanced: Will only act based on distance and not on distance + visibility
        if (CraptopMode.DialogTriggerPerformance.Value == BasicQualityLevel.Balanced)
        {
            hitInfo = default;
            return false;
        }

        // Fancy: Vanilla behaviour
        return scene.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }
}

[HarmonyPatch(typeof(GroundSnapPosition), nameof(GroundSnapPosition.Update))]
static class GroundSnapPerf
{
    static bool Prefix(GroundSnapPosition __instance)
    {
        if (CraptopMode.GroundSnapPerformance.Value == BasicQualityLevel.Fast)
        {
            __instance._transformObject.gameObject.SetActive(false);
            return false;
        }

        return CraptopMode.ShouldUpdateGroundSnapEffects;
    }
}

[HarmonyPatch]
static class HandleNetNPCYAxis
{
    static MethodInfo TargetMethod() => AccessTools.FirstMethod(typeof(NetNPC), x => x.Name.Contains("Handle_YAxisPosition"));

    static bool Prefix() => CraptopMode.ShouldUpdateNPCYAxis;
}

[HarmonyPatch(typeof(Billboard), nameof(Billboard.LateUpdate))]
static class BillboardsPerf
{
    static bool Prefix() => CraptopMode.ShouldUpdateBillboards;
}

[HarmonyPatch(typeof(PlayerVisual), nameof(PlayerVisual.Handle_WaterRipplingEffect))]
static class PlayerWaterRipple
{
    // Balanced: Skip some update cycles
    static bool Prefix() => CraptopMode.ShouldUpdateWaterRipples;

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        var targetMethod = AccessTools.Method(typeof(PhysicsScene), nameof(PhysicsScene.Raycast), [typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction)]);
        matcher.MatchForward(false, new CodeMatch(x => x.Calls(targetMethod)));

        if (!matcher.IsValid)
        {
            Logging.LogWarning($"Failed to transpile {nameof(DialogTriggerNametagHandling)}! Please notify the mod developer about this!");
        }

        var raycastPosition = matcher.Pos;

        targetMethod = AccessTools.Method(typeof(PhysicsScene), nameof(PhysicsScene.SphereCast), [typeof(Vector3), typeof(float), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction)]);
        matcher.MatchForward(false, new CodeMatch(x => x.Calls(targetMethod)));

        if (!matcher.IsValid)
        {
            Logging.LogWarning($"Failed to transpile {nameof(DialogTriggerNametagHandling)}! Please notify the mod developer about this!");
        }

        var sphereRaycastPosition = matcher.Pos;

        // Replace in reverse order

        matcher.Start().Advance(sphereRaycastPosition);
        matcher.RemoveInstruction();
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.Call(typeof(PlayerWaterRipple), nameof(SphereCast))
            );

        matcher.Start().Advance(raycastPosition);
        matcher.RemoveInstruction();
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.Call(typeof(PlayerWaterRipple), nameof(Raycast))
            );

        return matcher.InstructionEnumeration();
    }

    private static bool Raycast(ref PhysicsScene scene, Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, PlayerVisual __instance)
    {
        // Optimization: Do the vanilla water check before the raycast and shortcircuit if needed - this saves on unnecessary raycasts
        if (CraptopMode.OptimizeRaycasts.Value)
        {
            if (!__instance._pWater._isInWater)
            {
                hitInfo = default;
                return false;
            }
        }

        // Fast: Ripple detection disabled
        if (CraptopMode.WaterRipplePerformance.Value <= BasicQualityLevel.Fast)
        {
            hitInfo = default;
            return false;
        }

        // Fancy / Balanced: Vanilla behaviour
        return scene.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    private static bool SphereCast(ref PhysicsScene scene, Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, PlayerVisual __instance)
    {
        // Optimization: Do the vanilla water check before the raycast and shortcircuit if needed - this saves on unnecessary raycasts
        if (CraptopMode.OptimizeRaycasts.Value)
        {
            if (__instance._pWater._isInWater)
            {
                hitInfo = default;
                return false;
            }
        }

        // Fast: Ripple detection disabled
        if (CraptopMode.WaterRipplePerformance.Value <= BasicQualityLevel.Fast)
        {
            hitInfo = default;
            return false;
        }

        // Fancy / Balanced: Vanilla behaviour
        return scene.SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }
}

public enum BasicQualityLevel
{
    Fast,
    Balanced,
    Fancy,
}

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
public class CraptopMode : BaseUnityPlugin
{
    public static CraptopMode Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(CraptopMode)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static CraptopMode? _plugin;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    public static ConfigEntry<BasicQualityLevel> DynamicBonePerformance { get; private set; } = null!;
    public static ConfigEntry<BasicQualityLevel> DialogTriggerPerformance { get; private set; } = null!;
    public static ConfigEntry<BasicQualityLevel> GroundSnapPerformance { get; private set; } = null!;
    public static ConfigEntry<BasicQualityLevel> NPCNavigationPerformance { get; private set; } = null!;
    public static ConfigEntry<BasicQualityLevel> WaterRipplePerformance { get; private set; } = null!;
    public static ConfigEntry<BasicQualityLevel> BillboardsPerformance { get; private set; } = null!;

    public static ConfigEntry<bool> OptimizeRaycasts { get; private set; } = null!;
    public static ConfigEntry<bool> OptimizeObjectRotations { get; private set; } = null!;
    public static ConfigEntry<bool> OptimizeItemMenu { get; private set; } = null!;
    public static ConfigEntry<bool> OptimizeOSConsoleInput { get; private set; } = null!;

    public static bool ShouldUpdateBones { get; private set; } = true;
    private static int _boneUpdatesCounter;

    public static bool ShouldUpdateGroundSnapEffects { get; private set; } = true;
    private static int _groundSnapCounter;

    public static bool ShouldUpdateNPCYAxis { get; private set; } = true;
    private static int _npcYAxisCounter;

    public static bool ShouldUpdateWaterRipples { get; private set; } = true;
    private static int _waterRipplesCounter;

    public static bool ShouldUpdateBillboards { get; private set; } = true;
    private static int _billboardsCounter;

    public CraptopMode()
    {
        _plugin = this;
        Logger = base.Logger;

        OptimizeRaycasts = Config.Bind("Performance", nameof(OptimizeRaycasts), true, "(Safe) Optimize raycasts in places where they're done inefficiently.");
        OptimizeObjectRotations = Config.Bind("Performance", nameof(OptimizeObjectRotations), true, "(Safe) Optimize RotateObject to run faster.");
        OptimizeItemMenu = Config.Bind("Performance", nameof(OptimizeItemMenu), true, "(Safe) Optimize the player item menu to not process stuff while inactive.");
        OptimizeOSConsoleInput = Config.Bind("Performance", nameof(OptimizeOSConsoleInput), true, "(Safe) Force network server logic to not read from the OS console.");

        DynamicBonePerformance = Config.Bind("Performance", nameof(DynamicBonePerformance), BasicQualityLevel.Fancy, "Selects quality level of dynamic bones. Faster options might cause glitches with jiggles, tails, ears, etc.");
        DialogTriggerPerformance = Config.Bind("Performance", nameof(DialogTriggerPerformance), BasicQualityLevel.Fancy, "Selects quality level for dialog triggers (i.e. NPCs). Faster options might cause glitches or remove name plates for NPCs.");
        GroundSnapPerformance = Config.Bind("Performance", nameof(GroundSnapPerformance), BasicQualityLevel.Fancy, "Selects quality level for ground snap effects (i.e. shadows and other stuff). Faster options might degrade visual accuracy or disable them completely.");
        NPCNavigationPerformance = Config.Bind("Performance", nameof(NPCNavigationPerformance), BasicQualityLevel.Fancy, "Selects quality level for NPC navigation. Faster options might degrade visual accuracy or cause glitches in behaviour.");
        WaterRipplePerformance = Config.Bind("Performance", nameof(WaterRipplePerformance), BasicQualityLevel.Fancy, "Selects quality level for water ripples. Faster options might degrade accuracy or disable ripples completely.");
        BillboardsPerformance = Config.Bind("Performance", nameof(BillboardsPerformance), BasicQualityLevel.Fancy, "Selects quality level for billboards. Faster options might make billboards appear laggy.");
    }

    public void Awake()
    {
        _harmony.PatchAll();
        if (EasySettings.IsAvailable)
        {
            EasySettings.OnApplySettings.AddListener(() =>
            {
                try
                {
                    Config.Save();

                    // Some patches require applying them again to refresh their state
                    _harmony.UnpatchSelf();
                    _harmony.PatchAll();
                }
                catch (Exception e)
                {
                    Logging.LogError($"ModAudio crashed in OnApplySettings! Please report this error to the mod developer:");
                    Logging.LogError(e.ToString());
                }
            });
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddHeader(ModInfo.NAME);
                EasySettings.AddToggle("Optimize raycasts", OptimizeRaycasts);
                EasySettings.AddToggle("Optimize object rotations", OptimizeObjectRotations);
                EasySettings.AddToggle("Optimize item menu", OptimizeItemMenu);
                EasySettings.AddToggle("Force OS console input off", OptimizeOSConsoleInput);
                EasySettings.AddDropdown("Dynamic bones", DynamicBonePerformance);
                EasySettings.AddDropdown("Dialog trigger name plates", DialogTriggerPerformance);
                EasySettings.AddDropdown("Ground snap effects", GroundSnapPerformance);
                EasySettings.AddDropdown("NPC navigation", NPCNavigationPerformance);
                EasySettings.AddDropdown("Billboards", BillboardsPerformance);
            });
        }
    }

    public void LateUpdate()
    {
        ShouldUpdateBones = UpdateCounter(ref _boneUpdatesCounter, DynamicBonePerformance.Value switch
        {
            BasicQualityLevel.Fancy => 1,
            BasicQualityLevel.Balanced => 2,
            BasicQualityLevel.Fast => int.MaxValue,
            _ => 1,
        });

        ShouldUpdateGroundSnapEffects = UpdateCounter(ref _groundSnapCounter, GroundSnapPerformance.Value switch
        {
            BasicQualityLevel.Fancy => 1,
            BasicQualityLevel.Balanced => 2,
            BasicQualityLevel.Fast => int.MaxValue,
            _ => 1,
        });

        ShouldUpdateNPCYAxis = UpdateCounter(ref _npcYAxisCounter, NPCNavigationPerformance.Value switch
        {
            BasicQualityLevel.Fancy => 1,
            BasicQualityLevel.Balanced => 4,
            BasicQualityLevel.Fast => 8,
            _ => 1,
        });

        ShouldUpdateWaterRipples = UpdateCounter(ref _waterRipplesCounter, WaterRipplePerformance.Value switch
        {
            BasicQualityLevel.Fancy => 1,
            BasicQualityLevel.Balanced => 2,
            BasicQualityLevel.Fast => 4,
            _ => 1,
        });

        ShouldUpdateBillboards = UpdateCounter(ref _billboardsCounter, BillboardsPerformance.Value switch
        {
            BasicQualityLevel.Fancy => 1,
            BasicQualityLevel.Balanced => 4,
            BasicQualityLevel.Fast => 8,
            _ => 1,
        });
    }

    private static bool UpdateCounter(ref int counter, int counterTick)
    {
        counter++;

        if (counter >= counterTick)
        {
            counter -= counterTick;
            return true;
        }
        else
        {
            return false;
        }
    }
}