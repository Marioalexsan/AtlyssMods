using BepInEx;
using Marioalexsan.Multitool.SaveUtils.Patches;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using static UnityEngine.UIElements.VisualElement;

namespace Marioalexsan.Multitool.SaveUtils;

/// <summary>
/// Callback that is used for saving modded profile data.
/// <para/>
/// This function has two behaviours depending on the data written to <paramref name="data"/>:
/// <list type="bullet">
/// <item>When <paramref name="data"/> is non-null, the new save data is serialized and stored to disk.</item>
/// <item>When <paramref name="data"/> is null, the existing save data is erased.</item>
/// </list>
/// This method is called both when a fresh profile is created, and when an existing profile is saved. Your code must be able to handle both scenarios.
/// </summary>
/// <typeparam name="T">The type that will hold the data to be used for serialization.</typeparam>
/// <param name="file">The vanilla save file for which moded data is being saved.</param>
/// <param name="slotIndex">The slot index for which modded data is being saved.</param>
/// <param name="data">The new data to save, or <see langword="null"/> to erase existing data.</param>
public delegate void SaveProfileData<T>(CharacterFile file, int slotIndex, out T? data) where T : class;

/// <summary>
/// Callback that is used for loading modded profile data.
/// <para/>
/// The <paramref name="data"/> parameter will be non-null if there is existing data in the save file, otherwise it will be null.
/// </summary>
/// <typeparam name="T">The type that will hold the data to be used for serialization.</typeparam>
/// <param name="file">The vanilla save file for which moded data is being loaded.</param>
/// <param name="slotIndex">The slot index for which modded data is being loaded.</param>
/// <param name="data">The data present in the modded save file, or <see langword="null"/> if there is no existing data.</param>
public delegate void LoadProfileData<T>(CharacterFile file, int slotIndex, T? data) where T : class;

/// <summary>
/// Callback that is invoked whenever a profile is deleted by the player. The modded data associated with the profile is automatically deleted alongside it.
/// <para/>
/// You can use this callback to perform any additional cleanup procedures for the given profile.
/// </summary>
/// <param name="file">The vanilla save file that is about to be deleted.</param>
/// <param name="slotIndex">The slot index for which modded data is being deleted.</param>
public delegate void DeleteProfileData(CharacterFile file, int slotIndex);

internal delegate void SaveRawProfileData(CharacterFile file, int slotIndex, out JToken? data);
internal delegate void LoadRawProfileData(CharacterFile file, int slotIndex, JToken? data);

internal class PluginSaveConfig(SaveRawProfileData onSave, LoadRawProfileData onLoad, DeleteProfileData onDelete)
{
    public SaveRawProfileData OnSave { get; set; } = onSave;
    public LoadRawProfileData OnLoad { get; set; } = onLoad;
    public DeleteProfileData OnDelete { get; set; } = onDelete;
}

internal class ProfileDataStore(CharacterFile associatedFile, Dictionary<string, JToken> storedData)
{
    public CharacterFile AssociatedFile { get; set; } = associatedFile;
    public Dictionary<string, JToken> StoredData { get; set; } = storedData;
}

/// <summary>
/// Utilities for saving custom mod data in a reliable way.
/// </summary>
public static class SaveUtilsAPI
{
    internal static readonly Dictionary<string, PluginSaveConfig> PluginConfiguration = [];

    // This is indexed by save slot index, and not CharacterFile
    // This is because due to vanilla game quirks, which might cause us to load the same file multiple times,
    // generating multiple CharacterFile objects per slot
    internal static Dictionary<int, ProfileDataStore> ProfileDataStores { get; } = [];

    /// <summary>
    /// Gets the save directory used for storing modded save data.
    /// </summary>
    public static string SaveDirectory => Path.Combine(Path.GetDirectoryName(Paths.ExecutablePath), "ATLYSS_Data", "profileCollections", "Marioalexsan_Multitool");

    /// <summary>
    /// Gets the path to a modded character profile's save data.
    /// </summary>
    /// <param name="index">The index of the save file.</param>
    /// <returns>The path for the modded save data.</returns>
    public static string GetModdedCharacterProfilePath(int index) => Path.Combine(SaveDirectory, $"atl_characterProfile_{index}_mods");

    /// <summary>
    /// Registers handlers for modded profile data.
    /// <para/>
    /// To customize serialization behaviour, use attributes from Netwonsoft.Json, such as <see cref="Newtonsoft.Json.JsonPropertyAttribute"/>.
    /// </summary>
    /// <typeparam name="T">The type that will hold the data to be used for serialization.</typeparam>
    /// <param name="guid">An unique identifier for your modded save data. This should remain unchanged between updates. For most mods, using <see cref="BepInPlugin.GUID"/> is good enough.</param>
    /// <param name="onSave">The callback to use for saving profile data.</param>
    /// <param name="onLoad">The callback to use when loading profile data.</param>
    /// <param name="onDelete">The callback to use when a profile is deleted.</param>
    public static void RegisterProfileData<T>(string guid, SaveProfileData<T> onSave, LoadProfileData<T> onLoad, DeleteProfileData onDelete) where T : class
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            Logging.LogWarning($"GUID {guid} is invalid!");
            return;
        }

        if (PluginConfiguration.ContainsKey(guid))
        {
            Logging.LogWarning($"Mod {guid} already registered profile save data!");
            return;
        }

        void WrappedOnSave(CharacterFile file, int slotIndex, out JToken? data)
        {
            onSave(file, slotIndex, out var typedData);
            data = typedData != null ? JToken.FromObject(typedData) : null;
        }

        void WrappedOnLoad(CharacterFile file, int slotIndex, JToken? data)
        {
            T? typedData;
            try
            {
                typedData = data?.ToObject<T>();
            }
            catch (Exception e)
            {
                Logging.LogWarning($"Failed to deserialize modded data of type {typeof(T).Name} for {guid}! This may cause issues with saved mod data.");
                Logging.LogWarning($"Will try to load without any modded save data available.");
                Logging.LogWarning(e);
                typedData = null;
            }
            onLoad(file, slotIndex, typedData);
        }

        PluginConfiguration[guid] = new PluginSaveConfig(WrappedOnSave, WrappedOnLoad, onDelete);
    }
}
