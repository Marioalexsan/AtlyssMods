using BepInEx;
using HarmonyLib;
using Marioalexsan.Multitool.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.BetterPrismUI;

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

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("Marioalexsan.Multitool")]
public class BetterPrismUI : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony(ModInfo.GUID);

    internal static Texture2D Texture = null!;
    internal static Sprite Sprite = null!;
    internal static Image PrismImage = null!;

    private static float LastKnownMaxShield = 0;
    private static float LastKnownShield = 0;

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
            LastKnownShield = Player._mainPlayer._statusEntity._damageAbsorbtion;

            if (LastKnownShield <= 0)
            {
                LastKnownMaxShield = 0;
            }
            else
            {
                LastKnownMaxShield = Math.Max(LastKnownMaxShield, LastKnownShield);
            }

            var currentFill = PrismImage.fillAmount;
            var targetFill = LastKnownMaxShield != 0 ? LastKnownShield / LastKnownMaxShield : 0;

            PrismImage.fillAmount = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * 12f);
        }
    }
}