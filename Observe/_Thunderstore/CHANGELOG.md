# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2025-Feb-18

### Added

- Added `/observe freeze` - this is the same as `/observe pose`, except your current head direction is used instead of current camera direction
- The behaviour of the camera when looking at frontal angles in `Default` look mode (without Owl mode enabled) is now configurable:
  - `FaceBehindRight` - tries to look behind over the right shoulder (current / default behaviour)
  - `FaceBehindLeft` - tries to look behind over the left shoulder
  - `FaceCamera` - tries to look at the camera, as if `Camera` look mode were used
  - `FaceFront` - tries to look forward, as if `/observe forward` were used
  - `NoEffect` - doesn't change the behaviour in any way when looking at frontal angles (may act weird during movement!)
- Added an EasySettings configuration option and `/observe frontal <mode>` command to configure the feature from above
- Added `/observe tilt <left | right | reset> [angle]` that allows you to tilt your head left / right by the given number of degrees
  - If not specified, the default angle is 18 degrees
  - If specified, angle must be between 0 and 35 degrees

### Changed

- The character will now use up / down eye states when looking above / below 60 degrees

## [1.2.1] - 2025-Feb-15

### Fixed

- Looking at NPCs / interactables during dialogue now correctly uses the player head's position instead of the body's position

## [1.2.0] - 2025-Feb-15

### Added

- `LookAtInteractables` sets whenever the character should look at interactables in Default mode (`true` by default)
- `LookAtNPCDuringDialogue` sets whenever the character should look at NPCs during dialogue in Default mode (`true` by default)
  - For NPCs with a head, the character will try to look at it; otherwise, it will look at the character's center 

### Changed

- Updated to EasySettings 1.3.0 and moved Observe settings to a dedicated tab

## [1.1.2] - 2026-Jan-20

### Fixed

- Observe will now avoid sending packets to players that happen to have invalid or empty Steam IDs set on the player object

## [1.1.1] - 2026-Jan-20

### Fixed

- Added AtlyssCommandLib to the Thunderstore dependency list

## [1.1.0] - 2026-Jan-20

### Added

- Added chat commands that change where and how your character looks, as well as various settings
    - `/observe default` resets back to default behaviour (i.e. look in line with the camera)
    - `/observe pose [seconds]` makes your character look in your current direction for that many seconds, if
      specified
      - the pose is relative to the body, i.e. moving around will keep the relative direction
    - `/observe <direction> [seconds]` poses in the given relative direction, similar to `/observe pose`
      - available values are `left`, `right`, `up`, `down`, `forward`, and `backward`
    - `/observe camera [seconds]` makes your character look *towards* your camera instead of in line with it for that
      many seconds, if specified
    - `/observe environment [seconds]` makes your character look at the environment in the given direction for that many
      seconds, if specified
      - moving around will make the head rotate to face the set direction
    - `/observe owl` toggles Owl Mode on / off
    - `/observe speed <setting>` sets the speed at which your character rotates their head
    - `/observe vanilla` toggles Vanilla Mode on / off (previously this was the Ignore Camera setting)
- Added settings to configure the rotation constraints for character heads:
    - the `OwlMode` setting allows your character to face any direction (toggled off by default)
    - the `AllowOwlModeForOthers` setting allows other characters that use `OwlMode` to display their head rotations
      unconstrained (toggled on by default)
        - if disabled, their rotations will be constrained even if they have it specified
    - while `OwlMode` is active, eye states are not used to display far angles that can't be "reached" with normal
      rotations
- Added the `LookSpeed` setting that defines how fast the character's head responds to direction changes
    - available options are `Sloth`, `VerySlow`, `Slow`, `Normal`, `Fast`, `VeryFast` and `Caffeinated`
    - each player uses their own look speed
- Added the `HoldHeadDirectionAfterStrafing` setting (on by default); this acts as if you were to use
  `/observe environment` while in strafe mode
    - The duration can be controlled with `HoldHeadDirectionAfterStrafingDuration` (4 second by default, 120 seconds max)

### Changed

- While in freecam mode or in a dialogue, the character will now temporarily avoid using the camera and instead look forward
  - Currently only Homebrewery's freecam mode is supported
- `IgnoreCamera` setting was renamed to `VanillaMode`
    - It will now generally disable this mod's functionality for your character, making them act as in vanilla

### Fixed

- Fixed head rotations being applied incorrectly to left handed characters
- Fixed a packet serialization issue that would override parts of player NetIDs and cause packets to be dropped for
  players if they don't join early enough, meaning you couldn't see other player's head rotations

## [1.0.1] - 2026-Jan-19

### Fixed

- Removed leftover info logs for packets sent / received

## [1.0.0] - 2026-Jan-17

**Initial mod release**
