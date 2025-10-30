# Multitool

Swiss army knife of APIs for Atlyss mod development.

This is an utility library / plugin that can be used by mods for common tasks and patterns encountered in mods, such as custom save data.

This library comes with an XML plugin documenting the public API surface. You can use this together with your IDE to explore its functionality.

Keep in mind that if you're a regular user, there is no reason to install this plugin by itself. It needs to be installed alongside the mods that use it.

# Table of contents

- SaveUtils
- HierarchyTraverse

# SaveUtils

At the moment, this module allows you to save and load custom data as part of character profile saves.

All of the related functionality can be accessed using the `Marioalexsan.Multitool.SaveUtils.SaveUtilsAPI` class, and other objects found in the `Marioalexsan.Multitool.SaveUtils` namespace.

## Custom Profile Data

To enable saving modded profile data, you must first register your custom data using an unique identifier. Usually, you can use your mod's GUID for this. For example:

```cs
SaveUtilsAPI.RegisterProfileData<T>(ModInfo.GUID, SaveProfileData, LoadProfileData, DeleteProfileData);
```

Once you've setup custom data, you must keep the same GUID at all times, since it is used for loading and deserializing mod data from disk.

The registration requires you to supply five parameters, as follows:
1. The type of the custom data to be serialized and deserialized, as the generic type of the method, `T`
2. The GUID to use for this custom data
3. A callback for saving profile data, with the signature `void SaveProfileData<T>(CharacterFile file, int slotIndex, out T? data)`
4. A callback for loading profile data, with the signature `void LoadProfileData<T>(CharacterFile file, int slotIndex, T? data)`
5. A callback for deleting profile data, with the signature `void DeleteProfileData(CharacterFile file, int slotIndex)`

### Custom data

The custom data to be serialized and deserialized is provided as a generic parameter for `SaveUtilsAPI.RegisterProfileData`.

You can customize serialization using Newtonsoft.Json attributes, such as `JsonProperty`.

### GUID

The GUID you specify for your custom data should be unique from any other custom mod data's GUID.

Usually, you can use your BepInEx plugin's GUID for this field.

Once you start using a GUID, you need to keep it unchanged between mod updates, since this is used by Multitool to identify and load your custom data.

### Save profile callback

The save callback, `void SaveProfileData<T>(CharacterFile file, int slotIndex, out T? data)`, is called whenever the game is currently saving a character profile.

To save your custom data, set the `T? data` parameter to the custom data that you want to be serialized and saved on disk for the given save slot.

You can specify `null` as the data to be serialized, which will erase the saved custom data from the disk.

Keep in mind that this callback will be called both for existing characters, and whenever a new character is created. This means that you need to handle the case where you might not have any existing data available for that character yet.

Sample API usage:

```cs
Dictionary<int, MyModData> CustomSaveData = [];

void SaveProfileData(CharacterFile file, int index, out MyModData? saveData)
{
    if (!CustomSaveData.TryGetValue(index, out saveData))
    {
        saveData = new()
        {
            RichTextName = file._nickName
        };
    }
}
```

### Load profile callback

The load callback, `void LoadProfileData<T>(CharacterFile file, int slotIndex, T? data)`, is called whenever the game is currently loading a character profile.

If there is custom data available for this profile, `T? data` will contain a deserialized object with that data. Otherwise, it will be `null`.

The data can be `null` whenever a vanilla character is loaded, or the Multitool save file didn't contain any existing data for your mod. This means that you need to handle the case where you might not have any existing data available for that character yet.

Keep in mind that Atlyss has some quirks when it comes to loading profile saves. Your save callback might be called multiple times for a specific save slot, each time with a different `CharacterFile` object containing the same data. It's recommended you store your custom mod data in your plugin based on the save slot's index, rather than storing it based on the `CharacterFile` reference.

Sample API usage:

```cs
Dictionary<int, MyModData> CustomSaveData = [];

void LoadProfileData(CharacterFile file, int index, MyModData? saveData)
{
    saveData ??= new()
    {
        RichTextName = file._nickName
    };

    CustomSaveData[index] = saveData;
}
```

### Delete profile callback

The delete callback, `void DeleteProfileData(CharacterFile file, int slotIndex)`, is called whenever the game is about to delete a character profile.

You can use this callback to remove associated mod data for that profile, thus cleaning up any obsolete objects.

Keep in mind that regardless of what you do in your callback, Multitool will proceed to delete all stored custom data for the given slot.

Sample API usage:

```cs
Dictionary<int, MyModData> CustomSaveData = [];

static void DeleteProfileData(CharacterFile file, int index)
{
    CustomSaveData.Remove(index);
}
```

# HierarchyTraverse

This is a set of extensions for `GameObject`, allowing you to express various actions on hierarchies in a simpler way.

All of the related functionality can be accessed using the `Marioalexsan.Multitool.Utils.HierarchyTraverse` class.

This can be useful when editing the UI at runtime. Since there's a lot of components and operations involved, the simplified API can make the code easier to read and reason about.

Sample API usage:

```cs
var backdrop = GameObject.Find("_backdrop_deleteCharacter");
GameObject renameInputText = null;

backdrop
  .Rename("_backdrop_renameCharacter")
  .ForChild("_text_characterDeletePrompt", prompt =>
  {
      prompt
          .Rename("_text_characterRenamePrompt")
          .ForComponent<Text>(x => x.text = "Type in the new name for the character");
  })
  .ForChild("_input_characterDeleteConfirm", input =>
  {
      input
          .Rename("_input_characterRenameConfirm")
          .SaveComponentRef(ref renameInputText)
          .ForComponent<InputField>(input =>
          {
              input.characterLimit = MaxRichTextNameLength;
              input.characterValidation = InputField.CharacterValidation.None;
          });
  });
```

# Mod Compatibility

Multitool targets the following game versions and mods:

- ATLYSS 102025.a5

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.