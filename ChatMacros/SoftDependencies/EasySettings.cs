using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Marioalexsan.ChatMacros;
using Nessie.ATLYSS.EasySettings;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Marioalexsan.ChatMacros.SoftDependencies;

public static class EasySettings
{
    private const MethodImplOptions SoftDepend = MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization;

    // Bookkeeping

    public const string ModID = "EasySettings";
    public static readonly Version ExpectedVersion = new Version("1.2.0");
    public static Version CurrentVersion { get; private set; } = new Version("1.2.0");

    public static bool IsAvailable
    {
        get
        {
            if (!_initialized)
            {
                _plugin = Chainloader.PluginInfos.TryGetValue(ModID, out PluginInfo info) ? info.Instance : null;
                _initialized = true;

                if (_plugin == null)
                {
                    Logging.LogWarning($"Soft dependency {ModID} was not found.");
                }
                else if (_plugin.Info.Metadata.Version != ExpectedVersion)
                {
                    CurrentVersion = _plugin.Info.Metadata.Version;
                    Logging.LogWarning($"Soft dependency {ModID} has a different version than expected (have: {_plugin.Info.Metadata.Version}, expect: {ExpectedVersion}).");
                }
            }

            return _plugin != null;
        }
    }
    private static BaseUnityPlugin? _plugin;
    private static bool _initialized;

    // Implementation - all method calls must be marked with [MethodImpl(SoftDepend)] and must be guarded with a check to IsAvailable

    public static UnityEvent OnInitialized
    {
        [MethodImpl(SoftDepend)]
        get => Settings.OnInitialized;
    }

    public static UnityEvent OnCancelSettings
    {
        [MethodImpl(SoftDepend)]
        get => Settings.OnCancelSettings;
    }

    public static UnityEvent OnApplySettings
    {
        [MethodImpl(SoftDepend)]
        get => Settings.OnApplySettings;
    }

    public static UnityEvent OnCloseSettings
    {
        [MethodImpl(SoftDepend)]
        get => Settings.OnCloseSettings;
    }

    [MethodImpl(SoftDepend)]
    public static GameObject AddSpace()
        => Settings.ModTab.AddSpace().Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddHeader(string label)
        => Settings.ModTab.AddHeader(label).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddButton(string buttonLabel, UnityAction onClick)
        => Settings.ModTab.AddButton(buttonLabel, onClick).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddToggle(string label, ConfigEntry<bool> config)
        => Settings.ModTab.AddToggle(label, config).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddSlider(string label, ConfigEntry<float> config, bool wholeNumbers = false)
        => Settings.ModTab.AddSlider(label, config, wholeNumbers).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddAdvancedSlider(string label, ConfigEntry<float> config, bool wholeNumbers = false)
        => Settings.ModTab.AddAdvancedSlider(label, config, wholeNumbers).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddDropdown<T>(string label, ConfigEntry<T> config) where T : Enum
        => Settings.ModTab.AddDropdown(label, config).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddKeyButton(string label, ConfigEntry<KeyCode> config)
        => Settings.ModTab.AddKeyButton(label, config).Root.gameObject;

    [MethodImpl(SoftDepend)]
    public static GameObject AddTextField(string label, ConfigEntry<string> config, string preset = "Text...")
    {
        if (CurrentVersion < new Version(1, 2, 0))
        {
            if (!_warnedAboutTextFields)
            {
                _warnedAboutTextFields = true;
                Logging.LogWarning($"EasySettings text fields are not supported in version {CurrentVersion} (need {new Version(1, 2, 0)})");
                Logging.LogWarning($"This means that this mod won't be able to initialize text field settings, so you may have some missing stuff.");
                Logging.LogWarning($"Please update EasySettings or edit the configuration manually instead.");
            }
            return null!;
        }

        return AddTextFieldInternal(label, config, preset);
    }

    private static bool _warnedAboutTextFields = false;

    [MethodImpl(SoftDepend)]
    private static GameObject AddTextFieldInternal(string label, ConfigEntry<string> config, string preset = "Text...")
    {
        // Hack to fix an issue with extra long configs
        var previousValue = config.Value;

        var input = Settings.ModTab.AddTextField(label, config, preset);

        input.InputField.characterLimit = 0;
        input.InputField.SetTextWithoutNotify(previousValue);
        input.Apply();

        var rectTransform = input.InputField.GetComponent<RectTransform>();

        if (rectTransform)
        {
            rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 10, rectTransform.sizeDelta.x * 1.5f);
        }

        return input.Root.gameObject;
    }
}