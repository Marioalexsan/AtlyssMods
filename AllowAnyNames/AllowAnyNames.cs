using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CodeTalker.Networking;
using CodeTalker.Packets;
using HarmonyLib;
using Marioalexsan.Multitool.SaveUtils;
using Marioalexsan.Multitool.Utils;
using Mirror;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.AllowAnyNames;

// Send information about your own name to others
public class NameUpdatePacket : PacketBase
{
    public override string PacketSourceGUID => $"{ModInfo.GUID}.{nameof(NameUpdatePacket)}";

    public int Version { get; set; } = 1;

    // Credit goes to Catman232 for the suggestion to use NetIDs
    public uint TargetNetId { get; set; } = 0;

    public string RichTextName { get; set; } = "";

    public static readonly NameUpdatePacket Instance = new();
}

[HarmonyPatch(typeof(Player), nameof(Player.OnGameConditionChange))]
static class SendNameUpdateOnPlayerJoinedForClients
{
    static void Postfix(CharacterSelectManager __instance)
    {
        if (!NetworkServer.activeHost && Player._mainPlayer)
        {
            var richText = AllowAnyNames.CustomSaveData.TryGetValue(ProfileDataManager._current.SelectedFileIndex, out var modData) ? modData.RichTextName : AllowAnyNames.NullAAN;
            AllowAnyNames.SendNameUpdate(Player._mainPlayer.netId, richText);
        }
    }
}

[HarmonyPatch(typeof(ProfileDataSender), nameof(ProfileDataSender.Assign_PlayerData))]
static class SendNameUpdateOnPlayerJoinedForServer
{
    static void Postfix(CharacterSelectManager __instance)
    {
        if (Player._mainPlayer)
        {
            var richText = AllowAnyNames.CustomSaveData.TryGetValue(ProfileDataManager._current.SelectedFileIndex, out var modData) ? modData.RichTextName : AllowAnyNames.NullAAN;
            AllowAnyNames.SendNameUpdate(Player._mainPlayer.netId, richText);
        }
    }
}

[HarmonyPatch]
static class HandleCharacterSelectionInputs
{
    static MethodInfo TargetMethod() => AccessTools.FirstMethod(typeof(CharacterSelectManager), x => x.Name.Contains("Handle_GamepadSelectionControl"));

    static bool Prefix(CharacterSelectManager __instance)
    {
        return !CreateRenameUserInteface.IsRenamePromptActive;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.Update))]
static class ConnectedPlayers
{
    static void Postfix(Player __instance)
    {
        AllowAnyNames.Players.Add(__instance);
    }
}

[HarmonyPatch]
static class HandleRenameInterface
{
    static MethodInfo TargetMethod() => AccessTools.FirstMethod(typeof(CharacterSelectManager), x => x.Name.Contains("Handle_ButtonControl"));

    static void Postfix(CharacterSelectManager __instance)
    {
        if (CreateRenameUserInteface.IsRenamePromptActive)
        {
            __instance._nextSlotPageButton.interactable = false;
            __instance._previousSlotPageButton.interactable = false;
            __instance._characterDeleteButton.interactable = false;

            CreateRenameUserInteface.RenamePromptButton!.interactable = false;
            CreateRenameUserInteface.RenameButton!.interactable = true;
            CreateRenameUserInteface.ReturnButton!.interactable = true;
        }
        else
        {
            CreateRenameUserInteface.RenamePromptButton!.interactable = !__instance._deleteCharacterPrompt && !ProfileDataManager._current._characterFile._isEmptySlot;
            CreateRenameUserInteface.RenameButton!.interactable = false;
            CreateRenameUserInteface.ReturnButton!.interactable = false;
            CreateRenameUserInteface.RenameInputText!.text = AllowAnyNames.GetCharacterRichTextName(ProfileDataManager._current._characterFile);
        }
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Awake))]
static class CreateRenameUserInteface
{
    internal static bool IsRenamePromptActive = false;

    internal static MenuElement? RenameDolly;
    internal static InputField? RenameInputText;
    internal static Button? RenamePromptButton;
    internal static Button? RenameButton;
    internal static Button? ReturnButton;

    static void Postfix(MainMenuManager __instance)
    {
        var field = __instance._characterCreationManager._characterNameInputField;
        field.characterValidation = UnityEngine.UI.InputField.CharacterValidation.None;
        field.characterLimit = AllowAnyNames.MaxRichTextNameLength;

        // Skip creating if it's already present for some reason
        if (GameObject.Find("AAN_button_renameCharacter"))
            return;

        var deleteButton = GameObject.Find("_button_deleteCharacter");
        var deletePrompt = GameObject.Find("_dolly_characterDeletePrompt");
        var multiplayerNickname = GameObject.Find("_input_@nickname");

        bool templateValid = deleteButton
            && deleteButton.GetComponentInChildren<Text>()
            && deleteButton.GetComponent<Button>()
            && deletePrompt
            && multiplayerNickname;

        if (!templateValid)
        {
            Logging.LogWarning("Couldn't find base UI elements to edit / instantiate from!");
            return;
        }

        var renameButton = GameObject.Instantiate(deleteButton, deleteButton.transform.parent);

        multiplayerNickname
            .ForComponent<RectTransform>(transform =>
            {
                transform.localPosition += new Vector3(0, 60, 0);
            });

        renameButton
            .GoToParent()
            .ForComponent<RectTransform>(transform =>
            {
                transform.localPosition += new Vector3(0, 20, 0);
                transform.sizeDelta += new Vector2(0, transform.sizeDelta.y - 5);
            });

        renameButton
            .Rename("AAN_button_renameCharacter")
            .ForChildComponent<Text>(x => x.text = "Rename Character")
            .ForComponent<Button>(x =>
            {
                RenamePromptButton = x;
                x.onClick = new Button.ButtonClickedEvent();
                x.onClick.AddListener(OpenRenameCharacterPromptClicked);
            });

        var renamePrompt = GameObject.Instantiate(deletePrompt, deletePrompt.transform.parent);

        renamePrompt
            .Rename("AAN_dolly_characterRenamePrompt")
            .SaveComponentRef(ref RenameDolly)
            .ForChild("_backdrop_deleteCharacter", backdrop =>
            {
                backdrop
                    .Rename("AAN_backdrop_renameCharacter")
                    .ForChild("_text_characterDeletePrompt", prompt =>
                    {
                        prompt
                            .Rename("AAN_text_characterRenamePrompt")
                            .ForComponent<Text>(x => x.text = "Type in the new name for the character");
                    })
                    .ForChild("_input_characterDeleteConfirm", input =>
                    {
                        input
                            .Rename("AAN_input_characterRenameConfirm")
                            .SaveComponentRef(ref RenameInputText)
                            .ForComponent<InputField>(input =>
                            {
                                input.characterLimit = AllowAnyNames.MaxRichTextNameLength;
                                input.characterValidation = InputField.CharacterValidation.None;
                            });
                    })
                    .ForChild("_button_confirmDeleteCharacter", input =>
                    {
                        input
                            .Rename("AAN_button_confirmRenameCharacter")
                            .ForComponent<Button>(button =>
                            {
                                RenameButton = button;
                                button.onClick = new Button.ButtonClickedEvent();
                                button.onClick.AddListener(RenameCharacterConfirmClicked);
                            })
                            .ForChildComponent<Text>(text => text.text = "Rename Character");
                    })
                    .ForChild("_button_deletePrompt_return", input =>
                    {
                        input
                            .Rename("AAN_button_renamePrompt_return")
                            .ForComponent<Button>(button =>
                            {
                                button.onClick = new Button.ButtonClickedEvent();
                                button.onClick.AddListener(RenameCharacterReturnClicked);
                                ReturnButton = button;
                            });
                    });
            });
    }

    static void OpenRenameCharacterPromptClicked()
    {
        GameObject.Find("_aSrc_menuDecline")?.GetComponent<AudioSource>()?.Play();
        IsRenamePromptActive = true;
        RenameDolly!.EnableElement(true);
        RenameInputText!.ActivateInputField();
    }

    static void RenameCharacterConfirmClicked()
    {
        GameObject.Find("_aSrc_menuAccept")?.GetComponent<AudioSource>()?.Play();
        RenameDolly!.EnableElement(false);
        IsRenamePromptActive = false;

        var rtfName = RenameInputText!.text;
        var saveFile = ProfileDataManager._current._characterFile;
        AllowAnyNames.SetCharacterRichTextName(rtfName, saveFile);
        ProfileDataManager._current.Save_ProfileData();
    }

    static void RenameCharacterReturnClicked()
    {
        RenameDolly!.EnableElement(false);
        IsRenamePromptActive = false;
    }
}

[HarmonyPatch]
static class FixCharacterNicknames
{
    static MethodInfo TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CharacterCreationManager)).First(x => x.Name.Contains("Apply_CharacterBaseData"));

    static void Postfix(CharacterCreationManager __instance)
    {
        var rtfName = __instance._characterNameInputField.text;
        var saveFile = ProfileDataManager._current._characterFile;
        AllowAnyNames.SetCharacterRichTextName(rtfName, saveFile);
    }
}

[HarmonyPatch]
static class FullRichTextReplacements
{
    static IEnumerable<MethodInfo> TargetMethods()
    {
        // Player _nickname
        yield return AccessTools.Method(typeof(StatusEntity), nameof(StatusEntity.Init_PvpFragMessage));
        yield return AccessTools.Method(typeof(PartyUIDataEntry), nameof(PartyUIDataEntry.Update));
        yield return AccessTools.FirstMethod(typeof(TargetInfoDisplayManager), method => method.Name.Contains("Handle_PlayerInfoDisplay"));
        yield return AccessTools.FirstMethod(typeof(StatsMenuCell), method => method.Name.Contains("Handle_StatCounterDisplays"));
        yield return AccessTools.Method(typeof(WhoMenuCell), nameof(WhoMenuCell.Init_MutePeer));
        yield return AccessTools.Method(typeof(WhoListDataEntry), nameof(WhoListDataEntry.Handle_WhoDataEntry));
        yield return AccessTools.Method(typeof(ChatBehaviour), nameof(ChatBehaviour.On_ChannelSwitch));
        yield return AccessTools.Method(typeof(ChatBehaviour), nameof(ChatBehaviour.UserCode_Cmd_SendChatMessage__String__ChatChannel));
        yield return AccessTools.Method(typeof(ChatBehaviour), nameof(ChatBehaviour.UserCode_Rpc_RecieveChatMessage__String__Boolean__ChatChannel));

        // Assign_PlayerData's connect message will break if the name has any RTF tags, so skip it!
        //yield return AccessTools.Method(typeof(ProfileDataSender).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).First(x => x.Name.Contains("Assign_PlayerData")), "MoveNext");

        yield return AccessTools.Method(typeof(ConsoleCommandManager), nameof(ConsoleCommandManager.Init_BanClient));
        yield return AccessTools.Method(typeof(ConsoleCommandManager), nameof(ConsoleCommandManager.Init_KickClient));
        yield return AccessTools.Method(typeof(HC_PeerListEntry), nameof(HC_PeerListEntry.Apply_PeerDataEntry));
        yield return AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Destroy_PeerListEntry));
        yield return AccessTools.Method(typeof(Player), nameof(Player.Handle_ClientParameters));
        yield return AccessTools.Method(typeof(Player), nameof(Player.Handle_PartyInviteStatus));
        yield return AccessTools.Method(typeof(Player), nameof(Player.Pend_PartyInvite));
        yield return AccessTools.Method(typeof(Player), nameof(Player.UserCode_Cmd_InviteToParty__Player));
        yield return AccessTools.Method(typeof(PlayerStats), nameof(PlayerStats.OnLevelUp));
        yield return AccessTools.Method(typeof(PlayerStats), nameof(PlayerStats.UserCode_Cmd_GainProfessionExp__ResourceEntity__Int32));
        yield return AccessTools.Method(typeof(PlayerStats), nameof(PlayerStats.UserCode_Cmd_RequestClass__String));
        yield return AccessTools.Method(typeof(PlayerStats), nameof(PlayerStats.UserCode_Cmd_RequestClassTier__Int32));

        // CharacterFile _nickName
        yield return AccessTools.FirstMethod(typeof(CharacterSelectListDataEntry), method => method.Name.Contains("Handle_FilledSlotDisplay"));
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, MethodBase original)
    {
        var matcher = new CodeMatcher(code);

        int occurrenceCount = 0;

        while (true)
        {
            matcher.MatchForward(false, new CodeMatch(ins =>
            {
                return
                    ins.LoadsField(AccessTools.Field(typeof(Player), nameof(Player._nickname))) ||
                    ins.LoadsField(AccessTools.Field(typeof(CharacterFile), nameof(CharacterFile._nickName)));
            }));

            if (!matcher.IsValid)
                break;

            occurrenceCount++;

            if (matcher.Instruction.LoadsField(AccessTools.Field(typeof(Player), nameof(Player._nickname))))
            {
                matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AllowAnyNames), nameof(AllowAnyNames.GetPlayerRichTextName))));
            }
            else
            {
                matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AllowAnyNames), nameof(AllowAnyNames.GetCharacterRichTextName))));
            }
        }

        //Console.WriteLine("Patched " + occurrenceCount + " occurrences in " + original.Name);

        return matcher.InstructionEnumeration();
    }
}

class AllowAnyNamesSave
{
    public string RichTextName { get; set; } = "";
}

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("Marioalexsan.Multitool")]
public class AllowAnyNames : BaseUnityPlugin
{
    public const int MaxRichTextNameLength = 512;
    public const string NullAAN = "NullAAN";

    public struct MultiplayerPlayerData
    {
        public string CustomName;
        public DateTime LastUpdate;
    }

    private readonly Harmony _harmony = new Harmony(ModInfo.GUID);

    // Indexed by netID
    internal static readonly Dictionary<uint, MultiplayerPlayerData> MultiplayerData = [];

    private static readonly Regex TagRegex = new Regex("<.*?>");

    internal static readonly Dictionary<int, AllowAnyNamesSave> CustomSaveData = [];

    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal new static ConfigFile Config { get; private set; } = null!;

    internal static HashSet<Player> Players { get; } = [];

    private static TimeSpan NameUpdateCooldown;

    public void Awake()
    {
        Config = base.Config;
        Logger = base.Logger;
        _harmony.PatchAll();

        CodeTalkerNetwork.RegisterListener<NameUpdatePacket>(OnNameUpdate);
        SaveUtilsAPI.RegisterProfileData<AllowAnyNamesSave>(ModInfo.GUID, SaveProfileData, LoadProfileData, DeleteProfileData);
    }

    static void SaveProfileData(CharacterFile file, int index, out AllowAnyNamesSave? saveData)
    {
        if (!CustomSaveData.TryGetValue(index, out saveData))
        {
            saveData = new()
            {
                RichTextName = file._nickName
            };
        }
    }

    static void LoadProfileData(CharacterFile file, int index, AllowAnyNamesSave? saveData)
    {
        saveData ??= new()
        {
            RichTextName = file._nickName
        };

        CustomSaveData[index] = saveData;
    }

    static void DeleteProfileData(CharacterFile file, int index)
    {
        CustomSaveData.Remove(index);
    }

    public void Update()
    {
        foreach (var player in Players)
        {
            if (!player)
                MultiplayerData.Remove(player.netId);
        }

        Players.RemoveWhere(player => !player);

        if (!(NetworkServer.active || NetworkClient.active))
        {
            NameUpdateCooldown = TimeSpan.FromSeconds(5);
        }
        else
        {
            NameUpdateCooldown -= TimeSpan.FromSeconds(Time.deltaTime);

            if (NameUpdateCooldown < TimeSpan.Zero)
            {
                NameUpdateCooldown += TimeSpan.FromSeconds(60);

                // Update the name, just in case it's desynced
                if (Player._mainPlayer)
                {
                    var richText = CustomSaveData.TryGetValue(ProfileDataManager._current.SelectedFileIndex, out var modData) ? modData.RichTextName : NullAAN;
                    SendNameUpdate(Player._mainPlayer.netId, richText);
                }
            }
        }
    }

    public static bool ValidateName(string richTextName)
    {
        if (string.IsNullOrEmpty(richTextName))
            return false; // Ignore junk

        if (richTextName.Length > MaxRichTextNameLength)
            return false; // Ignore names that are above the AAN limitations

        return true;
    }

    public static string StripToVanillaName(string richText)
    {
        const int MaxVanillaNameLength = 18;

        // Strip tags fully, then strip tag characters if any are left over for some reason.
        var tagsStripped = TagRegex
            .Replace(richText, "")
            .Replace("<", "")
            .Replace(">", "");

        // Apply additional stripping so that the name conforms to vanilla
        // Vanilla bans '<', '>', '/', texts over 18 characters, empty tests, and texts consisting of only whitespace
        var vanillaStripped = tagsStripped
            .Replace("/", "");

        if (string.IsNullOrWhiteSpace(vanillaStripped))
            return NullAAN;

        // For names that are too long, try to strip them down to 18 characters instead of setting to NullAAN
        if (vanillaStripped.Length > MaxVanillaNameLength)
            vanillaStripped = vanillaStripped.Trim()[..MaxVanillaNameLength];

        return vanillaStripped;
    }

    public static string GetPlayerRichTextName(Player player)
    {
        if (player == null)
            return NullAAN;

        if (player == Player._mainPlayer)
            return CustomSaveData.TryGetValue(ProfileDataManager._current.SelectedFileIndex, out var modData) ? modData.RichTextName : player._nickname;

        if (MultiplayerData.TryGetValue(player.netId, out var data))
            return data.CustomName;

        // Fall back to default nickname
        return player._nickname;
    }

    internal static int GetFileIndex(CharacterFile file)
    {
        var files = ProfileDataManager._current._characterFiles;

        for (int i = 0; i < files.Length; i++)
        {
            if (files[i] == file)
                return i;
        }

        return -1;
    }

    internal static string GetCharacterRichTextName(CharacterFile player)
    {
        if (player == null)
            return NullAAN;

        if (CustomSaveData.TryGetValue(GetFileIndex(player), out var saveData))
            return saveData.RichTextName;

        // Fall back to default nickname
        return player._nickName;
    }

    public static void SetCharacterRichTextName(string rtfName, CharacterFile saveFile)
    {
        var rawName = StripToVanillaName(rtfName);

        saveFile._nickName = rawName;

        var saveIndex = GetFileIndex(saveFile);

        if (!CustomSaveData.TryGetValue(saveIndex, out var customSaveData))
            CustomSaveData[saveIndex] = customSaveData = new();

        customSaveData.RichTextName = rtfName;
    }

    private static void OnNameUpdate(PacketHeader header, PacketBase packet)
    {
        if (packet is not NameUpdatePacket nameUpdate)
            return; // Nonsense? Ignore it anyway

        var hasPreviousData = MultiplayerData.TryGetValue(nameUpdate.TargetNetId, out var data);

        if (hasPreviousData && DateTime.Now < data.LastUpdate + TimeSpan.FromSeconds(15))
            return; // Drop spam packets

        if (!ValidateName(nameUpdate.RichTextName))
            return; // Disallow junk names

        bool validPlayer = false;

        foreach (var player in Players)
        {
            if (!player)
                continue;

            // Check that the netId matches *and* that the net ID is owned by the steam ID sending the packet
            if (player.netId == nameUpdate.TargetNetId && player._steamID == header.SenderID.ToString())
            {
                validPlayer = true;
                break;
            }
        }

        if (!validPlayer)
            return; // Disallow updates targeting players not owned by this steamID

        MultiplayerData[nameUpdate.TargetNetId] = new()
        {
            CustomName = nameUpdate.RichTextName,
            LastUpdate = DateTime.Now,
        };
        Logging.LogInfo($"Received name update! {header.SenderID} {nameUpdate.RichTextName}");
    }

    internal static void SendNameUpdate(uint netId, string richTextName)
    {
        var packet = NameUpdatePacket.Instance;

        packet.Version = 1;

        // Send our netID - if a player is connected through multiple instances,
        // then this should allow us to update each of them correctly
        packet.TargetNetId = netId;
        packet.RichTextName = richTextName;

        Logging.LogInfo($"Sending name update! {packet.RichTextName}");
        CodeTalkerNetwork.SendNetworkPacket(packet);
    }
}