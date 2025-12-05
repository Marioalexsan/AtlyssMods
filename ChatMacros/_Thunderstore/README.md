# ChatMacros

Allows you to bind text to buttons that can be sent as chat messages or chat commands by pressing the button.

# Configuration options

These options are available under `BepInEx/config/Marioalexsan.ChatMacros.cfg` or under [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/)'s mod tab, if available.

There are 9 macro buttons that can be configured, numbered from 1 to 9 as indicated by the `N` letter. For example, Macro1Button = 1st button's keycode, Macro2 = 2nd button's text, etc.

Each Macro can be optionally combined with Alt and Ctrl if `EnableAltMacros` and `EnableCtrlMacros` is enabled, allowing for up to 27 commands total.

| Setting          | Description                                                        | Acceptable Values                           | Default Value |
|------------------|--------------------------------------------------------------------|---------------------------------------------|---------------|
| Enabled          | Enable or disable the key bindings for the chat macros             | `true` / `false`                            | `true`        |
| EnableAltMacros  | Enables usage of Alt + Macro combinations                          | `true` / `false`                            | `false`       |
| EnableCtrlMacros | Enables usage of Ctrl + Macro combinations                         | `true` / `false`                            | `false`       |
| MacroNButton     | The button to use for the Nth macro                                | any key code                                | Keypad N      |
| MacroN           | The text to send in chat when the Nth macro is triggered           | any text (up to 125 characters for vanilla) | <empty>       |
| MacroNAlt        | The text to send in chat when the Nth macro is triggered with Alt  | any text (up to 125 characters for vanilla) | <empty>       |
| MacroNCtrl       | The text to send in chat when the Nth macro is triggered with Ctrl | any text (up to 125 characters for vanilla) | <empty>       |

# Mod Compatibility

ChatMacros targets the following game versions and mods:

- ATLYSS 112025.a4
- Nessie's EasySettings v1.2.1 (optional dependency used for configuration)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.