# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.2] - 2026-Feb-14

### Changed

- Updated to EasySettings 1.3.0 and moved ChatMacros settings to a dedicated tab

## [1.2.1] - 2025-Dec-18

### Fixed

- Using macros while not in-game would throw errors in the console

## [1.2.0] - 2025-Dec-10

### Added

- Multiple commands can now be sequenced as part of a single macro by separating them with `&&`
  - For example, `/dance && CONGA TIME LADS` would make your character both dance and say "CONGA TIME LADS"
  - Successive commands are sent at 100ms intervals
  - Literal `&` characters can be escaped using `&amp;`, so you can send `&&` using `&amp;&amp;` without having it be interpreted as separate commands

## Fixed

- Macros no longer trigger while any event system text inputs are selected, such as the Host Console's command input

## [1.1.0] - 2025-Dec-05

### Added

- Alt and Ctrl Macro combinations can now be used, raising the total number from 9 to 27
- Added `MacroNAlt` options to configure Alt behaviour for macros
- Added `MacroNCtrl` options to configure Ctrl behaviour for macros
- Added `EnableAltMacros` and `EnableCtrlMacros` options to control whenever Alt and Ctrl macros are active

## [1.0.1] - 2025-Dec-04

### Fixed

- Macros no longer trigger while the chat window or the in-game settings menu is open

## [1.0.0] - 2025-Dec-03

### Changed

**Initial mod release**