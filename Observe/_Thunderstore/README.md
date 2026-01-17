# Observe

A simple mod that makes your character look in the direction your camera is looking.

If other players have the mod installed, you'll also be able to see where they're looking!

# Configuration options

These options are available under `BepInEx/config/Marioalexsan.Observe.cfg` or
under [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/)'s mod tab, if available.

| Setting          | Description                                                                        | Acceptable Values | Default Value |
|------------------|------------------------------------------------------------------------------------|-------------------|---------------|
| Enabled          | Enable or disable mod functionality completely                                     | `true` / `false`  | `true`        |
| IgnoreCamera     | While active, makes your player look forward instead of using the camera direction | `true` / `false`  | `false`       |
| EnableNetworking | Enable sending/receiving camera directions to/from people with the mod installed   | `true` / `false`  | `true`        |

# Mod Compatibility

Observe targets the following game versions and mods:

- ATLYSS 12026.a3
- Nessie's EasySettings v1.2.1 (optional dependency used for configuration)
- Soggy's CodeYapper v2.1.0

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.