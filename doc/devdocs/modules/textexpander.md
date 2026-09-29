# Text Expander

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Text%20Expander%22)<br>
[Feature request #5074](https://github.com/microsoft/PowerToys/issues/5074)

## Overview

Text Expander replaces short triggers with longer snippets as you type, in any app. For example, typing `#sig` can expand into a multi-line signature. Snippets can contain variables such as the current date or time, a prompt for a value, and a cursor position marker.

## Architecture

```
textexpander/
├── TextExpanderModuleInterface/  # Native runner module DLL
├── TextExpander/                 # Expansion engine (PowerToys.TextExpander.exe)
└── TextExpander.UnitTests/       # Engine unit tests
```

The Settings page lives in `src/settings-ui` (`TextExpanderPage.xaml`, `TextExpanderViewModel.cs`, and `TextExpanderSettings.cs` / `TextExpanderProperties.cs` / `TextExpanderSnippetFile.cs` in `Settings.UI.Library`).

### Module Interface (TextExpanderModuleInterface)

`dllmain.cpp` implements `PowertoyModuleIface`. It:
- Launches `WinUI3Apps\PowerToys.TextExpander.exe` when the module is enabled, resolving the path relative to its own DLL.
- Passes the runner PID, `--managed --instance:powertoys`, and `--settings:"<path to settings.json>"`.
- Stops the engine by signaling `CommonSharedConstants::TEXT_EXPANDER_EXIT_EVENT`, then terminates it if it has not exited within 1.5 seconds.
- Honors the `ConfigureEnabledUtilityTextExpander` group policy.

### Engine (TextExpander)

A self-contained .NET process with no UI framework dependency. It:
- Installs a low-level keyboard hook and tracks the characters typed into the focused app.
- Matches triggers against the snippet file (`snippets.txt`), optionally waiting for a word boundary (space or punctuation) before expanding.
- Replaces the trigger by pasting (the user's clipboard is saved and restored) or by typing with `SendInput`, depending on the Insertion method setting and snippet length.
- Expands variables (`{{date}}`, `{{time}}`, `{{datetime}}`, `{{isodate}}`, `{{date:<format>}}`, `{{prompt:<label>}}`) and places the cursor at `$|$`.
- Supports undo: pressing Ctrl+Z immediately after an expansion restores the typed trigger.
- Ignores its own injected input and, optionally, input injected by Keyboard Manager remaps.
- Watches `settings.json` and the snippet file and reloads them without a restart.
- Exits when the runner process exits or the exit event is signaled.

### Settings and snippet storage

| File | Location |
| --- | --- |
| Module settings | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\settings.json` |
| Snippets (default) | `%LOCALAPPDATA%\Microsoft\PowerToys\TextExpander\snippets.txt` |

The snippet folder can be changed in Settings. The Settings page and the engine resolve the snippet file with the same rules, so both always read the same file.

Snippet file format:

```
// Comments start with //
#sig=Best regards,\nYour Name
#addr<<<
One Microsoft Way
Redmond, WA 98052
>>>
```

## Debugging

1. Build `PowerToys.slnx` and start the runner.
2. Enable Text Expander in Settings.
3. Attach the debugger to `PowerToys.TextExpander.exe`.

The engine can also be started directly for debugging: `PowerToys.TextExpander.exe --managed --settings:"<path to settings.json>"`.
