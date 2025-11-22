using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.Multitool;
using Marioalexsan.Multitool.Utils;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.BetterPrismUI;

public class PrismUIData
{
    public Image? FillImage;
    public Player? Player;
    public float LastKnownMaxShield;
    public float LastKnownShield;
    public bool Lerp = true;

    public void UpdateFill()
    {
        if (Player == null || FillImage == null)
            return;

        LastKnownShield = Player._statusEntity._damageAbsorbtion;

        if (LastKnownShield <= 0)
        {
            LastKnownMaxShield = 0;
        }
        else
        {
            LastKnownMaxShield = Math.Max(LastKnownMaxShield, LastKnownShield);
        }

        var currentFill = FillImage.fillAmount;
        var targetFill = LastKnownMaxShield != 0 ? LastKnownShield / LastKnownMaxShield : 0;

        FillImage.fillAmount = Lerp ? Mathf.Lerp(currentFill, targetFill, Time.deltaTime * 12f) : targetFill;
    }
}

[HarmonyPatch(typeof(InGameUI), nameof(InGameUI.Awake))]
static class PrismHealthUI
{
    static void Postfix()
    {
        if (GameObject.Find("_valueOrb(Prism)(BetterPrismUI)"))
            return;

        var hp = GameObject.Find("_valueOrb(Health)").GoToChild("_healthFill");

        var prismHp = GameObject.Instantiate(hp, hp.transform.parent);
        prismHp.transform.SetSiblingIndex(hp.transform.GetSiblingIndex() + 1);

        prismHp
            .Rename("_absorbtionFill")
            .ForComponent<Image>(image =>
            {
                BetterPrismUI.PrismImage = image;
                image.sprite = BetterPrismUI.Sprite;
                image.color = new Color(1, 1, 1, 0.6f);
                image.fillAmount = 0;
            })
            .ForComponent<RectTransform>(rect =>
            {
                rect.sizeDelta += new Vector2(5, 5);
            });
    }
}

[HarmonyPatch(typeof(PartyUIManager), nameof(PartyUIManager.Awake))]
[HarmonyWrapSafe]
static class PartyPrismHealthUI
{
    static void Postfix(PartyUIManager __instance)
    {
        BetterPrismUI.PartyShields.Clear();

        foreach (var entry in __instance._partyDataEntries)
        {
            var hp = entry.gameObject.GoToChild("_playerStatusValuesTab/_playerHealthBar/_healthFill");
            var prismHp = GameObject.Instantiate(hp, hp.transform.parent);
            prismHp.transform.SetSiblingIndex(hp.transform.GetSiblingIndex() + 1);

            prismHp
                .Rename("_absorbtionFill")
                .ForComponent<Image>(image =>
                {
                    image.color = new Color(1, 1, 1, 0.6f);
                    image.fillAmount = 0;
                    BetterPrismUI.PartyShields[entry] = new()
                    {
                        FillImage = image,
                        Lerp = false
                    };
                })
                .ForComponent<RectTransform>(rect =>
                {
                    rect.sizeDelta += new Vector2(5, 5);
                });
        }
    }
}

[HarmonyPatch]
static class DisplayWorldHealbarDuringPrism
{
    static MethodInfo TargetMethod() => AccessTools.FirstMethod(typeof(StatusEntityGUI), x => x.Name.Contains("Handle_PlayerHealthBar"));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        matcher.MatchForward(true,
            new CodeMatch(x => x.IsLdarg(0)),
            new CodeMatch(x => x.LoadsField(AccessTools.Field(typeof(StatusEntityGUI), nameof(StatusEntityGUI._statusEntity)))),
            new CodeMatch(x => x.LoadsField(AccessTools.Field(typeof(StatusEntity), nameof(StatusEntity._currentHealth)))),
            new CodeMatch(x => x.LoadsConstant(0)),
            new CodeMatch(x => x.Branches(out _))
        );

        if (!matcher.IsValid || !matcher.Instruction.Branches(out var setActiveBranch))
        {
            Logging.LogWarning($"Couldn't find branch location to transpile for {nameof(DisplayWorldHealbarDuringPrism)}!");
            Logging.LogWarning($"World healthbar display may be impacted! Please notify the mod developer about this!");
            return matcher.InstructionEnumeration();
        }

        // Find the first condition
        matcher.MatchBack(false,
            new CodeMatch(x => x.IsLdarg(0)),
            new CodeMatch(x => x.LoadsField(AccessTools.Field(typeof(StatusEntityGUI), nameof(StatusEntityGUI._statusEntity)))),
            new CodeMatch(x => x.LoadsField(AccessTools.Field(typeof(StatusEntity), nameof(StatusEntity._currentHealth)))),
            new CodeMatch(x => x.IsLdarg(1)),
            new CodeMatch(x => x.opcode == OpCodes.Ldfld),
            new CodeMatch(x => x.Branches(out _))
            );

        if (!matcher.IsValid)
        {
            Logging.LogWarning($"Couldn't find insert location to transpile for {nameof(DisplayWorldHealbarDuringPrism)}!");
            Logging.LogWarning($"World healthbar display may be impacted! Please notify the mod developer about this!");
            return matcher.InstructionEnumeration();
        }

        // Add another initial check that would send us to SetActive
        matcher
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StatusEntityGUI), nameof(StatusEntityGUI._statusEntity))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DisplayWorldHealbarDuringPrism), nameof(HasActivePrism))),
                new CodeInstruction(OpCodes.Brtrue, setActiveBranch)
            );

        return matcher.InstructionEnumeration();
    }

    static bool HasActivePrism(StatusEntity entity) => entity._damageAbsorbtion > 0;
}

[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
[HarmonyWrapSafe]
static class PlayerPrismUI
{
    static void Postfix(Player __instance)
    {
        var hp = __instance.gameObject.GoToChild("_Canvas_statusBars/_healthbarFillBackdrop/_healthbarFill");
        var prismHp = GameObject.Instantiate(hp, hp.transform.parent);
        prismHp.transform.SetSiblingIndex(hp.transform.GetSiblingIndex() + 1);

        prismHp
            .Rename("_absorbtionFill")
            .ForComponent<Image>(image =>
            {
                image.color = new Color(0.75f, 1, 1, 0.5f);
                image.fillAmount = 0;
                BetterPrismUI.PlayerWorldHealthbarShields[__instance] = new()
                {
                    Player = __instance,
                    FillImage = image,
                    Lerp = false
                };
            })
            .ForComponent<RectTransform>(rect =>
            {
                rect.sizeDelta += new Vector2(0.4f, 0.4f);
            });
    }
}

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("Marioalexsan.Multitool")]
public class BetterPrismUI : BaseUnityPlugin
{
    public static BetterPrismUI Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(BetterPrismUI)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static BetterPrismUI? _plugin;

    internal static new ManualLogSource Logger { get; private set; } = null!;

    private readonly Harmony _harmony = new Harmony(ModInfo.GUID);

    internal static Texture2D Texture = null!;
    internal static Sprite Sprite = null!;
    internal static Image PrismImage = null!;

    private static readonly PrismUIData MainPlayerShield = new();
    public static readonly Dictionary<Player, PrismUIData> PlayerWorldHealthbarShields = [];
    public static readonly Dictionary<PartyUIDataEntry, PrismUIData> PartyShields = [];

    BetterPrismUI()
    {
        _plugin = this;
        Logger = base.Logger;
    }

    public void Awake()
    {
        _harmony.PatchAll();

        Texture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
        {
            name = $"{ModInfo.GUID}-prismFillTex",
            filterMode = FilterMode.Bilinear,
        };

        Texture.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Info.Location), "Assets", "prismFill.png")));
        Sprite = Sprite.Create(Texture, new Rect(0, 0, Texture.width, Texture.height), new Vector2(0.5f, 0.5f));
        Sprite.name = $"{ModInfo.GUID}-prismFillSprite";
    }

    public void Update()
    {
        if (Player._mainPlayer != null)
        {
            MainPlayerShield.Player = Player._mainPlayer;
            MainPlayerShield.FillImage = PrismImage;
            MainPlayerShield.UpdateFill();
        }

        if (PartyUIManager._current != null)
        {
            var partyEntries = PartyUIManager._current._partyDataEntries;

            for (int i = 0; i < partyEntries.Length; i++)
            {
                var entry = partyEntries[i];

                if (!PartyShields.TryGetValue(entry, out var data))
                    continue; // Extra modded slots? Ehhhhhhh we'll fix that later

                if (data.Player != entry._player)
                {
                    data.LastKnownShield = 0;
                    data.LastKnownMaxShield = 0;
                    data.Player = entry._player;
                }

                data.UpdateFill();
            }
        }

        bool requiresCleanup = false;

        foreach (var player in PlayerWorldHealthbarShields)
        {
            if (!player.Key)
            {
                requiresCleanup = true;
                continue;
            }

            var data = player.Value;

            data.UpdateFill();
        }

        if (requiresCleanup)
        {
            foreach (var player in PlayerWorldHealthbarShields.Keys.ToArray())
            {
                if (!player)
                    PlayerWorldHealthbarShields.Remove(player);
            }
        }
    }
}