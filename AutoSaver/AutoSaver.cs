using System.Globalization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.AutoSaver.HarmonyReversePatches;
using Marioalexsan.AutoSaver.SoftDependencies;
using UnityEngine;

namespace Marioalexsan.AutoSaver;

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
public class AutoSaver : BaseUnityPlugin
{
    public const string MoreBankTabsIndentifier = "com.16mb.morebanktabs";

    public static AutoSaver Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(AutoSaver)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static AutoSaver? _plugin;

    public new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony;
    private TimeSpan _elapsedAutosaveTime;

    public ConfigEntry<bool> EnableAutosaving { get; private set; }
    public ConfigEntry<bool> SaveOnMapChange { get; private set; }
    public ConfigEntry<bool> EnableExperimentalFeatures { get; private set; }
    public ConfigEntry<bool> AppendSlotToSaveName { get; private set; }
    public ConfigEntry<KeyCode> SaveMultiplayerKeyCode { get; private set; }

    public ConfigEntry<int> BackupIntervalInMinutesConfig { get; private set; }
    public ConfigEntry<int> SaveCountToKeepConfig { get; private set; }

    public int BackupIntervalInMinutes { get; private set; }
    public int SaveCountToKeep { get; private set; }

    public bool IsInGame { get; private set; }

    public TimeSpan AutosaveInterval => TimeSpan.FromMinutes(BackupIntervalInMinutes);

    public bool DetectedMoreBankTabsMod { get; private set; } = false;

    private readonly char[] BannedChars = [.. Path.GetInvalidPathChars(), .. Path.GetInvalidFileNameChars(), '.'];

    private string ObsoleteModDataFolderPath => Path.Combine(ProfileDataManager._current._dataPath, ModDataFolderName);
    private readonly string ModDataFolderName = "Marioalexsan_AutoSaver";
    private string ModDataFolderPath => Path.Combine(Path.GetDirectoryName(Paths.ExecutablePath), ModDataFolderName);
    private string CharacterFolderPath => Path.Combine(ModDataFolderPath, "Characters");
    private string ItemBankFolderPath => Path.Combine(ModDataFolderPath, "ItemBank");
    private string MultiSavesFolderPath => Path.Combine(ModDataFolderPath, "Multi");

    private bool _checkedForOldAutosaverBackups = false;

    private string GameVersion => Application.version;

    public AutoSaver()
    {
        _plugin = this;
        Logger = base.Logger;
        _harmony = new Harmony(ModInfo.PLUGIN_GUID);

        EnableAutosaving = Config.Bind("General", "EnableAutosaving", true, "True to enable autosaving every few minutes, false to disable it.");
        SaveOnMapChange = Config.Bind("General", "SaveOnMapChange", false, "Set to true to trigger autosaving whenever a new level is loaded.");
        AppendSlotToSaveName = Config.Bind("General", "AppendSlotToSaveName", true, "Set to true to append slot index to the saved character names, false to use the character name only. Recommended if you use characters with duplicate names.");
        BackupIntervalInMinutesConfig = Config.Bind("General", "BackupInterval", 4, new ConfigDescription("Interval between save backups, in minutes.", new AcceptableValueRange<int>(1, 60))); // Has to be float range due to EasySettings
        SaveCountToKeepConfig = Config.Bind("General", "SavesToKeep", 15, new ConfigDescription("Maximum number of saves to keep.", new AcceptableValueRange<int>(5, 50))); // Has to be float range due to EasySettings

        BackupIntervalInMinutes = BackupIntervalInMinutesConfig.Value;
        SaveCountToKeep = SaveCountToKeepConfig.Value;

        EnableExperimentalFeatures = Config.Bind("Experimental", "EnableExperimentalFeatures", false, "Set to true to enable experimental features.");
        SaveMultiplayerKeyCode = Config.Bind(
            "Experimental", 
            "SaveMultiplayerKeyCode", 
            KeyCode.KeypadPlus,
            $"Key to trigger saving other people's saves in multiplayer under the \"Multi\" folder.{Environment.NewLine}Note that saves saved in this way are lackluster."
        );
    }

    private void Awake()
    {
        _harmony.PatchAll();
        InitializeConfiguration();

        Logging.LogInfo("AutoSaver initialized!");
    }

    private void CheckForOldAutosaverBackups()
    {
        if (Directory.Exists(ObsoleteModDataFolderPath))
        {
            if (Directory.Exists(ModDataFolderPath))
            {
                Logging.LogWarning($"Found old Autosaver folder in {ObsoleteModDataFolderPath}, but new save location {ModDataFolderPath} already exists.");
                Logging.LogWarning($"Please backup and move {ObsoleteModDataFolderPath} out of the profileCollections folder manually!");
                return;
            }

            Logging.LogInfo($"Found old Autosaver folder in {ObsoleteModDataFolderPath}, will try to move it to the new save location {ModDataFolderPath}.");

            try
            {
                Directory.Move(ObsoleteModDataFolderPath, ModDataFolderPath);
                Logging.LogInfo($"Backup data moved successfully to the new location!");
            }
            catch (Exception e)
            {
                Logging.LogError($"Failed to move old backup folder! Please send the exception message to the mod developer:");
                Logging.LogError(e.ToString());
                Logging.LogError($"Please check {ObsoleteModDataFolderPath} and backup and move it out of the profileCollections folder manually!");
            }
        }
    }

    private void InitializeConfiguration()
    {
        if (EasySettings.IsAvailable)
        {
            EasySettings.OnApplySettings.AddListener(() =>
            {
                try
                {
                    Config.Save();

                    // This is set on apply since otherwise it would cause things
                    // to get garbage collected or saved ahead of time if we take the values it applies on the fly
                    BackupIntervalInMinutes = BackupIntervalInMinutesConfig.Value;
                    SaveCountToKeep = SaveCountToKeepConfig.Value;
                }
                catch (Exception e)
                {
                    Logging.LogError($"AutoSaver crashed in OnApplySettings! Please report this error to the mod developer:");
                    Logging.LogError(e.ToString());
                }
            });
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddHeader(ModInfo.PLUGIN_NAME);
                EasySettings.AddToggle("Enable periodic autosaves", EnableAutosaving);
                EasySettings.AddToggle("Enable autosaving whenever map changes", SaveOnMapChange);
                EasySettings.AddToggle("Append save slot to save names (recommended)", AppendSlotToSaveName);
                // EnableExperimentalFeatures is skipped on purpose
                EasySettings.AddAdvancedSlider("Backup interval (minutes)", BackupIntervalInMinutesConfig);
                EasySettings.AddAdvancedSlider("Number of saves to keep", SaveCountToKeepConfig);
                // SaveMultiplayerKeyCode is skipped on purpose

                EasySettings.AddButton("Open save backups folder", () =>
                {
                    Application.OpenURL(new Uri($"{ModDataFolderPath}").AbsoluteUri);
                });
            });
        }
    }

    internal void GameEntered()
    {
        bool runSave = false;

        if (!IsInGame)
        {
            IsInGame = true;
            runSave = true;
            Logging.LogInfo("Game entered. Activating character autosaves.");
        }
        else if (SaveOnMapChange.Value)
        {
            runSave = true;
            Logging.LogInfo("Map changed. Autosaving.");
        }

        if (runSave)
        {
            ProfileDataManager._current.Load_ItemStorageData(); // This isn't loaded on start for some reason
            RunAutosaves();
        }
    }

    internal void GameExited()
    {
        Logging.LogInfo("Game exited. Stopping character autosaves.");
        RunAutosaves();
        IsInGame = false;
    }

    private void Update()
    {
        // Cannot be done in Awake since it relies on dataPath to be available
        if (!_checkedForOldAutosaverBackups && ProfileDataManager._current?._dataPath != null)
        {
            _checkedForOldAutosaverBackups = true;
            CheckForOldAutosaverBackups();
        }

        if (!DetectedMoreBankTabsMod)
        {
            if (Chainloader.PluginInfos.ContainsKey(MoreBankTabsIndentifier))
            {
                DetectedMoreBankTabsMod = true;
                Logging.LogInfo($"Detected MoreBankTabs mod ({MoreBankTabsIndentifier}).");
                Logging.LogInfo($"Will try to backup the extra bank tabs.");
            }
        }

        if (IsInGame)
        {
            _elapsedAutosaveTime += TimeSpan.FromSeconds(Time.deltaTime);

            if (EnableAutosaving.Value)
            {
                if (_elapsedAutosaveTime >= AutosaveInterval)
                {
                    Logging.LogInfo($"Periodic autosave triggered ({AutosaveInterval.TotalMinutes} minutes have passed).");
                    RunAutosaves();
                }
            }

            // Retry if player is buffering or w/e, and the last round of saving didn't finish
            // This applies to autosaves, and map changes
            if (!CharacterAutoSaver.SaveDone)
            {
                AutosaveCurrentCharacter();
            }
            if (!ItemBankAutoSaver.SaveDone)
            {
                AutosaveCurrentItemBank();
            }
        }

        if (EnableExperimentalFeatures.Value && Input.GetKeyDown(SaveMultiplayerKeyCode.Value))
        {
            Logging.LogInfo("[Experimental] Saving every online player's saves.");
            
            foreach (var player in FindObjectsOfType<Player>())
            {
                Logging.LogInfo($"Saving player data for {player._nickname}");
                Directory.CreateDirectory(MultiSavesFolderPath);
                CharacterAutoSaver.TrySaveSpecificProfileToLocation(player, Path.Combine(MultiSavesFolderPath, SanitizeString(player._nickname)));

                if (!CharacterAutoSaver.SaveDone)
                {
                    Logging.LogWarning($"Failed to do save for {player._nickname}. Current game status is {player._currentGameCondition}.");
                }
            }
        }
    }

    private void RunAutosaves()
    {
        _elapsedAutosaveTime = TimeSpan.Zero;
        AutosaveCurrentCharacter();
        AutosaveCurrentItemBank();
    }

    public string SanitizedCurrentTime => SanitizeStringSimple(DateTime.UtcNow.ToString("yyyy:MM:dd-HH:mm:ss", CultureInfo.InvariantCulture));

    public string SanitizeStringSimple(string str)
    {
        for (int i = 0; i < BannedChars.Length; i++)
        {
            str = str.Replace($"{BannedChars[i]}", $"_");
        }

        return str;
    }

    public string SanitizeString(string str)
    {
        for (int i = 0; i < BannedChars.Length; i++)
        {
            int codePoint = char.ConvertToUtf32($"{BannedChars[i]}", 0);
            str = str.Replace($"{BannedChars[i]}", $"_{codePoint}");
        }

        return str;
    }

    public string GetBackupNameForCurrentPlayer()
    {
        if (AppendSlotToSaveName.Value)
        {
            return $"{SanitizeString(Player._mainPlayer._nickname)}_slot{ProfileDataManager._current._selectedFileIndex}";
        }
        else
        {
            return $"{SanitizeString(Player._mainPlayer._nickname)}";
        }
    }

    private void RunItemBankGarbageCollector()
    {
        if (!Directory.Exists(ItemBankFolderPath))
        {
            Logging.LogWarning($"Couldn't find backup path {ItemBankFolderPath} to run garbage collection for.");
            return;
        }

        List<string> names = [];
        foreach (var directory in Directory.EnumerateDirectories(ItemBankFolderPath))
        {
            names.Add(Path.GetFileName(directory));
        }

        names.RemoveAll(x => x.Contains("_latest"));
        names.Sort();

        while (names.Count > SaveCountToKeep)
        {
            var saveToDelete = names[0];
            names.RemoveAt(0);

            var targetPath = Path.Combine(ItemBankFolderPath, saveToDelete);

            // Failsafe
            if (!targetPath.Contains(ModDataFolderName))
                throw new InvalidOperationException($"Got an invalid folder to delete {targetPath}, please notify the mod developer!");

            Directory.Delete(Path.Combine(ItemBankFolderPath, saveToDelete), true);
        }
    }

    private void RunCharacterGarbageCollector()
    {
        var backupPath = Path.Combine(CharacterFolderPath, GetBackupNameForCurrentPlayer());

        if (!Directory.Exists(backupPath))
        {
            Logging.LogWarning($"Couldn't find backup path {backupPath} to run garbage collection for.");
            return;
        }    

        List<string> names = [];
        foreach (var file in Directory.EnumerateFiles(backupPath))
        {
            names.Add(Path.GetFileName(file));
        }

        names.RemoveAll(x => x.Contains("_latest"));
        names.Sort();

        while (names.Count > SaveCountToKeep)
        {
            var saveToDelete = names[0];
            names.RemoveAt(0);

            var targetPath = Path.Combine(CharacterFolderPath, GetBackupNameForCurrentPlayer(), saveToDelete);

            // Failsafe
            if (!targetPath.Contains(ModDataFolderName))
                throw new InvalidOperationException($"Got an invalid file to delete {targetPath}, please notify the mod developer!");

            File.Delete(targetPath);
        }
    }

    private void AutosaveCurrentItemBank()
    {
        try
        {
            Directory.CreateDirectory(ModDataFolderPath);
            var itembankFolder = Path.Combine(ItemBankFolderPath, SanitizedCurrentTime);
            Directory.CreateDirectory(itembankFolder);

            ItemBankAutoSaver.TrySaveCurrentProfileToLocation(itembankFolder);

            if (ItemBankAutoSaver.SaveDone)
            {
                if (DetectedMoreBankTabsMod)
                    ItemBankAutoSaver.SaveModBankTabsToLocation(itembankFolder);

                void CopySaves(string saveName)
                {
                    var latestSave = Path.Combine(ItemBankFolderPath, saveName);
                    Directory.CreateDirectory(latestSave);

                    foreach (var path in Directory.EnumerateFiles(itembankFolder))
                    {
                        File.Copy(path, Path.Combine(latestSave, Path.GetFileName(path)), true);
                    }
                }

                CopySaves("_latest");
                CopySaves($"_latest_version_{SanitizeStringSimple(GameVersion)}");
            }

            RunItemBankGarbageCollector();
        }
        catch (Exception e)
        {
            Logging.LogError($"Failed to autosave!");
            Logging.LogError($"Exception message: {e}");
            return;
        }
    }

    private void AutosaveCurrentCharacter()
    {
        try
        {
            if (!(bool)Player._mainPlayer)
            {
                Logging.LogError($"Couldn't autosave! No main player found.");
                return;
            }

            Directory.CreateDirectory(ModDataFolderPath);
            var characterFolder = Path.Combine(CharacterFolderPath, GetBackupNameForCurrentPlayer());
            Directory.CreateDirectory(characterFolder);

            var filePath = Path.Combine(characterFolder, SanitizedCurrentTime);

            CharacterAutoSaver.TrySaveCurrentProfileToLocation(filePath);

            if (CharacterAutoSaver.SaveDone)
            {
                File.Copy(filePath, Path.Combine(characterFolder, "_latest"), true);
                File.Copy(filePath, Path.Combine(characterFolder, $"_latest_version_{SanitizeStringSimple(GameVersion)}"), true);
            }

            RunCharacterGarbageCollector();
        }
        catch (Exception e)
        {
            Logging.LogError($"Couldn't autosave!");
            Logging.LogError($"Exception message: {e}");
            return;
        }
    }
}
