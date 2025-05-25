# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2024-May-22

### Added

- AutoSaver will now also save latest save files for the current game version
  - These save files will not get touched when updating to a newer version, allowing you to restore / return to a previous version's save file if needed 

## [1.2.0] - 2024-May-05

### Added

- Added support for EasySettings version 1.1.6
- Added EnableAutosaving config option that enables or disables the periodic autosave component (default: true)
- Added AppendSlotToSaveName config option that enabled or disables appending the slot index to the backup names (default: true)
  - It's recommended to keep this set to true; setting this to false can be useful when moving saves between save slots, but will cause characters with duplicate names to overwrite each other

### Changed

- Migrated the backup save folder from `ATLYSS/ATLYSS_Data/_profileCollections/Marioalexsan_AutoSaver` to `ATLYSS/Marioalexsan_AutoSaver`
  - Saves from AutoSaver version 1.1.1 and below will be migrated automatically on first launch
- Updated compatibility for public test branch of ATLYSS (currently v2.0.5d)
  - Note that this is experimental and subject to change until a stable version is released

## [1.1.1] - 2024-Dec-17

### Changed

- Updated for ATLYSS v1.6.0b

## [1.1.0] - 2024-Dec-09

### Added

- Support for saving extra Spike slots from [MoreBankTabs](https://thunderstore.io/c/atlyss/p/16MB/MoreBankTabs/) v0.1.0
- Option to toggle autosaving whenever map instance changes.
- Experimental feature that allows locally saving other player's saves in multiplayer.

### Changed

- Autosaves no longer happen whenever map instance changes, you must now toggle an option for this to happen.
- Save backups now reference the slot that has been used. This allows multiple saves with the same character name to be backed up correctly.

## [1.0.0] - 2024-Dec-06

### Changed

**Initial mod release**