using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.Multiclass;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
public class Multiclass : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");
    internal new static ManualLogSource Logger { get; private set; } = null!;

    public Multiclass()
    {
        Logger = base.Logger;
        _harmony.PatchAll();
    }

    public void Awake()
    {
    }
}