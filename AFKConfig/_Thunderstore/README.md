# AFKConfig

Adds extra configuration options to the AFK mechanic.

This mod is client-side and mostly compatible with vanilla multiplayer.

# Special mentions & thanks

- Xesy for providing the icon for this mod

# Configuration options

These options are available under `BepInEx/config/Marioalexsan.AFKConfig.cfg` or under [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/)'s mod tab, if available.

The default values correspond to the vanilla game's behaviour.

| Setting         | Description                                                                               | Acceptable Values     | Default Value |
|-----------------|-------------------------------------------------------------------------------------------|-----------------------|---------------|
| AFKEnabled      | Enable or disable the AFK mechanic entirely                                               | `true` / `false`      | `true`        |
| AFKTimer        | How many minutes should pass before AFK mode is activated                                 | min: `0.5`, max: `60` | `2` (minutes) |
| AllowTabbingOut | If enabled, the game will not react to `Alt` + `Tab` or to the `Windows` / `Meta` buttons | `true` / `false`      | `false`       |
| SitDownOnAFK    | If enabled, the character will not sit down on AFK                                        | `true` / `false`      | `true`        |
| StandUpFromAFK  | If enabled, the character will not stand up from AFK                                      | `true` / `false`      | `true`        |

# Mod Compatibility

AFKConfig targets the following game versions and mods:

- ATLYSS 112025.a3
- Nessie's EasySettings v1.2.1 (optional dependency used for configuration)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.