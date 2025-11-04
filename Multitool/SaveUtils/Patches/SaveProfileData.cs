using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.Multitool.SaveUtils.Patches;

[HarmonyPatch(typeof(ProfileDataManager), nameof(ProfileDataManager.Save_ProfileData))]
[HarmonyPriority(Priority.First)]
static class SaveProfileData
{
    static void Postfix(ProfileDataManager __instance)
    {
        var index = __instance._selectedFileIndex;
        var characterFile = __instance._characterFile;

        try
        {
            ExecuteSaveCallbacks(characterFile, index);
            SaveModdedData(characterFile, index);
        }
        catch (Exception e)
        {
            Logging.LogError($"Failed to execute {nameof(SaveProfileData)} logic for mooded saves! Please report this error to the mod author!");
            Logging.LogError(e);
        }
    }

    static void SaveModdedData(CharacterFile file, int index)
    {
        if (!Directory.Exists(SaveUtilsAPI.SaveDirectory))
            Directory.CreateDirectory(SaveUtilsAPI.SaveDirectory);

        var tokens = SaveUtilsAPI.ProfileDataStores.TryGetValue(index, out var profileStore)
            ? profileStore.StoredData
            : [];

        var pluginDataStore = JToken.FromObject(tokens);

        var profile = new JObject
        {
            ["multitoolVersion"] = ModInfo.VERSION,
            ["mods"] = pluginDataStore
        };

        var savePath = SaveUtilsAPI.GetModdedCharacterProfilePath(index);

        File.WriteAllText(savePath, profile.ToString(Newtonsoft.Json.Formatting.Indented));
    }

    static void ExecuteSaveCallbacks(CharacterFile file, int index)
    {
        if (!SaveUtilsAPI.ProfileDataStores.TryGetValue(index, out var profileStore))
            SaveUtilsAPI.ProfileDataStores[index] = profileStore = new(file, []);

        foreach (var store in SaveUtilsAPI.PluginConfiguration)
        {
            try
            {
                store.Value.OnSave(file, index, out var data);

                // A null value means "remove the save entry if it exists"
                if (data == null)
                {
                    profileStore.StoredData.Remove(store.Key);
                }
                else
                {
                    profileStore.StoredData[store.Key] = data;
                }
            }
            catch (Exception e)
            {
                Logging.LogWarning($"Failed to execute {nameof(SaveProfileData)} callback for {store.Key}! This may cause issues with saved mod data.");
                Logging.LogWarning(e);
            }
        }
    }
}
