using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CodeTalker.Networking;
using CodeTalker.Packets;
using HarmonyLib;
using Nessie.ATLYSS.EasySettings;
using UnityEngine;

namespace Marioalexsan.Observe;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("EasySettings", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("CodeTalker")]
public class ObservePlugin : BaseUnityPlugin
{
    private struct PlayerLookInfo
    {
        public Quaternion CameraRotation;
        public PlayerRaceModel RaceModel;
        public Transform Head;
        public Quaternion LastHeadRotation;
        public bool IgnoreCamera;
    }

    private struct SentPlayerInfo
    {
        public Quaternion CameraRotation;
        public bool IgnoreCamera;
        public DateTime Timestamp;
    }
    
    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");

    private readonly Dictionary<Player, PlayerLookInfo> _cachedPlayerData = [];
    private readonly Dictionary<ulong, SentPlayerInfo> MultiplayerData = [];
    internal static HashSet<Player> Players { get; } = [];
    
    private static ConfigEntry<bool> Enabled = null!;
    private static ConfigEntry<bool> IgnoreCameraSetting = null!;
    private static ConfigEntry<bool> EnableNetworking = null!;

    private bool _lastSendEnableState = false;
    private bool _shouldSendCurrentState = false;

    private static TimeSpan RefreshDirectionAccumulator = TimeSpan.Zero;
    private static TimeSpan PacketSendCooldown = TimeSpan.Zero;

    public ObservePlugin()
    {
        Enabled = Config.Bind("General", "Enabled", true, "Enable or disable mod functionality completely.");
        IgnoreCameraSetting = Config.Bind("General", "IgnoreCamera", false, "While active, makes your player look forward instead of using the camera direction.");
        EnableNetworking = Config.Bind("General", "EnableNetworking", true, "Enable sending/receiving camera directions to/from people with the mod installed.");
    }

    private void Awake()
    {
        _harmony.PatchAll();
        
        if (Chainloader.PluginInfos.ContainsKey("EasySettings"))
            RegisterEasySettings();

        CodeTalkerNetwork.RegisterBinaryListener<LookPacket>(HandleNetworkSyncFromOthers);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private void RegisterEasySettings()
    {
        Settings.OnInitialized.AddListener(() =>
        {
            Settings.ModTab.AddHeader("Observe");
            Settings.ModTab.AddToggle("Enabled", Enabled);
            Settings.ModTab.AddToggle("Ignore Camera (self)", IgnoreCameraSetting);
            Settings.ModTab.AddToggle("Enable Networking", EnableNetworking);
        });
        Settings.OnApplySettings.AddListener(() =>
        {
            Config.Save();
        });
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

        Logger.LogInfo("Received packet");
        MultiplayerData[lookPacket.TargetNetId] = new SentPlayerInfo()
        {
            CameraRotation = lookPacket.CameraRotation,
            IgnoreCamera = lookPacket.IgnoreCamera,
            Timestamp = DateTime.UtcNow,
        };
    }

    private void LateUpdate()
    {
        HandlePlayerCleanup();
        HandleNetworkSyncToOthers();
        HandleLocalPlayers();
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

    private void HandleNetworkSyncToOthers()
    {
        if (Player._mainPlayer && _cachedPlayerData.TryGetValue(Player._mainPlayer, out var mainPlayerLookData))
        {
            // Send every 3 seconds or every 30 degree camera change
            const double SecondsPerSend = 3f;
            const double DegreesPerSend = 30;

            if (_lastSendEnableState || EnableNetworking.Value)
            {
                bool ignoreCameraSwitched = mainPlayerLookData.IgnoreCamera != IgnoreCameraSetting.Value;
            
                if (IgnoreCameraSetting.Value)
                    RefreshDirectionAccumulator -= TimeSpan.FromSeconds(Time.deltaTime);
                else
                {
                    var angleChange = Quaternion.Angle(mainPlayerLookData.CameraRotation, CameraFunction._current.transform.rotation);
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
            
            // Add local settings to cache
            mainPlayerLookData.CameraRotation = CameraFunction._current.transform.rotation;
            mainPlayerLookData.IgnoreCamera = IgnoreCameraSetting.Value;
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
            
            foreach (var player in Players)
            {
                if (player != Player._mainPlayer)
                {
                    LookPacket.Instance.CameraRotation = IgnoreCameraSetting.Value ? Quaternion.identity : CameraFunction._current.transform.rotation;
                    LookPacket.Instance.TargetNetId = Player._mainPlayer.netId;
                    LookPacket.Instance.IgnoreCamera = IgnoreCameraSetting.Value;
                    Logger.LogInfo("Sent packet");
                    CodeTalkerNetwork.SendNetworkPacket(player, LookPacket.Instance);
                }
            }
        }
    }

    private void HandleLocalPlayers()
    {
        foreach (var player in Players)
        {
            if (_cachedPlayerData.TryGetValue(player, out var lookData) && MultiplayerData.TryGetValue(player.netId, out var sentData))
            {
                // Check if network peer stopped sending directions for some reason
                // If so, assume they disabled sending info and make them act as vanilla
                if (sentData.Timestamp + TimeSpan.FromSeconds(6) < DateTime.UtcNow)
                {
                    sentData.IgnoreCamera = true;
                    MultiplayerData[player.netId] = sentData;
                }
                
                lookData.CameraRotation = sentData.CameraRotation;
                lookData.IgnoreCamera = sentData.IgnoreCamera;
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
                var head = raceModel._armatureTransform
                    ?.Find("Armature_character")
                    ?.Find("masterBone")
                    ?.Find("hipCtrl")
                    ?.Find("hip")
                    ?.Find("lowBody")
                    ?.Find("midBody")
                    ?.Find("torso")
                    ?.Find("neck")
                    ?.Find("head");

                if (head != null)
                {
                    _cachedPlayerData[player] = data = new PlayerLookInfo()
                    {
                        CameraRotation = player.transform.rotation,
                        RaceModel = raceModel,
                        Head = head,
                        LastHeadRotation = Quaternion.identity,
                        IgnoreCamera = false
                    };
                }
            }
        }

        if (data.Head == null)
            return;

        if (data.IgnoreCamera)
        {
            data.LastHeadRotation = Quaternion.identity;
            data.CameraRotation = player.transform.rotation;
            _cachedPlayerData[player] = data;
            return;
        }

        var playerTransform = player.transform;
        var cameraRotation = data.CameraRotation;

        var cameraAngle = Quaternion.Angle(playerTransform.rotation, cameraRotation);

        const float MaxHeadRotation = 70f;

        bool farLook = cameraAngle >= 105;
        bool reallyFarLook = cameraAngle >= 160;

        // Prefer looking over the right shoulder if camera is towards the back
        var cameraRotationToConsider = !reallyFarLook ? cameraRotation : Quaternion.LookRotation(playerTransform.right, playerTransform.up);
        Quaternion wantsToLookAt = Quaternion.RotateTowards(data.Head.parent.rotation, cameraRotationToConsider, MaxHeadRotation);
        var currentHeadRotation = Quaternion.Slerp(data.LastHeadRotation, wantsToLookAt, Time.deltaTime * 3.5f);
        
        // Add a clamp in case we have a sudden direction change that would make the head do "funny" movements
        currentHeadRotation = Quaternion.RotateTowards(data.Head.parent.rotation, currentHeadRotation, MaxHeadRotation);
        
        data.Head.rotation = currentHeadRotation;
        data.LastHeadRotation = data.Head.rotation;

        if (farLook)
        {
            if (data.RaceModel._currentEyeCondition == EyeCondition.Center)
            {
                float lookRightAngle = Quaternion.Angle(Quaternion.LookRotation(playerTransform.right * -1, playerTransform.up), cameraRotationToConsider);
                data.RaceModel.Set_EyeCondition(lookRightAngle <= 90 ? EyeCondition.Right : EyeCondition.Left, 0.15f);
            }
        }

        _cachedPlayerData[player] = data;
    }
}