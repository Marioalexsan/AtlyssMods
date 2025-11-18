using BepInEx;
using UnityEngine.Events;
using UnityEngine;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Nessie.ATLYSS.EasySettings;
using System.Runtime.CompilerServices;

namespace Marioalexsan.CraptopMode.SoftDependencies;

public static class EasySettings
{
    private const MethodImplOptions SoftDepend = MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization;

    // Bookkeeping

    public const string ModID = "EasySettings";
    public static readonly Version ExpectedVersion = new Version("1.2.0");

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
}
