using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.AFKConfig.SoftDependencies;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.AFKConfig;

[HarmonyPatch]
static class PlayerAFKTimer
{
    static MethodInfo TargetMethod() => AccessTools.FirstMethod(typeof(Player), x => x.Name.Contains("Handle_AFKCondition"));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        while (matcher.MatchForward(false, new CodeMatch(x => x.LoadsConstant(125f))).IsValid)
            matcher.SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(PlayerAFKTimer), nameof(GetAFKTimer)));

        matcher.Start();

        while (matcher.MatchForward(false, new CodeMatch(x => x.LoadsConstant(126f))).IsValid)
            matcher.SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(PlayerAFKTimer), nameof(GetAFKTimerPlusOne)));

        matcher.Start();

        while (matcher.MatchForward(false, new CodeMatch(x => x.Calls(AccessTools.Method(typeof(Player), nameof(Player.Cmd_InitAfkCondition))))).IsValid)
            matcher.SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(PlayerAFKTimer), nameof(InitAfkCondition)));

        return matcher.InstructionEnumeration();
    }


    static float GetAFKTimer() => AFKConfig.GetAFKTimer();
    static float GetAFKTimerPlusOne() => AFKConfig.GetAFKTimer() + 1;

    static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    static void InitAfkCondition(Player player, bool goAfk)
    {
        bool significantKeyPressed = false;

        if (AFKConfig.AllowTabbingOut.Value)
        {
            for (int i = 0; i < AllKeys.Length; i++)
            {
                var key = AllKeys[i];

                switch (key)
                {
                    case KeyCode.LeftMeta:
                    case KeyCode.RightMeta:
                    case KeyCode.LeftAlt:
                    case KeyCode.RightAlt:
                        continue;
                    case KeyCode.Tab:
                        if (Input.GetKey(KeyCode.Tab) && !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
                        {
                            significantKeyPressed = true;
                            break;
                        }
                        else continue;
                }

                if (Input.GetKey(key))
                {
                    significantKeyPressed = true;
                    break;
                }
            }
        }
        else
        {
            significantKeyPressed = Input.anyKey;
        }

        if (goAfk)
        {
            player.Cmd_InitAfkCondition(true);

            if (!AFKConfig.SitDownOnAFK.Value)
                AFKConfig.ShouldSendIdleAnim = true;
        }
        else
        {
            if (significantKeyPressed)
            {
                player.Cmd_InitAfkCondition(false);

                if (!AFKConfig.StandUpFromAFK.Value)
                    AFKConfig.ShouldSendSitAnim = true;
            }
        }
    }
}


[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
public class AFKConfig : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");
    internal new static ManualLogSource Logger { get; private set; } = null!;

    public static ConfigEntry<float> AFKTimer { get; private set; } = null!;
    public static ConfigEntry<bool> AFKEnabled { get; private set; } = null!;
    public static ConfigEntry<bool> AllowTabbingOut { get; private set; } = null!;
    public static ConfigEntry<bool> SitDownOnAFK { get; private set; } = null!;
    public static ConfigEntry<bool> StandUpFromAFK { get; private set; } = null!;

    public static float GetAFKTimer() => AFKEnabled.Value ? AFKTimer.Value * 60 : 10000000;
    public static bool ShouldSendSitAnim { get; set; }
    public static bool ShouldSendIdleAnim { get; set; }

    public AFKConfig()
    {
        Logger = base.Logger;
        _harmony.PatchAll();

        AFKTimer = Config.Bind("General", "AFKTimer", 2f, new ConfigDescription("The time in minutes before AFK mode is activated.", new AcceptableValueRange<float>(0.5f, 60f)));
        AFKEnabled = Config.Bind("General", "AFKEnabled", true, "Enable or disable the AFK mechanic.");
        AllowTabbingOut = Config.Bind("General", "AllowTabbingOut", false, "Prevents AFK state from being modified when alt-tabbing or pressing the Windows (Meta) key.");
        SitDownOnAFK = Config.Bind("General", "SitDownOnAFK", true, "If true, the character will sit down when entering AFK.");
        StandUpFromAFK = Config.Bind("General", "StandUpFromAFK", true, "If true, the character will stand up when exiting AFK.");
    }

    public void Awake()
    {
        if (EasySettings.IsAvailable)
        {
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddToggle("AFK Enabled", AFKEnabled);
                EasySettings.AddAdvancedSlider("AFK Timer (minutes)", AFKTimer);
                EasySettings.AddToggle("Allow Tabbing Out", AllowTabbingOut);
                EasySettings.AddToggle("Sit Down On AFK", SitDownOnAFK);
                EasySettings.AddToggle("Stand Up From AFK", StandUpFromAFK);
            });
            EasySettings.OnApplySettings.AddListener(() =>
            {
                Config.Save();
            });
        }
    }

    public void Update()
    {
        if (ShouldSendSitAnim && Player._mainPlayer)
        {
            ShouldSendSitAnim = false;
            Player._mainPlayer._pVisual.Send_CrossFadeAnim("sit", 0f, 11, LatencyCheck.IGNORE_LATENCY);
        }

        if (ShouldSendIdleAnim && Player._mainPlayer)
        {
            ShouldSendIdleAnim = false;
            Player._mainPlayer._pVisual.Send_CrossFadeAnim("Idle", 0f, 11, LatencyCheck.IGNORE_LATENCY);
        }
    }
}