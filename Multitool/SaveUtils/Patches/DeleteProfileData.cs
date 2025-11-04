using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Marioalexsan.Multitool.SaveUtils.Patches;

[HarmonyPatch(typeof(ProfileDataManager), nameof(ProfileDataManager.Delete_ProfileData))]
static class DeleteProfileData
{
    static void Postfix(ProfileDataManager __instance, int _index)
    {
        var characterFile = __instance._characterFiles[_index];

        try
        {
            ExecuteDeleteCallbacks(characterFile, _index);
            DeleteModdedData(_index);
        }
        catch (Exception e)
        {
            Logging.LogError($"Failed to execute {nameof(DeleteProfileData)} logic for mooded saves! Please report this error to the mod author!");
            Logging.LogError(e);
        }
    }

    static void DeleteModdedData(int index)
    {
        SaveUtilsAPI.ProfileDataStores.Remove(index);

        if (Directory.Exists(SaveUtilsAPI.SaveDirectory))
        {
            var savePath = SaveUtilsAPI.GetModdedCharacterProfilePath(index);

            if (File.Exists(savePath))
                File.Delete(savePath);
        }
    }

    static void ExecuteDeleteCallbacks(CharacterFile file,int index)
    {
        foreach (var store in SaveUtilsAPI.PluginConfiguration)
        {
            try
            {
                store.Value.OnDelete(file, index);
            }
            catch (Exception e)
            {
                Logging.LogWarning($"Failed to execute {nameof(DeleteProfileData)} callback for {store.Key}! This may cause issues with saved mod data.");
                Logging.LogWarning(e);
            }
        }
    }
}
