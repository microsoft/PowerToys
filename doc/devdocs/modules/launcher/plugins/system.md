# Windows System Commands Plugin

As the name suggests, the Windows System Commands Plugin is used to directly run Windows system commands that have been entered by the user as a query. This is done by parsing the entry and validating the command, followed by executing it.

The user can change the behavior of the plugin (language, confirmation dialog, ...) with optional plugin settings.

![Image of System Commands plugin](/doc/images/launcher/plugins/sys.gif)

Available commands:
* Shutdown
* Restart
* Sign Out
* Lock
* Sleep
* Hibernate
* Empty Recycle Bin
* UEFI Firmware Settings (Only available on systems, that boot in UEFI mode.)

## Optional plugin settings

* We have the following settings that the user can configure to change the behavior of the plugin:

	| Key | Default value | Name/Description |
	|--------------|-----------|------------|
	| `ConfirmSystemCommands` | `false` | Show a dialog to confirm system commands |
	| `LocalizeSystemCommands` | `true` | Use localized system commands instead of English ones |

* The optional plugin settings are implemented via the [`ISettingProvider`](/src/modules/launcher/Wox.Plugin/ISettingProvider.cs) interface from `Wox.Plugin` project. All available settings for the plugin are defined in the [`Main`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.System/Main.cs) class of the plugin.

## Technical details

### [`Main`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.System/Main.cs)

* Tries to parse the user input and returns a specific Windows system command by using a [`Result`](/src/modules/launcher/Wox.Plugin/Result.cs) list.

* While parsing, the plugin uses [`FuzzyMatch`](/src/modules/launcher/Wox.Infrastructure/StringMatcher.cs) to get characters matching a result in the list.

### UEFI command

* The UEFI command is only available on systems, that boot in UEFI mode.

* This is validated by checking the result of the method [`GetSystemFirmwareType`](/src/modules/launcher/Wox.Plugin/Common/Win32/Win32Helpers.cs), which uses the native method [`GetFirmwareType`](/src/modules/launcher/Wox.Plugin/Common/Win32/NativeMethods.cs) in `kernel32.dll`.

### Score

* [`CalculateSearchScore`](/src/modules/launcher/Wox.Infrastructure/StringMatcher.cs) A match found near the beginning of a string is scored more than a match found near the end. A match is scored more if the characters in the patterns are closer to each other, while the score is lower if they are more spread out.
