using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.ChatMacros.SoftDependencies;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.ChatMacros;

[HarmonyPatch(typeof(ChatBehaviour), nameof(ChatBehaviour.Send_ChatMessage))]
static class DisableReturnRequirement
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        matcher.MatchForward(false, new CodeMatch(x => x.Calls(AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), [typeof(KeyCode)]))));

        if (!matcher.IsValid)
            throw new InvalidOperationException("Couldn't patch Send_ChatMessage! Please notify the mod developer about this!");

        matcher.Set(OpCodes.Call, AccessTools.Method(typeof(DisableReturnRequirement), nameof(GetKeyDownMixin)));

        return matcher.InstructionEnumeration();
    }

    static bool GetKeyDownMixin(KeyCode code)
    {
        if (SkipReturnCheck && code == KeyCode.Return)
            return true;

        return Input.GetKeyDown(code);
    }

    internal static bool SkipReturnCheck { get; set; } = false;
}

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
public class ChatMacros : BaseUnityPlugin
{
    public static ChatMacros Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(ChatMacros)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static ChatMacros? _plugin;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    public static ConfigEntry<bool> Enabled { get; private set; } = null!;
    public static List<ConfigEntry<string>> MacroTexts { get; private set; } = [];
    public static List<ConfigEntry<KeyCode>> MacroButtons { get; private set; } = [];

    public ChatMacros()
    {
        _plugin = this;
        Logger = base.Logger;
        _harmony.PatchAll();

        Enabled = Config.Bind("General", "Enabled", true, "Enable or disable all keybindings for this mod");

        for (KeyCode key = KeyCode.Keypad1; key <= KeyCode.Keypad9; key++)
        {
            int index = key - KeyCode.Keypad1 + 1;

            MacroButtons.Add(Config.Bind("MacroBindings", $"Macro{index}Button", key, $"Button to press to trigger Macro {index}"));
            MacroTexts.Add(Config.Bind("MacroTexts", $"Macro{index}", "", $"Chat message or command to send when Macro {index} is triggered"));
        }
    }

    public void Awake()
    {
        if (EasySettings.IsAvailable)
        {
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddHeader(ModInfo.NAME);
                EasySettings.AddToggle("Enabled", Enabled);

                for (int i = 0; i < MacroButtons.Count; i++)
                {
                    EasySettings.AddTextField($"Macro {i + 1}", MacroTexts[i]);
                    EasySettings.AddKeyButton($"Macro {i + 1} Button", MacroButtons[i]);
                }
            });
            EasySettings.OnApplySettings.AddListener(() =>
            {
                Config.Save();
            });
        }
    }

    public void Update()
    {
        if (Enabled.Value && ChatBehaviour._current && !ChatBehaviour._current._focusedInChat && (!SettingsManager._current || !SettingsManager._current._isOpen))
        {
            int triggeredKey = -1;

            for (int i = 0; i < MacroButtons.Count; i++)
            {
                if (Input.GetKeyDown(MacroButtons[i].Value))
                {
                    triggeredKey = i;
                    break;
                }
            }

            if (triggeredKey != -1 && !string.IsNullOrWhiteSpace(MacroTexts[triggeredKey].Value))
            {
                try
                {
                    DisableReturnRequirement.SkipReturnCheck = true;
                    ChatBehaviour._current.Send_ChatMessage(MacroTexts[triggeredKey].Value);
                }
                finally
                {
                    DisableReturnRequirement.SkipReturnCheck = false;
                }
            }
        }
    }
}