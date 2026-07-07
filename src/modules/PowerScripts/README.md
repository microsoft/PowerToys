# PowerScripts (prototype)

> **Status: prototype.** Write a small script once and surface it across PowerToys.
> This folder contains the **working core** (manifest schema, registry, shared executor
> `PowerScripts.Host.exe`) plus sample scripts, and three **implemented surfaces**:
> a Settings module page, the Explorer right-click menu, and the Keyboard Manager editor.

## Implemented surfaces (prototype)

| Surface | What it does | How |
| --- | --- | --- |
| **Settings module** | New "PowerScripts" page in the Settings app that lists installed scripts and has an enable toggle. Enabling/disabling installs/removes the Explorer context-menu entries. | `src/settings-ui/.../Views/PowerScriptsPage.xaml(.cs)` + `PowerScriptsViewModel`; reads `Host.exe list --json`; toggle runs `Host.exe shell-install`/`shell-uninstall`. |
| **Explorer right-click** | Right-click a file → "PowerScript" submenu lists scripts whose manifest declares that extension; clicking runs the script on the file. | `Host.exe shell-install` writes `HKCU\Software\Classes\SystemFileAssociations\<ext>\shell\PowerScripts` cascading verbs → `Host.exe run <id> --files "%1"`. |
| **Keyboard Manager** | A new "PowerScript" action in the KBM editor; pick a system script and assign it to a hotkey. | `KeyboardManagerEditorUI` action picker saves an ordinary `RunProgram` mapping → `Host.exe run <id>`. |

### End-to-end demo

1. **Settings**: open Settings → PowerScripts → see `convert_md_to_txt`, `volume_up`, etc.; toggle on.
2. **Context menu**: right-click a `.md` file → PowerScript → "Convert Markdown to Text" → a `.txt` is written next to it.
3. **Keyboard Manager**: KBM editor → add mapping → action "PowerScript" → pick "Volume Up" → assign a shortcut.


## The idea

A **PowerScript** is a script plus a manifest, living in its own folder. Two flavours:

- **System** (`kind: "system"`) — "do something on my PC". No file input. Triggered by a Keyboard
  Manager hotkey (and later the Command Palette).
- **File** (`kind: "file"`) — "do something with this file". Input is one or more files of declared
  types. Surfaced in the Explorer right-click menu.

Every surface is a thin consumer of one **registry** and invokes one **executor** — so a script is
authored once and appears everywhere it's declared.

## Architecture

```
 Registry (PowerScripts.Core)  ──read──►  surfaces:
   scans <root>/<id>/manifest.json          • Explorer context menu  (file actions)
                                            • Keyboard Manager editor (system actions)
                                            • Command Palette / Advanced Paste (later)
        ▲                                          │ invoke
        └──────────── all surfaces ────────────────┘
                          ▼
            PowerScripts.Host.exe (executor)
              list [--json] | run <id> [--files ...] [--set k=v ...]
```

- **`PowerScripts.Core`** — manifest model + JSON (`Manifest/`), validation, registry (`Registry/`),
  executor (`Execution/`).
- **`PowerScripts.Host`** — the CLI every surface points at. `list --json` is the structured catalogue
  the KBM editor picker and future agents/MCP consume; `run <id>` executes.
- **`samples/`** — `system-snapshot` & `volume_up` (system), `sha256-checksum` & `convert_md_to_txt` (file).

### Scripts root

`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts\<id>\manifest.json`
(override with the `POWERSCRIPTS_ROOT` env var or `--root`).

## Manifest schema (v1)

```jsonc
{
  "schemaVersion": 1,
  "id": "heic-to-jpg",            // portable identity; need not match the folder name
  "name": "Convert HEIC to JPG",
  "description": "…",
  "kind": "file",                 // "system" | "file"
  "runtime": "powershell",        // "powershell" | "python"
  "entry": "run.ps1",
  "input": { "extensions": [".heic"], "minFiles": 1, "maxFiles": 0 }, // file kind
  "output": { "type": "convertedFile", "extension": ".jpg" },
  "promptForParameters": true,    // optional: show a dialog to collect parameters before running
  "parameters": [
    { "name": "quality", "type": "int", "default": "90", "min": 1, "max": 100 },
    { "name": "mode", "type": "choice", "options": ["fast", "best"], "default": "best", "label": "Mode" },
    { "name": "overwrite", "type": "bool", "default": "false" }
  ],
  "surfaces": ["contextMenu", "keyboardManager"],
  "capabilities": ["fileWrite"],  // consent string + agent permission contract
  "elevation": "asInvoker"        // prototype always runs non-elevated
}
```

### Parameters (optional)

A script may declare typed `parameters`. When `promptForParameters` is `true`, PowerScripts shows a
small dialog before running so the user can pick/enter values; the chosen values are passed to the
script (PowerShell as `-Name value`, Python as keyword arguments). Values arrive as **strings**, so a
`bool` parameter is passed as the literal `"true"`/`"false"`. Supported types:

- `choice` — one value from a fixed `options` list (rendered as a dropdown).
- `bool` — a checkbox.
- `int` — a numeric box honoring `min`/`max`.
- `string` — a text box.

When `promptForParameters` is omitted/`false`, no UI shows and parameters only come from an explicit
`--set name=value` (unchanged behavior). Pass `--no-prompt` to `run` to suppress the dialog for
automated invocations. See the `greet` (PowerShell) and `py_greet` (Python) samples.

## Build & run

```powershell
cd src\modules\PowerScripts
dotnet build PowerScripts.Host\PowerScripts.Host.csproj -c Debug

$env:POWERSCRIPTS_ROOT = "$PWD\samples"
$exe = "PowerScripts.Host\bin\Debug\net10.0\PowerScripts.Host.exe"
& $exe list
& $exe run system-snapshot
& $exe run sha256-checksum --files C:\some\file.png
```

> The prototype projects are isolated from the repo build via local `Directory.Build.props`,
> `Directory.Packages.props` and `nuget.config` (no StyleCop / warnings-as-errors / central package
> management; restores from public nuget.org). Delete these three files when promoting the module to
> follow standard PowerToys build rules.

## Tests

```powershell
cd src\modules\PowerScripts
dotnet test PowerScripts.Core.Tests\PowerScripts.Core.Tests.csproj
```

`PowerScripts.Core.Tests` (MSTest) covers manifest serialization/validation and the registry
(extension + wildcard matching, multi-file selection min/max, kind filtering, invalid-script
skipping). 32 tests, all passing.

## Surface integration plans

### 1. Keyboard Manager (system actions) — first priority

KBM already has a `RunProgram` action, so a hotkey → PowerScript works **today**. Get the exact
mapping for a system script:

```powershell
& $exe kbm system-snapshot          # prints Program path + Arguments for the editor
& $exe kbm system-snapshot --json   # prints the raw remapShortcutsToRunProgram object
```

Then in Keyboard Manager → *Remap a shortcut* → action **Run Program**, paste the Program path and
`run <id>` arguments and choose the trigger keys. The mapping persists as the existing engine shape
(verified against `common/KeyboardManagerConstants.h`):

```json
{ "operationType": 1, "runProgramFilePath": "…\\PowerScripts.Host.exe", "runProgramArgs": "run system-snapshot", "unicodeText": "*Unsupported*" }
```

**Prototype goal — pick a PowerScript inside the editor** (instead of typing a path). The editor is
**C# WinUI 3** (`PowerToys.KeyboardManagerEditorUI.exe`), a separate process that already reads JSON
at runtime, so it can call `Host.exe list --json` to populate a script dropdown. Additive change-list
(verified against the current source):

- `Controls/UnifiedMappingControl.xaml.cs` — the nested `enum ActionType` (KeyOrShortcut, Text,
  OpenUrl, OpenApp, MouseClick, Disable): add a `PowerScript` value; extend `CurrentActionType`,
  `SetActionType`, `IsInputComplete`.
- `Controls/UnifiedMappingControl.xaml` — add a `ComboBoxItem` (Tag `PowerScript`) to
  `ActionTypeComboBox` and a `SwitchPresenter` `Case` hosting a script-picker ComboBox.
- `Pages/MainPage.xaml.cs` — add a `UnifiedMappingControl.ActionType.PowerScript` arm to the save
  `switch` (~line 390) that reuses the `SaveProgramMapping` path with
  `ProgramPath = <PowerScripts.Host.exe>` and `ProgramArgs = "run <id>"`.
- A small helper in `KeyboardManagerEditorUI` to load the script list (shell out to `Host.exe
  list --json`, like `Settings/SettingsManager.cs` reads its JSON).
- **No KBM engine change** — it stays a `RunProgram` mapping.

> The editor-picker edits live in the shared KBM WinUI project, which needs the full PowerToys build
> (VS + internal NuGet feeds) to compile — do them in that environment. The `kbm` command above is
> the verifiable, build-free path that already delivers hotkey → PowerScript.

### 2. Explorer right-click (file actions)

A single compiled `IExplorerCommand` COM handler (pattern: `src/modules/NewPlus/NewShellExtensionContextMenu`)
reads the registry, filters `kind:"file"` scripts whose `input.extensions` match the selection, and
shows a dynamic submenu. Invoking an item runs `Host.exe run <id> --files <paths>`.

### Deferred (kept easy by the registry design)

Command Palette (one `ICommandProvider` extension enumerating system scripts) and Advanced Paste —
both become additional registry-reading adapters. No core changes expected.

## Agent / AI tie-in (designed-for)

`Host.exe list --json` already yields a structured, permissioned capability list and `run <id>` is
the invoke — so an MCP server can expose installed PowerScripts as user-consented tools. AI authoring
("generate a PowerScript that…") emits a manifest + script folder the user reviews once.
