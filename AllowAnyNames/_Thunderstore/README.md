# AllowAnyNames

Removes restrictions on player names, allowing you to use color tags and longer names than usual.

The markup format that you can use for rich text is documented as part of Unity here: https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/StyledText.html

There are two options for specifying custom names:
- You can specify rich text as part of the character creation's character name field
- You can give a character a new rich text name using the new "Rename Character" UI option in character select

The Rich Text name will be visible by other people that have AllowAnyNames in servers. This functionality requires [CodeYapper](https://thunderstore.io/c/atlyss/p/Soggy_Pancake/CodeYapper/) to be installed.

Players that don't have AllowAnyNames will see a sanitized version of your Rich Text name. Additionally, elements such as "Delete Character" will use the sanitized version of characters to execute their actions.

For example, given the name:

```
<color=blue>Coboa</color>
```

Other players with AAN will see a blue "Coboa" name, vanilla players will see a plain text "Coboa" name, and "Delete Character" will prompt you to enter a plain text "Coboa".

![](https://marioalexsan.github.io/assets/atlyss/allowanynames/character_nameplate.png)
![](https://marioalexsan.github.io/assets/atlyss/allowanynames/rename_character.png)
![](https://marioalexsan.github.io/assets/atlyss/allowanynames/ingame.png)

# Save Data Storage

AllowAnyNames uses Multitool's custom profile data functionality to store the rich text name.

This means that the mod's data will be stored under `ATLYSS_Data/profileCollections/Marioalexsan_Multitool/atl_characterProfile_{index}_mods`.

If you need to uninstall the mod for some reason, the game should revert to a sanitized version of the rich text name that is stored as part of the vanilla data.

# Mod Compatibility

AllowAnyNames targets the following game versions and mods:

- ATLYSS 102025.a5
- Multitool v1.0.0 (required dependency)
- CodeYapper v2.0.0 (required dependency)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.