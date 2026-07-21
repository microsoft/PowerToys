---
description: Reference for the PowerToys Keyboard Manager profile DSC resource
ms.date:     07/20/2026
ms.topic:    reference
title:       Keyboard Manager Profile Resource
---

# Keyboard Manager Profile Resource

## Synopsis

Manages the Keyboard Manager remapping profile — key remappings and shortcut
remappings — declaratively through DSC v3.

## Description

The `profile` resource deploys Keyboard Manager key and shortcut remappings.
While the [`settings` resource][01] manages a module's settings (for
KeyboardManager: the enabled state and editor options), the `profile`
resource manages the remapping profile itself, which Keyboard Manager stores
separately from its settings.

Keys are written with friendly names instead of virtual-key codes, so
configurations are easy to author and review:

```yaml
profile:
  keys:
    - { from: CapsLock, to: Esc }
  shortcuts:
    - { from: "Ctrl+Shift+A", to: "Ctrl+V" }
```

The only supported module is `KeyboardManager`. The DSC resource type is
`Microsoft.PowerToys/KeyboardManagerProfile`.

> **Important — replace semantics:** Applying the resource replaces the
> **whole** remapping profile with the declared state. Remappings created in
> the Keyboard Manager editor that are not part of the configuration are
> removed. Use `export` to capture the current remappings before switching a
> machine to declarative management.

When the profile is applied while PowerToys is running, the Keyboard Manager
engine reloads it immediately; otherwise the profile takes effect the next
time PowerToys starts. Note that the remappings are only active when the
Keyboard Manager utility is enabled (see the [KeyboardManager module][02]).

## Profile schema

The `profile` object has two entry lists:

### `keys[]` — single-key remappings

| Property | Type   | Required | Description                                            |
| -------- | ------ | -------- | ------------------------------------------------------ |
| `from`   | string | yes      | The key being remapped, e.g. `CapsLock`.               |
| `to`     | string | one of   | Target key or shortcut, e.g. `Esc`, `Ctrl+C`, `Disable`. |
| `toText` | string | one of   | Text to type instead of the key.                       |

Exactly one of `to` or `toText` must be set.

### `shortcuts[]` — shortcut remappings

| Property     | Type    | Required | Description                                                            |
| ------------ | ------- | -------- | ---------------------------------------------------------------------- |
| `from`       | string  | yes      | The shortcut being remapped, e.g. `Ctrl+Shift+A` or `Win+O, K` (chord). |
| `to`         | string  | one of   | Target key or shortcut.                                                |
| `toText`     | string  | one of   | Text to type instead of the shortcut.                                  |
| `runProgram` | object  | one of   | Program to start (see below).                                          |
| `openUri`    | string  | one of   | URI to open, e.g. `https://github.com` or `ms-settings:`.              |
| `targetApp`  | string  | no       | Process name the remap applies to, e.g. `notepad.exe`. Omit for global. |
| `exactMatch` | boolean | no       | Only trigger when no other keys are pressed. Default `false`.          |

Exactly one of `to`, `toText`, `runProgram`, or `openUri` must be set. A
shortcut `from` requires at least one modifier plus one action key, and may
add a chord second key after a comma (`"Win+O, K"`).

### `runProgram` object

| Property      | Type   | Required | Description                                                                                 |
| ------------- | ------ | -------- | ------------------------------------------------------------------------------------------- |
| `filePath`    | string | yes      | Program path; environment variables are expanded.                                           |
| `args`        | string | no       | Command-line arguments.                                                                     |
| `startInDir`  | string | no       | Working directory.                                                                          |
| `elevation`   | string | no       | `normal` (default), `elevated`, or `differentUser`.                                         |
| `ifRunning`   | string | no       | `showWindow` (default), `startAnother`, `doNothing`, `close`, `endTask`, `closeAndEndTask`. |
| `windowStyle` | string | no       | `normal` (default), `hidden`, `minimized`, `maximized`.                                     |

## Key names

Key names are case-insensitive and independent of the active keyboard
layout. Shortcut parts are joined with `+`; a chord second key follows after
a comma.

| Category      | Names                                                                                                                          |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Letters/digits | `A`–`Z`, `0`–`9`                                                                                                              |
| Function      | `F1`–`F24`                                                                                                                     |
| Modifiers     | `Ctrl`, `Alt`, `Shift`, `Win` (either side); `LCtrl`, `RCtrl`, `LAlt`, `RAlt`, `LShift`, `RShift`, `LWin`, `RWin`              |
| Navigation    | `Esc`, `Tab`, `CapsLock`, `Space`, `Enter`, `Backspace`, `Insert`, `Delete`, `Home`, `End`, `PgUp`, `PgDn`, `Up`, `Down`, `Left`, `Right`, `PrintScreen`, `ScrollLock`, `Pause`, `NumLock`, `Apps`, `Break`, `Clear`, `Sleep` |
| Numpad        | `NumPad0`–`NumPad9`, `NumPadMultiply`, `NumPadAdd`, `NumPadSeparator`, `NumPadSubtract`, `NumPadDecimal`, `NumPadDivide`; numpad-origin variants: `NumPadEnter`, `NumPadHome`, `NumPadEnd`, `NumPadPgUp`, `NumPadPgDn`, `NumPadInsert`, `NumPadDelete`, `NumPadUp`, `NumPadDown`, `NumPadLeft`, `NumPadRight`, `NumPadSlash` |
| Punctuation   | `Semicolon` (`;`), `Equals` (`=`), `Comma` (`,`), `Minus` (`-`), `Period` (`.`), `Slash` (`/`), `Backquote` (`` ` ``), `LBracket` (`[`), `Backslash` (`\`), `RBracket` (`]`), `Quote` (`'`), `OEM102` |
| Media/browser | `VolumeMute`, `VolumeDown`, `VolumeUp`, `MediaNext`, `MediaPrev`, `MediaStop`, `MediaPlayPause`, `BrowserBack`, `BrowserForward`, `BrowserRefresh`, `BrowserStop`, `BrowserSearch`, `BrowserFavorites`, `BrowserHome`, `LaunchMail`, `LaunchMediaSelect`, `LaunchApp1`, `LaunchApp2` |
| Special       | `Disable` (disables the key or shortcut), `VK<decimal>` or `0x<hex>` for any other virtual-key code                            |

> **Note:** Punctuation names refer to the key's physical position on a US
> layout (`Semicolon` is the `VK_OEM_1` key), so profiles behave identically
> regardless of the machine's keyboard layout.

## Common operations

```powershell
# Get or export the current remapping profile
PowerToys.DSC.exe get --resource 'profile' --module KeyboardManager
PowerToys.DSC.exe export --resource 'profile' --module KeyboardManager

# Apply a remapping profile
$input = '{"profile":{"keys":[{"from":"CapsLock","to":"Esc"}]}}'
PowerToys.DSC.exe set --resource 'profile' --module KeyboardManager --input $input

# Test whether the current remappings match the desired state
PowerToys.DSC.exe test --resource 'profile' --module KeyboardManager --input $input

# Get the JSON schema of the resource
PowerToys.DSC.exe schema --resource 'profile' --module KeyboardManager
```

## Examples

### Example 1 - Deploy remappings with DSC

```bash
dsc config set --file keyboardmanager-profile.dsc.yaml
```

```yaml
# keyboardmanager-profile.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Deploy key remappings
    type: Microsoft.PowerToys/KeyboardManagerProfile
    properties:
      profile:
        keys:
          - { from: CapsLock, to: Esc }
          - { from: Insert, to: Disable }
          - { from: F2, toText: "you@example.com" }
        shortcuts:
          - { from: "Ctrl+Shift+A", to: "Ctrl+V" }
          - { from: "Win+O, K", toText: "chord-triggered text" }
          - { from: "Ctrl+Alt+N", to: "Ctrl+S", targetApp: "notepad.exe", exactMatch: true }
          - from: "Ctrl+Alt+T"
            runProgram:
              filePath: "%WINDIR%\\System32\\cmd.exe"
              args: "/k echo hello"
          - { from: "Ctrl+Alt+B", openUri: "https://github.com/microsoft/PowerToys" }
```

### Example 2 - Install PowerToys and deploy remappings with WinGet

```bash
winget configure winget-kbm-profile.yaml
```

```yaml
# winget-kbm-profile.yaml
$schema: https://raw.githubusercontent.com/PowerShell/DSC/main/schemas/2023/08/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Install PowerToys
    type: Microsoft.WinGet.DSC/WinGetPackage
    properties:
      id: Microsoft.PowerToys
      source: winget

  - name: Enable Keyboard Manager
    type: Microsoft.PowerToys/KeyboardManagerSettings
    properties:
      settings:
        properties:
          Enabled: true
        name: KeyboardManager
        version: 1.0

  - name: Deploy key remappings
    type: Microsoft.PowerToys/KeyboardManagerProfile
    properties:
      profile:
        keys:
          - { from: CapsLock, to: LCtrl }
        shortcuts:
          - { from: "Ctrl+Shift+V", toText: "Best regards,`nContoso IT" }
```

### Example 3 - Capture existing remappings

Export the current remappings on a reference machine, then reuse them in a
configuration document:

```powershell
PowerToys.DSC.exe export --resource 'profile' --module KeyboardManager
# {"profile":{"keys":[{"from":"CapsLock","to":"Esc"}],"shortcuts":[]}}
```

## See also

- [KeyboardManager Module][02]
- [Settings Resource Reference][01]
- [PowerToys DSC Overview][03]

<!-- Link reference definitions -->
[01]: ./settings-resource.md
[02]: ./modules/KeyboardManager.md
[03]: ./overview.md
