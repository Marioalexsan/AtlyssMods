using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace Marioalexsan.Multitool.SaveUtils.Patches;

[HarmonyPatch(typeof(ProfileDataManager), nameof(ProfileDataManager.Load_ProfileData))]
[HarmonyPriority(Priority.First)]
static class LoadProfileData
{
    private static readonly object CallbackLock = new();

    static void Postfix(ProfileDataManager __instance, int _index)
    {
        var characterFile = __instance._characterFiles[_index];

        if (characterFile._isEmptySlot)
            return;

        try
        {
            // Loads are done in a very weird async / multithreaded manner (see ProfileDataManager.LoadPlayerDataAsync)
            // This almost always causes issues for mod code, so try to force the game to process it one at a time with a mutex
            lock (CallbackLock)
            {
                LoadModdedData(characterFile, _index);
                ExecuteLoadCallbacks(characterFile, _index);
            }
        }
        catch (Exception e)
        {
            Logging.LogError($"Failed to execute {nameof(LoadProfileData)} logic for mooded saves! Please report this error to the mod author!");
            Logging.LogError(e);
        }
    }

    static void LoadModdedData(CharacterFile file, int index)
    {
        if (!Directory.Exists(SaveUtilsAPI.SaveDirectory))
            Directory.CreateDirectory(SaveUtilsAPI.SaveDirectory);

        var savePath = SaveUtilsAPI.GetModdedCharacterProfilePath(index);

        var modTokens = new Dictionary<string, JToken>();

        SaveUtilsAPI.ProfileDataStores[index] = new ProfileDataStore(file, modTokens);

        if (!File.Exists(savePath))
            return;

        var tokens = JToken.Parse(File.ReadAllText(savePath));

        if (tokens is not JObject profile)
        {
            Logging.LogWarning("Couldn't parse character file plugin profile data.");
            return;
        }

        if (profile["mods"] is not JObject pluginDataStore)
        {
            Logging.LogWarning("Couldn't parse character file plugin profile data.");
            return;
        }

        foreach (var item in pluginDataStore)
        {
            if (item.Value is JObject obj)
            {
                modTokens[item.Key] = obj;
            }
            else
            {
                Logging.LogWarning($"Profile data key {item.Key} is not an object. This may cause issues with saved mod data.");
            }
        }
    }

    static void ExecuteLoadCallbacks(CharacterFile file, int index)
    {
        var profileStore = SaveUtilsAPI.ProfileDataStores[index];

        foreach (var store in SaveUtilsAPI.PluginConfiguration)
        {
            var data = profileStore.StoredData.GetValueOrDefault(store.Key);

            try
            {
                store.Value.OnLoad(file, index, data);
            }
            catch (Exception e)
            {
                Logging.LogWarning($"Failed to execute {nameof(LoadProfileData)} callback for {store.Key}! This may cause issues with saved mod data.");
                Logging.LogWarning(e);
            }
        }
    }
}
