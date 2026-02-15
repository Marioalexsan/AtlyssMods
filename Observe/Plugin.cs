using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using CodeTalker.Networking;
using CodeTalker.Packets;
using HarmonyLib;
using Nessie.ATLYSS.EasySettings;
using UnityEngine;

namespace Marioalexsan.Observe;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("EasySettings", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("CodeTalker")]
[BepInDependency("Soggy_Pancake.CommandLib")]
public class ObservePlugin : BaseUnityPlugin
{
    private struct PlayerLookInfo
    {
        public Quaternion DesiredLookDirection;
        public PlayerRaceModel RaceModel;
        public Transform Head;
        public Quaternion LastHeadRotation;
        public bool OwlMode;
        public LookSpeed LookSpeed;
        public LookDirection OverrideDirection; // Only used for secondary behaviour!
        public bool VanillaMode;
    }

    private struct SentPlayerInfo
    {
        public Quaternion DesiredLookDirection;
        public bool VanillaMode;
        public bool OwlMode;
        public LookSpeed LookSpeed;
        public DateTime Timestamp;
        public LookDirection OverrideDirection; // Only used for secondary behaviour!
    }
    
    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    private readonly Dictionary<Player, PlayerLookInfo> _cachedPlayerData = [];
    private readonly Dictionary<ulong, SentPlayerInfo> MultiplayerData = [];
    internal static HashSet<Player> Players { get; } = [];
    
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> VanillaModeSetting = null!;
    internal static ConfigEntry<bool> EnableNetworking = null!;
    internal static ConfigEntry<bool> OwlModeSetting = null!;
    internal static ConfigEntry<bool> AllowOwlModeForOthers = null!;
    internal static ConfigEntry<LookSpeed> LookSpeedSetting = null!;
    internal static ConfigEntry<bool> HoldHeadDirectionAfterStrafing = null!;
    internal static ConfigEntry<float> HoldHeadDirectionAfterStrafingDuration = null!;
    
    internal static ConfigEntry<bool> LookAtInteractables = null!;
    internal static ConfigEntry<bool> LookAtNPCDuringDialogue = null!;

    private bool _lastSendEnableState = false;
    private bool _shouldSendCurrentState = false;

    private static TimeSpan RefreshDirectionAccumulator = TimeSpan.Zero;
    private static TimeSpan PacketSendCooldown = TimeSpan.Zero;

    internal static LookDirection LocalOverrideDirection = LookDirection.Default;
    internal static TimeSpan LocalOverrideDirectionTime = TimeSpan.Zero;
    internal static Quaternion SavedOverride = Quaternion.identity;

    internal new static ManualLogSource Logger = null!;

    public ObservePlugin()
    {
        Logger = base.Logger;
        Enabled = Config.Bind("General", "Enabled", true, "Enable or disable mod functionality completely.");
        VanillaModeSetting = Config.Bind("General", "VanillaMode", false, "While active, makes your character's head act the same as in vanilla.");
        EnableNetworking = Config.Bind("General", "EnableNetworking", true, "Enable sending/receiving camera directions to/from people with the mod installed.");
        OwlModeSetting = Config.Bind("Display", "OwlMode", false, "Enables full range of rotation for your player's head. This might look pretty weird in some cases!");
        AllowOwlModeForOthers = Config.Bind("Display", "AllowOwlModeForOthers", true, "Allow other players that use OwlMode to display their unconstrained head rotations.");
        LookSpeedSetting = Config.Bind("Display", "LookSpeed", LookSpeed.Normal, "The speed at which your character reacts to changes in direction.");
        HoldHeadDirectionAfterStrafing = Config.Bind("Controls", "HoldHeadDirectionAfterStrafing", true, "Enable to keep looking at the given direction after strafing as if you used \"/observe environment\".");
        HoldHeadDirectionAfterStrafingDuration = Config.Bind("Controls", "HoldHeadDirectionAfterStrafingDuration", 4f, new ConfigDescription("How long to continue looking at the environment when HoldHeadDirectionAfterStrafing is enabled, in seconds.", new AcceptableValueRange<float>(0, 120)));
        LookAtInteractables = Config.Bind("FunStuff", "LookAtInteractables", true, "Have your character look at interactable objects if not already posing.");
        LookAtNPCDuringDialogue = Config.Bind("FunStuff", "LookAtNPCDuringDialogue", true, "Have your character look at NPCs during dialogue if not already posing.");
    }

    private void Awake()
    {
        _harmony.PatchAll();
        
        if (Chainloader.PluginInfos.ContainsKey("EasySettings"))
            RegisterEasySettings();

        // TODO: Implement and test as soft dependency
        if (Chainloader.PluginInfos.ContainsKey("CodeTalker"))
            RegisterCodeYapperListener();

        // TODO: Implement and test as soft dependency
        if (Chainloader.PluginInfos.ContainsKey("Soggy_Pancake.CommandLib"))
            RegisterCommandLibCommands();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private void RegisterCodeYapperListener()
    {
        try
        {
            CodeTalkerNetwork.RegisterBinaryListener<LookPacket>(HandleNetworkSyncFromOthers);
        }
        catch (Exception e)
        {
            Logger.LogWarning("Failed to register network listener for CodeYapper! Please report this to the mod author!");
            Logger.LogWarning($"Exception: {e}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private void RegisterCommandLibCommands()
    {
        try
        {
            Commands.RegisterCommands();
        }
        catch (Exception e)
        {
            Logger.LogWarning("Failed to register commands for CommandLib! Please report this to the mod author!");
            Logger.LogWarning($"Exception: {e}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private void RegisterEasySettings()
    {
        try
        {
            Settings.OnInitialized.AddListener(() =>
            {
                var observeTab = Settings.GetOrAddCustomTab("Observe");

                observeTab.AddHeader("General");
                observeTab.AddToggle("Enabled", Enabled);
                observeTab.AddToggle("Ignore Camera (self)", VanillaModeSetting);
                observeTab.AddToggle("Enable Networking", EnableNetworking);
                observeTab.AddToggle("Owl Mode (full rotations)", OwlModeSetting);
                observeTab.AddToggle("Allow Owl Mode for others", AllowOwlModeForOthers);
                observeTab.AddDropdown("Look speed", LookSpeedSetting);
                observeTab.AddToggle("Hold head direction after strafing", HoldHeadDirectionAfterStrafing);
                observeTab.AddAdvancedSlider("Strafe hold duration", HoldHeadDirectionAfterStrafingDuration, true);
                observeTab.AddHeader("Fun stuff");
                observeTab.AddToggle("Look at highlighted items", LookAtInteractables);
                observeTab.AddToggle("Look at highlighted NPCs", LookAtNPCDuringDialogue);
            });
            Settings.OnApplySettings.AddListener(() =>
            {
                Config.Save();
            });
        }
        catch (Exception e)
        {
            Logger.LogWarning("Failed to register settings for EasySettings! Please report this to the mod author!");
            Logger.LogWarning($"Exception: {e}");
        }
    }

    private void HandleNetworkSyncFromOthers(PacketHeader header, BinaryPacketBase packet)
    {
        if (!EnableNetworking.Value)
            return;
        
        if (packet is not LookPacket lookPacket || !lookPacket.IsValid)
            return; // Junk

        bool validPlayer = false;
        
        foreach (var player in Players)
        {
            if (!player)
                continue;

            // Check that the netId matches *and* that the net ID is owned by the steam ID sending the packet
            if (player.netId == lookPacket.TargetNetId && player._steamID == header.SenderID.ToString())
            {
                validPlayer = true;
                break;
            }
        }

        if (!validPlayer)
            return; // Junk

        MultiplayerData[lookPacket.TargetNetId] = new SentPlayerInfo()
        {
            Timestamp = DateTime.UtcNow,
            DesiredLookDirection = lookPacket.CameraRotation,
            VanillaMode = lookPacket.VanillaMode,
            OwlMode = lookPacket.OwlMode,
            OverrideDirection = lookPacket.OverrideDirection,
            LookSpeed = lookPacket.LookSpeed,
        };
    }

    private void LateUpdate()
    {
        // Handle override timer
        LocalOverrideDirectionTime -= TimeSpan.FromSeconds(Time.deltaTime);
        if (LocalOverrideDirectionTime < TimeSpan.Zero)
        {
            LocalOverrideDirectionTime = TimeSpan.Zero;
            LocalOverrideDirection = LookDirection.Default;
        }
        
        HandlePlayerCleanup();
        HandleLocalPlayerAndNetworkSyncToOthers();
        HandleOtherPlayers();
    }

    private void HandlePlayerCleanup()
    {
        foreach (var player in Players)
        {
            if (!player)
            {
                MultiplayerData.Remove(player.netId);
                _cachedPlayerData.Remove(player);
            }
        }
        
        Players.RemoveWhere(player => !player);
    }

    private void HandleLocalPlayerAndNetworkSyncToOthers()
    {
        if (Player._mainPlayer && _cachedPlayerData.TryGetValue(Player._mainPlayer, out var mainPlayerLookData))
        {
            // Send every 3 seconds or every 30 degree camera change
            const double SecondsPerSend = 3f;
            const double DegreesPerSend = 30;

            if (_lastSendEnableState || EnableNetworking.Value)
            {
                bool ignoreCameraSwitched = mainPlayerLookData.VanillaMode != VanillaModeSetting.Value;
            
                if (VanillaModeSetting.Value)
                    RefreshDirectionAccumulator -= TimeSpan.FromSeconds(Time.deltaTime);
                else
                {
                    var angleChange = Quaternion.Angle(mainPlayerLookData.DesiredLookDirection, CameraFunction._current.transform.rotation);
                    RefreshDirectionAccumulator -= TimeSpan.FromSeconds(Time.deltaTime + angleChange * SecondsPerSend / DegreesPerSend);
                }
            
                if (ignoreCameraSwitched || RefreshDirectionAccumulator <= TimeSpan.Zero)
                {
                    RefreshDirectionAccumulator = TimeSpan.FromSeconds(SecondsPerSend);
                    _shouldSendCurrentState = true;
                }
            }
            else
            {
                RefreshDirectionAccumulator = TimeSpan.Zero;
            }
            
            // Check if we need to pose the player with strafe
            if (Player._mainPlayer._pMove._strafeToggle && HoldHeadDirectionAfterStrafing.Value)
            {
                LocalOverrideDirectionTime = TimeSpan.FromSeconds(HoldHeadDirectionAfterStrafingDuration.Value);
                LocalOverrideDirection = LookDirection.Environment;
                SavedOverride = CameraFunction._current.transform.rotation;
            }
            
            // Add settings for local player to cache
            mainPlayerLookData.DesiredLookDirection = GetMainPlayerTargetRotation(mainPlayerLookData.Head);
            mainPlayerLookData.VanillaMode = VanillaModeSetting.Value;
            mainPlayerLookData.OwlMode = OwlModeSetting.Value;
            mainPlayerLookData.LookSpeed = LookSpeedSetting.Value;
            mainPlayerLookData.OverrideDirection = LocalOverrideDirection;
            _cachedPlayerData[Player._mainPlayer] = mainPlayerLookData;
        }

        _lastSendEnableState = EnableNetworking.Value;

        PacketSendCooldown -= TimeSpan.FromSeconds(Time.deltaTime);

        if (PacketSendCooldown < TimeSpan.Zero)
            PacketSendCooldown = TimeSpan.Zero;
        
        if (_shouldSendCurrentState && PacketSendCooldown <= TimeSpan.Zero)
        {
            _shouldSendCurrentState = false;
            PacketSendCooldown = TimeSpan.FromMilliseconds(250);

            if (Player._mainPlayer && _cachedPlayerData.TryGetValue(Player._mainPlayer, out var mainPlayerData))
            {
                LookPacket.Instance.CameraRotation = VanillaModeSetting.Value ? Quaternion.identity : GetMainPlayerTargetRotation(mainPlayerData.Head);
                LookPacket.Instance.TargetNetId = Player._mainPlayer.netId;
                LookPacket.Instance.VanillaMode = VanillaModeSetting.Value;
                LookPacket.Instance.OwlMode = OwlModeSetting.Value;
                LookPacket.Instance.LookSpeed = LookSpeedSetting.Value;
                LookPacket.Instance.OverrideDirection = LocalOverrideDirection;
            
                foreach (var player in Players)
                {
                    // Only send to other players that have valid SteamIDs to avoid log errors from CodeYapper
                    if (player != Player._mainPlayer && ulong.TryParse(player.Network_steamID, out _))
                        CodeTalkerNetwork.SendNetworkPacket(player, LookPacket.Instance);
                }
            }
        }
    }

    private void HandleOtherPlayers()
    {
        foreach (var player in Players)
        {
            if (_cachedPlayerData.TryGetValue(player, out var lookData) && MultiplayerData.TryGetValue(player.netId, out var sentData))
            {
                // Check if network peer stopped sending directions for some reason
                // If so, assume they disabled sending info and make them act as vanilla
                if (sentData.Timestamp + TimeSpan.FromSeconds(6) < DateTime.UtcNow)
                {
                    sentData.VanillaMode = true;
                    MultiplayerData[player.netId] = sentData;
                }
                
                lookData.DesiredLookDirection = sentData.DesiredLookDirection;
                lookData.VanillaMode = sentData.VanillaMode;
                lookData.OwlMode = AllowOwlModeForOthers.Value && sentData.OwlMode;
                lookData.LookSpeed = sentData.LookSpeed;
                lookData.OverrideDirection = sentData.OverrideDirection;
                _cachedPlayerData[player] = lookData;
            }
            
            ProcessPlayerHeadRotation(player);
        }
    }

    private void ProcessPlayerHeadRotation(Player player)
    {
        bool canDoStuff = Enabled.Value && player && (player == Player._mainPlayer || MultiplayerData.ContainsKey(player.netId));
        
        if (!canDoStuff)
            return;

        if (!_cachedPlayerData.TryGetValue(player, out var data))
        {
            var raceModel = player.GetComponentInChildren<PlayerRaceModel>();

            if (raceModel != null)
            {
                var head = GetHeadBone(raceModel);

                if (head != null)
                {
                    _cachedPlayerData[player] = data = new PlayerLookInfo()
                    {
                        DesiredLookDirection = player.transform.rotation,
                        RaceModel = raceModel,
                        Head = head,
                        LastHeadRotation = Quaternion.identity,
                        VanillaMode = false,
                        OwlMode = false,
                        OverrideDirection = LookDirection.Default,
                        LookSpeed = LookSpeed.Normal,
                    };
                }
            }
        }

        if (data.Head == null)
            return;

        if (data.VanillaMode)
        {
            data.LastHeadRotation = Quaternion.identity;
            data.DesiredLookDirection = player.transform.rotation;
            _cachedPlayerData[player] = data;
            return;
        }

        var playerTransform = player.transform;
        bool isMirrored = player._pVisual._playerAppearanceStruct._isLeftHanded;

        var targetLookRotation = data.DesiredLookDirection;
        var targetAngle = Quaternion.Angle(playerTransform.rotation, targetLookRotation);

        const float MaxHeadRotation = 70f;

        bool farLook = targetAngle >= 105;
        bool reallyFarLook = targetAngle >= 160;

        // When it's diametrally opposite from the forward direction and is using default mode,
        // opt to instead act as /observe camera
        var wantsToLookAt = data.OverrideDirection != LookDirection.Default || !reallyFarLook || data.OwlMode ? targetLookRotation : GetDefaultModeBackwardLookDirection(data, player);
        
        Quaternion atBestCanLookAt = data.OwlMode ? wantsToLookAt : Quaternion.RotateTowards(data.Head.parent.rotation, wantsToLookAt, MaxHeadRotation);
        var currentHeadRotation = Quaternion.Slerp(data.LastHeadRotation, atBestCanLookAt, Time.deltaTime * data.LookSpeed.MapToMultiplier());
        
        // Add a clamp in case we have a sudden direction change that would make the head do "funny" movements
        if (!data.OwlMode)
            currentHeadRotation = Quaternion.RotateTowards(data.Head.parent.rotation, currentHeadRotation, MaxHeadRotation);
        
        data.Head.rotation = currentHeadRotation;
        data.LastHeadRotation = data.Head.rotation;
        
        // If the character is mirrored, we need to also mirror the rotation on the X axis for it to be correct
        if (isMirrored)
        {
            var rot = data.Head.rotation;
            data.Head.rotation.Set(rot.x, -rot.y, -rot.z, rot.w);
        }

        if (!data.OwlMode && data.OverrideDirection == LookDirection.Default && farLook && !reallyFarLook)
        {
            if (data.RaceModel._currentEyeCondition == EyeCondition.Center)
            {
                float lookRightAngle = Quaternion.Angle(Quaternion.LookRotation(playerTransform.right * -1, playerTransform.up), wantsToLookAt);
                // If mirrored, need to reverse horizontal eye direction too
                data.RaceModel.Set_EyeCondition((lookRightAngle <= 90) != isMirrored ? EyeCondition.Right : EyeCondition.Left, 0.15f);
            }
        }

        _cachedPlayerData[player] = data;
    }

    private static Quaternion GetMainPlayerTargetRotation(Transform headTransform)
    {
        var player = Player._mainPlayer;

        bool cameraDisabled = CameraChecks.IsUsingFreecam() || (DialogManager._current && DialogManager._current._isDialogEnabled);
        
        switch (LocalOverrideDirection)
        {
            case LookDirection.Camera:
                if (cameraDisabled)
                    return headTransform.rotation;
                
                return Quaternion.LookRotation(-CameraFunction._current.transform.forward, player.transform.up);
            case LookDirection.Pose:
                return headTransform.parent.rotation * SavedOverride;
            case LookDirection.Environment:
                return SavedOverride;
            default:
                if (LookAtNPCDuringDialogue.Value)
                {
                    if (DialogManager._current._isDialogEnabled && DialogManager._current._cachedNpc)
                    {
                        var lookAt = DialogManager._current._cachedNpc.transform.position;
                        var lookFrom = headTransform.position;

                        var npcHeadBone = GetHeadBone(DialogManager._current._cachedNpc);

                        if (npcHeadBone != null)
                            lookAt = npcHeadBone.position;
                        
                        return Quaternion.LookRotation(lookAt - lookFrom, player.transform.up);
                    }
                }
                
                if (LookAtInteractables.Value)
                {
                    if (player.TryGetComponent(out PlayerInteract playerInteract) && playerInteract._interactReticleObject && playerInteract._interactReticleObject.gameObject.activeSelf)
                    {
                        var lookAt = playerInteract._interactReticleObject.position;
                        var lookFrom = headTransform.position;
                        
                        return Quaternion.LookRotation(lookAt - lookFrom, player.transform.up);
                    }
                }
                
                if (cameraDisabled)
                    return headTransform.rotation;

                // Look in line with camera if no overrides are specified
                return CameraFunction._current.transform.rotation;
        }
    }

    private static Quaternion GetDefaultModeBackwardLookDirection(PlayerLookInfo data, Player player)
    {
        bool lookAtYourself = false;
        
        if (lookAtYourself)
            return Quaternion.LookRotation(-(data.DesiredLookDirection * Vector3.forward), player.transform.up);
        
        return player.transform.rotation * Quaternion.Euler(0, 90, 0);
    }

    internal static Transform? GetHeadBone(PlayerRaceModel raceModel)
    {
        return raceModel._armatureTransform?.Find("Armature_character/masterBone/hipCtrl/hip/lowBody/midBody/torso/neck/head");
    }

    internal static Transform? GetHeadBone(NetNPC npc)
    {
        var meshRender = npc.GetComponentInChildren<SkinnedMeshRenderer>();
        
        if (!meshRender)
            return null;

        return meshRender.rootBone.Find("hipCtrl/hip/lowBody/midBody/torso/neck/head");
    }
}