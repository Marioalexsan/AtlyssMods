using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Marioalexsan.Multitool;

/// <summary>
/// The BepInEx plugin for Multitool.
/// </summary>
[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
public class MultitoolPlugin : BaseUnityPlugin
{
    /// <summary>
    /// Gets a reference to the currently loaded BepInEx plugin, if one exists
    /// </summary>
    public static MultitoolPlugin Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(MultitoolPlugin)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static MultitoolPlugin? _plugin;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony;

    /// <!-- nodoc -->
    public MultitoolPlugin()
    {
        _plugin = this;
        Logger = base.Logger;
        _harmony = new Harmony(ModInfo.GUID);
    }

    /// <!-- nodoc -->
    public void Awake()
    {
        _harmony.PatchAll();
    }
}