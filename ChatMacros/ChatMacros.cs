using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using Nessie.ATLYSS.EasySettings;
using UnityEngine;
using UnityEngine.EventSystems;

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
[BepInDependency("EasySettings", BepInDependency.DependencyFlags.SoftDependency)]
public class ChatMacros : BaseUnityPlugin
{
    public static ChatMacros Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(ChatMacros)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static ChatMacros? _plugin;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    public static ConfigEntry<bool> Enabled { get; private set; } = null!;
    public static ConfigEntry<bool> EnableAltMacros { get; private set; } = null!;
    public static ConfigEntry<bool> EnableCtrlMacros { get; private set; } = null!;
    public static List<ConfigEntry<string>> MacroTexts { get; private set; } = [];
    public static List<ConfigEntry<string>> MacroAltTexts { get; private set; } = [];
    public static List<ConfigEntry<string>> MacroCtrlTexts { get; private set; } = [];
    public static List<ConfigEntry<KeyCode>> MacroButtons { get; private set; } = [];

    private static readonly List<GameObject> _altTextOptions = [];
    private static readonly List<GameObject> _ctrlTextOptions = [];

    private static readonly Queue<string> _queuedCommands = [];
    private static DateTime _lastCommandAt = DateTime.Now;

    public ChatMacros()
    {
        _plugin = this;
        Logger = base.Logger;
        _harmony.PatchAll();

        Enabled = Config.Bind("General", "Enabled", true, "Enable or disable all keybindings for this mod");
        EnableAltMacros = Config.Bind("General", "EnableAltMacros", false, "Enables usage of Alt + Macro combinations");
        EnableCtrlMacros = Config.Bind("General", "EnableCtrlMacros", false, "Enables usage of Ctrl + Macro combinations");

        for (KeyCode key = KeyCode.Keypad1; key <= KeyCode.Keypad9; key++)
        {
            int index = key - KeyCode.Keypad1 + 1;

            MacroTexts.Add(Config.Bind("MacroTexts", $"Macro{index}", "", $"Chat message or command to send when Macro {index} is triggered"));
            MacroAltTexts.Add(Config.Bind("MacroTexts", $"Macro{index}Alt", "", $"Chat message or command to send when Macro {index} is triggered in combination with Alt"));
            MacroCtrlTexts.Add(Config.Bind("MacroTexts", $"Macro{index}Ctrl", "", $"Chat message or command to send when Macro {index} is triggered in combination with Ctrl"));
            MacroButtons.Add(Config.Bind("MacroBindings", $"Macro{index}Button", key, $"Button to press to trigger Macro {index}"));
        }
    }

    public void Awake()
    {
        if (Chainloader.PluginInfos.ContainsKey("EasySettings"))
        {
            SetupEasySettings();
            
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            void SetupEasySettings()
            {
                Settings.OnInitialized.AddListener(() =>
                {
                    var tab = Settings.GetOrAddCustomTab("ChatMacros");
                    
                    tab.AddToggle("Enabled", Enabled);
                    tab.AddToggle("Enable Alt Macros", EnableAltMacros);
                    tab.AddToggle("Enable Ctrl Macros", EnableCtrlMacros);

                    for (int i = 0; i < MacroButtons.Count; i++)
                    {
                        tab.AddTextField($"Macro {i + 1}", MacroTexts[i]);
                        _altTextOptions.Add(tab.AddTextField($"Macro {i + 1} + Alt", MacroAltTexts[i]).Root.gameObject);
                        _ctrlTextOptions.Add(tab.AddTextField($"Macro {i + 1} + Ctrl", MacroCtrlTexts[i]).Root.gameObject);
                        tab.AddKeyButton($"Macro {i + 1} Button", MacroButtons[i]);
                    }
                });
                Settings.OnApplySettings.AddListener(() =>
                {
                    Config.Save();
                });
            }
        }
    }
    
    public void Update()
    {
        for (int i = 0; i < _altTextOptions.Count; i++)
            _altTextOptions[i].SetActive(EnableAltMacros.Value);
        
        for (int i = 0; i < _ctrlTextOptions.Count; i++)
            _ctrlTextOptions[i].SetActive(EnableCtrlMacros.Value);

        bool canTakeInputs =
            Enabled.Value &&
            ChatBehaviour._current &&
            !ChatBehaviour._current._focusedInChat &&
            (!SettingsManager._current || !SettingsManager._current._isOpen) &&
            EventSystem.current.currentSelectedGameObject == null;

        if (_lastCommandAt + TimeSpan.FromMilliseconds(100) <= DateTime.Now && _queuedCommands.Count > 0)
        {
            _lastCommandAt = DateTime.Now;
            SendMacro(_queuedCommands.Dequeue());
        }
        
        if (canTakeInputs)
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

            if (triggeredKey != -1)
            {
                string targetText;

                if (EnableAltMacros.Value && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
                {
                    targetText = MacroAltTexts[triggeredKey].Value;
                }
                else if (EnableCtrlMacros.Value && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    targetText = MacroCtrlTexts[triggeredKey].Value;
                }
                else
                {
                    targetText = MacroTexts[triggeredKey].Value;
                }

                if (!string.IsNullOrWhiteSpace(targetText))
                {
                    string[] multipleCommands = targetText.Split("&&", StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < multipleCommands.Length; i++)
                    {
                        _queuedCommands.Enqueue(multipleCommands[i].Trim().Replace("&amp;", "&"));
                    }
                }
            }
        }
    }

    private static void SendMacro(string targetText)
    {
        if (!ChatBehaviour._current)
            return;
        
        try
        {
            DisableReturnRequirement.SkipReturnCheck = true;
            ChatBehaviour._current.Send_ChatMessage(targetText);
        }
        finally
        {
            DisableReturnRequirement.SkipReturnCheck = false;
        }
    }
}