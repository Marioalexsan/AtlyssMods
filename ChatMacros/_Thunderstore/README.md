# ChatMacros

Allows you to bind text to buttons that can be sent as chat messages or chat commands by pressing the button.

# Configuration options

These options are available under `BepInEx/config/Marioalexsan.ChatMacros.cfg` or under [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/)'s mod tab, if available.

There are 9 macro buttons that can be configured, numbered from 1 to 9 as indicated by the `N` letter. For example, Macro1Button = 1st button's keycode, Macro2 = 2nd button's text, etc.

Each Macro can be optionally combined with Alt and Ctrl if `EnableAltMacros` and `EnableCtrlMacros` is enabled, allowing for up to 27 commands total.

Multiple commands can be sent together by separating them with "&&". Each command in a sequence is queued and sent at 100ms intervals.
For example, `/dance && CONGA TIME LADS` would make your character both dance and say "CONGA TIME LADS".

If you need to send consecutive "&&" as part of the actual text without having it be interpreted as separate commands, you can do so by using "&amp;" as an escape sequence instead.
For example, `/sit && Eepy &amp;&amp; silly` would make your character sit and say "Eepy && silly".

| Setting          | Description                                                                        | Acceptable Values                           | Default Value |
|------------------|------------------------------------------------------------------------------------|---------------------------------------------|---------------|
| Enabled          | Enable or disable the key bindings for the chat macros                             | `true` / `false`                            | `true`        |
| EnableAltMacros  | Enables usage of Alt + Macro combinations                                          | `true` / `false`                            | `false`       |
| EnableCtrlMacros | Enables usage of Ctrl + Macro combinations                                         | `true` / `false`                            | `false`       |
| MacroNButton     | The button to use for the Nth macro                                                | any key code                                | Keypad N      |
| MacroN           | The text to send in chat when the Nth macro is triggered                           | any text (up to 125 characters for vanilla) | <empty>       |
| MacroNAlt        | The text to send in chat when the Nth macro is triggered with Alt                  | any text (up to 125 characters for vanilla) | <empty>       |
| MacroNCtrl       | The text to send in chat when the Nth macro is triggered with Ctrl                 | any text (up to 125 characters for vanilla) | <empty>       |

# Mod Compatibility

ChatMacros targets the following game versions and mods:

- v1.0.0 through v1.2.1
  - ATLYSS 112025.a4
  - [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/) v1.2.1 (optional, used for configuration)
- v1.2.2
  - ATLYSS 12026.a3
  - [EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/) v1.3.0 (optional, used for configuration)

Compatibility with other game versions and mods is not guaranteed, especially for updates with major changes.