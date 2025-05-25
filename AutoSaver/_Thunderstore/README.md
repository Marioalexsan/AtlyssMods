# AutoSaver

Automatically backups your character save and Spike's inventory on game load, quit and every 4 minutes.

Up to 15 saves are stored. You can configure both the number of saves stored and the automatic save interval in `BepInEx/config/Marioalexsan.AutoSaver.cfg`.

You can also configure the mod to trigger autosaving whenever the map instance changes.

If you have [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/), then Autosaver will provide UI options for configuring some of the settings in its mod menu.

# Restoring saves from backups

To fix save corruptions:
- go to `ATLYSS/Marioalexsan_AutoSaver/Characters/{CHARACTER_NAME}_slot{SLOT_INDEX}`, where CHARACTER_NAME is your character's name in ATLYSS, and SLOT_INDEX is the character slot (from 0 to 5)
  - alternatively, if you have disabled appending slot indexes to save names, then the save will be named `ATLYSS/Marioalexsan_AutoSaver/Characters/{CHARACTER_NAME}`
- grab the latest backup, or any previous backup based on the time it was created on
- copy the file to `ATLYSS_Data/profileCollections` and replace `atl_characterProfile_{X}` with that file, where X is the slot you want it to go in, from 0 to 5 (`atl_characterProfile_0` is the first slot)
- example: `ATLYSS/Marioalexsan_AutoSaver/Characters/Chip_slot0/_latest` -> `ATLYSS_Data/profileCollections/atl_characterProfile_0` (overwriting previous corrupt file if needed)

To fix item bank corruption:
- go to `ATLYSS/Marioalexsan_AutoSaver/ItemBank/`
- grab the latest backup, or any previous backup based on the time it was created on
- copy the files from within to `ATLYSS_Data/profileCollections`
- replace `atl_itemBank` with `itemBank_0`, `atl_itemBank_01` with `itemBank_1`, `atl_itemBank_02` with `itemBank_2`, etc.
- example: 
  - `ATLYSS/Marioalexsan_AutoSaver/ItemBank/_latest/itemBank_0` -> `ATLYSS_Data/profileCollections/atl_itemBank` (overwriting previous corrupt file if needed)
  - `ATLYSS/Marioalexsan_AutoSaver/ItemBank/_latest/itemBank_1` -> `ATLYSS_Data/profileCollections/atl_itemBank_01` (overwriting previous corrupt file if needed)
  - `ATLYSS/Marioalexsan_AutoSaver/ItemBank/_latest/itemBank_2` -> `ATLYSS_Data/profileCollections/atl_itemBank_02` (overwriting previous corrupt file if needed)

In addition to the `_latest` save files that are saved on any game version, the mod will also save `_latest_version` save files for each game version you play on, allowing you to restore to an older version if needed.

# Mod Compatibility

This mod version targets ATLYSS v1.6.2b. It also has experimental support for the public test branch (version 2.0.5d).

The mod has compatibility with the following mods:

- "More Bank Tabs" v0.1.1 - if this mod is detected, AutoSaver will try to backup the three extra slots created by it.
  - Extra slots will follow the format `MoreBankTabs_itemBank_{X}`, where X is the extra slot.
- Nessie's EasySettings v1.1.6 (optional dependency used for configuration)

 Compatibility with other game versions is not guaranteed, especially for updates with major changes.