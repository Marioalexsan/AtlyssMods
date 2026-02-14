# Observe

A mod that by default makes your character look in the direction your camera is looking.
You can pose them to have them look in specific directions using commands or controls.

If other players have the mod installed, you'll also be able to see where they're looking!

# Configuration options

These options are available under `BepInEx/config/Marioalexsan.Observe.cfg` or
under [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/)'s mod tab, if available.

| Setting                                | Description                                                                                                 | Acceptable Values                                                        | Default Value |
|----------------------------------------|-------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------|---------------|
| Enabled                                | Enable or disable mod functionality completely.                                                             | `true` / `false`                                                         | `true`        |
| VanillaMode                            | While active, makes your player act as a vanilla character as far as head rotations go.                     | `true` / `false`                                                         | `false`       |
| EnableNetworking                       | Enable sending/receiving camera directions to/from people with the mod installed.                           | `true` / `false`                                                         | `true`        |
| OwlMode                                | Enables full range of rotation for your player's head. This might look pretty weird in some cases!          | `true` / `false`                                                         | `false`       |
| AllowOwlModeForOthers                  | Allow other players that use OwlMode to display their unconstrained head rotations.                         | `true` / `false`                                                         | `true`        |
| LookSpeed                              | The speed at which your character reacts to changes in direction.                                           | `Sloth`, `VerySlow`, `Slow`, `Normal`, `Fast`, `VeryFast`, `Caffeinated` | `Normal`      |
| HoldHeadDirectionAfterStrafing         | Enable to keep looking at the given direction after strafing as if you used "/observe environment"          | `true` / `false`                                                         | `true`        |
| HoldHeadDirectionAfterStrafingDuration | How long to continue looking at the environment when HoldHeadDirectionAfterStrafing is enabled, in seconds. | min: `0`. max: `120` (seconds)                                           | `4` (seconds) |
| LookAtInteractables                    | Have your character look at interactable objects if not already posing.                                     | `true` / `false`                                                         | `true`        |
| LookAtNPCDuringDialogue                | Have your character look at NPCs during dialogue if not already posing.                                     | `true` / `false`                                                         | `true`        |

# Look modes

There are four main look modes used by the mod:
- `Default` makes the character look in line with the camera
- `Pose` makes the character look in the given direction relative to the character's body. If the character moves around, the head will maintain the direction relative to the body.
- `Environment` makes the character look in the given direction as if they were looking at the environment. If the character moves around, the head will turn to continue facing the direction.
- `Camera` makes the character look in the direction of the camera

# Behaviour explanations

- In `Default` and `Camera` mode, your character will temporarily look forward instead of using the camera's direction if you happen to use freecam or are in a dialogue
  - Currently, only Homebrewery's freecam mode is supported
- In `Pose` and `Environment` mode, the direction to use is set at the time of sending the command based on the camera direction
  - For `/observe <direction> [seconds]`, the direction to use is instead the one you specify in the commmand
- By default, your character's rotation range is restricted. Owl Mode allows full rotations, but this might make it look like the character snapped their neck.
  - You can control whenever you can see other people's Owl Mode setting with the `AllowOwlModeForOthers` configuration option.
- When strafing, if `HoldHeadDirectionAfterStrafing` is set, then the character will act as if you used `/observe environment [duration]` with HoldHeadDirectionAfterStrafingDuration as the duration
- While in `Default` mode, if you set `LookAtInteractables` and `LookAtNPCDuringDialogue`, then your character will look at interactables / NPCs if applicable instead of looking in line with the camera

# Commands

| Command                          | Action                                                                                                                                                                                               |
|----------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `/observe default`               | Makes your character return to the `Default` look mode. Also usable as `/observe reset`.                                                                                                             |
| `/observe pose [seconds]`        | Makes your character use the `Pose` look mode. If specified, it lasts for the given duration in seconds. The pose direction to set is the camera direction at the time of calling the command.       |
| `/observe <direction> [seconds]` | Same as `/observe pose`, except a specific direction is used. You can use one of `left`, `right`, `up`, `down`, `forward` and `backward`.                                                            |
| `/observe camera [seconds]`      | Makes your character use the `Camera` look mode. If specified, it lasts for the given duration in seconds.                                                                                           |
| `/observe environment [seconds]` | Makes your character use the `Environment` look mode. If specified, it lasts for the given duration in seconds. The direction to look at is the camera direction at the time of calling the command. |
| `/observe owl`                   | Toggles Owl Mode on/off.                                                                                                                                                                             |
| `/observe vanilla`               | Toggles Vanilla Mode on/off.                                                                                                                                                                         |
| `/observe speed <setting>`       | Changes the look speed for your character. You can use one of `Sloth`, `VerySlow`, `Slow`, `Normal`, `Fast`, `VeryFast` and `Caffeinated`.                                                           |

# Mod Compatibility

Observe targets the following game versions and mods:

- v1.1.1 - v1.2.0
  - ATLYSS 12026.a3
  - Soggy's CodeYapper v2.1.0
  - Soggy's AtlyssCommandLib v0.0.7
  - Nessie's EasySettings v1.2.1 (optional, used for configuration)
  - Catman's Homebrewery v4.7.3 (optional, interacts with its freecam setting)
- v1.0.0 - v1.1.0
  - ATLYSS 12026.a3
  - Soggy's CodeYapper v2.1.0
  - Nessie's EasySettings v1.2.1 (optional, used for configuration)
  - Catman's Homebrewery v4.7.3 (optional, interacts with its freecam setting)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.