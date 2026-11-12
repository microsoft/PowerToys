# Shortcut Guide

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/shortcut-guide)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Shortcut%20Guide%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Shortcut%20Guide%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Shortcut+Guide%22+)

## Overview
Shortcut Guide is a PowerToy that displays an overlay of available keyboard shortcuts when a user-set keyboard shortcut is pressed. It helps users discover and remember keyboard shortcuts for Windows and apps.

> [!NOTE]
> The spec for the manifest files is in development and will be linked here once available.

## Usage
- Press the user-defined hotkey to display the full overlay.
- Optionally, hold either Windows key to show taskbar indicators or the full overlay after a configurable delay.
- Press the hotkey again or press ESC to dismiss the overlay. A full overlay opened by holding the Windows key can either close on key release or remain open, depending on the setting.

The **Hold Windows key** setting is independent of the activation shortcut:

- **Off** leaves the Windows key behavior unchanged.
- **Show taskbar indicators** is the default and always hides the indicators when the Windows key is released.
- **Open Shortcut Guide** can close on Windows-key release or remain open.

- Use the title-bar search box to filter shortcuts on the selected application page
- Press Ctrl+F to focus search. Escape clears an active search before dismissing the overlay
- 
The hold duration accepts values from 100 through 5,000 milliseconds and defaults to 900 milliseconds.

## Build and Debug Instructions

### Build
1. Open PowerToys.slnx in Visual Studio
2. Select Release or Debug in the Solutions Configuration drop-down menu
3. From the Build menu, choose Build Solution
4. The executable is named PowerToys.ShortcutGuide.exe

### Debug
1. Right-click the ShortcutGuide.Ui project and select 'Set as Startup Project'
2. Right-click the project again and select 'Debug'

> [!NOTE]
> When run in debug mode, the window behaves differently than in release mode. It will not automatically close when loosing focus, it will be displayed on top of all other windows, and it is not hidden from the taskbar. 

### Automated UI tests

`src\modules\ShortcutGuide\Tests\ShortcutGuide.UITests` uses the winappcli-based
`UITestAutomation.Next` framework. Build it with:

```powershell
tools\build\build.cmd -Path src\modules\ShortcutGuide\Tests\ShortcutGuide.UITests -Platform x64 -Configuration Release
```

The suite covers Settings navigation and shortcut readout, the enable/disable process lifecycle,
and shortcut activation, Escape dismissal, disabled-shortcut suppression, and reactivation.
Shortcut Guide keeps its listener process alive while the overlay is hidden: overlay assertions
must check visible windows, not process presence. Activation also waits for the search control
before sending input, because a visible host window can precede initialized content.

#### Prepare and run locally

Use a disposable Windows test VM with an unlocked English-language, **en-US keyboard-layout**
standard-user desktop, .NET 10 Desktop Runtime, and the repository-pinned winappcli build.
The suite checks the keyboard-layout prerequisite explicitly because the configured shortcut's
displayed OEM-key label depends on that layout. The default shortcut is **Win + Shift + /**.

Building only the test project does not produce the runtime it launches. First build a complete
PowerToys Release runtime for the same architecture, including ShortcutGuide.Ui,
ShortcutGuideModuleInterface, Runner, and Settings.

Release Settings IPC requires trusted signatures on `PowerToys.exe` and
`WinUI3Apps\PowerToys.Settings.exe`; otherwise the Runner rejects Settings with
`not-microsoft-signed`. Use the existing
[prepared-runtime signing instructions](cropandlock.md#prepare-signing-on-the-execution-machine)
with `.pipelines\signSparsePackages.ps1 -RequiredAuthenticodeFile PowerToys.exe,PowerToys.Settings.exe`.
Shortcut Guide does not require the Crop and Lock fixture package: omit that recipe's
`RequiredPackage` argument and use `-Include PowerToys.exe,PowerToys.Settings.exe`.
Use a marker dedicated to this run, and follow
[temporary-trust cleanup](cropandlock.md#remove-temporary-trust) afterward. Repeat signing
whenever rebuilding replaces either executable.

For the preferred host-build/guest-execution workflow, use the
[host-signing and guest-trust guidance](cropandlock.md#build-on-the-host-and-execute-in-a-local-vm):
sign a staged copy with `-SkipLocalTrust` and export only the public certificate for the guest.
Do not install this test trust on the build host. Remove only this run's guest trust and
disposable signing key afterward.

Inside the prepared guest, run without elevation (these paths assume the matching runtime
and test payloads were staged as described in the local VM guide):

```powershell
$env:POWERTOYS_INSTALL_DIR = 'C:\PowerToysUiTestRun\PowerToys'
$tests = 'C:\PowerToysUiTestRun\Tests'
& (Join-Path $tests 'ShortcutGuide.UITests.exe') `
    --report-trx --report-trx-filename ShortcutGuide.trx `
    --results-directory (Join-Path $env:TEMP 'ShortcutGuideTestResults')
```

Expect **3 executed, 3 passed, zero skipped**. Do not replace failed Settings lifecycle assertions
with settings-file edits or a Runner restart.

Use the [local VM guide](../../../.github/skills/ui-tests-local-vm/references/agentic-loop.md)
for payload staging and the Windows 10/11 Default and Constrained matrix. Set
`-TestExecutable ShortcutGuide.UITests.exe` and the matching `-Platform x64Win10`,
`x64Win11`, or `ARM64`. The `ui-tests-pipeline-ci` skill covers scoped CI
(`[ShortcutGuide.UITests]`) and is **Microsoft FTE-only**; external contributors complete local
validation and request maintainer CI. The suite participates in the shared Runner/Settings
companion-signing setup so enable/disable assertions exercise the real Release Settings IPC path.

## Project Structure

The Shortcut Guide module consists of the following 4 projects:

### [`ShortcutGuide.Ui`](/src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuide.Ui.csproj)

This is the main UI project for the Shortcut Guide module. Upon startup it does the following tasks:

1. Copies the built-in manifest files to the users manifest directory (overwriting existing files).
2. Generate the `index.yml` manifest file.
3. Populate the PowerToys shortcut manifest with the user-defined shortcuts.
4. Starts the UI.

### Related files in PowerToys.Interop

#### [`excluded_app.cpp`](/src/modules/ShortcutGuide/ShortcutGuide.CPPProject/excluded_app.cpp)

This file contains one function with the following signature:

```cpp
__declspec(dllexport) bool IsCurrentWindowExcludedFromShortcutGuide()
```

This function checks if the current window is excluded from the Shortcut Guide overlay. It returns `true` if the current window is excluded otherwise it returns `false`.

#### [`tasklist_positions.cpp`](/src/modules/ShortcutGuide/ShortcutGuide.CPPProject/tasklist_positions.cpp)

This file contains helper functions to retrieve the positions of the taskbar buttons. It exports the following function:

```cpp
__declspec(dllexport) TasklistButton* get_buttons(HMONITOR monitor, int* size)
```

This function retrieves the positions of the taskbar buttons for a given monitor. It returns an array of `TasklistButton` structures (max 10), which contain the position and size of each button.

`monitor` must be the monitor handle of the monitor containing the taskbar instance of which the buttons should be retrieved.

`size` will contain the resulting array size.

It determines the positions through Windows `FindWindowEx` function.
For the primary taskbar it searches for:
* A window called "Shell_TrayWnd"
* that contains a window called "ReBarWindow32"
* that contains a window called "MSTaskSwWClass"
* that contains a window called "MSTaskListWClass"

For any secondary taskbar it searches for:
* A window called "Shell_SecondaryTrayWnd"
* that contains a window called "WorkerW"
* that contains a window called "MSTaskListWClass"

It then enumerates all the button elements inside "MSTaskListWClass" while skipping such with a same name (which implies the user does not use combining taskbar buttons) 

If this method fails, which it will for newer versions of Windows, it falls back to searching for:
* A window called "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
* that contains a window called "Windows.UI.Composition.DesktopWindowContentBridge"
* that contains a window called "Windows.UI.Input.InputSite.WindowClass"
* the first child element

It then enumerates all the button elements inside the selected while skipping such with a same name (which implies the user does not use combining taskbar buttons) and such that do not start with "Appid:" (which are not actual taskbar buttons related to apps, but others like the widgets or the search button).

### [`ShortcutGuide.IndexYmlGenerator`](/src/modules/ShortcutGuide/ShortcutGuide.IndexYmlGenerator/)

This application generates the `index.yml` manifest file.

It is a separate project so that its code can be easier ported to WinGet in the future.

### [`ShortcutGuideModuleInterface`](/src/modules/ShortcutGuide/ShortcutGuideModuleInterface/ShortcutGuideModuleInterface.vcxproj)

The module interface that handles opening and closing the user interface.

## Features and Limitations

- Currently the displayed shortcuts (Except the ones from PowerToys) are not localized.
- It's currently rated as a P3 (lower priority) module

## Future Development

- Implementing with WinGet to get new shortcut manifest files
- Adding localization support for the built-in manifest files