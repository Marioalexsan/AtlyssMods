# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.2] - 2025-Nov-24

### Fixed

- Issues stemming from the 112025.a1 update
  - Note: controller support is not currently included, it will have to be done in a later update 

## [2.0.1] - 2025-Nov-15

### Changed
- Name updates sent / received are no longer logged
- Rich text names are now sanitized so that broken or abusable tags are stripped
  - `<size=N>` is now limited between a size of 8 and 30 - other values are considered invalid and will be stripped
  - `<color=#RRGGBBAA>` formats must now have an alpha value of at least `0x20` (i.e. 12.5% opacity), otherwise it will be stripped
  - some TextMeshPro specific tags that are abusable are now stripped

## [2.0.0] - 2025-Oct-31

### Added
- Now relies on CodeYapper for networking functionality
- Now relies on Multitool for custom profile data functionality
- A new "Rename character" UI option has been added to the character select screen, allowing you to rename characters
  - You can use this to set rich text for your existing characters instead of having to edit the save file manually

### Changed
- The character creator's name input is now modified to have no restrictions, allowing you to specify a rich text name on creation

### Fixed
- AllowAnyNames should now be a bit more compatible with non-modded players
  - Players without the mod will now see a vanilla-friendly fallback name instead of "Null"
  - Previously, custom names used to be reverted to "Null" when joining servers without the mod, and this change would be persisted to disk; this should now no longer be the case
 
## [1.0.1] - 2025-Apr-13

### Fixed

- Updated mod to work with ATLYSS 1.6.2b

## [1.0.0] - 2024-Nov-28

### Changed

**Initial mod release**