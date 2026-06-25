# PowerScripts (prototype)

> **Status: prototype.** Write a small script once and surface it across PowerToys.
> This folder currently contains the **working core**: the manifest schema, the registry, and the
> shared executor (`PowerScripts.Host.exe`), plus two sample scripts. Surface integrations
> (Explorer right-click and Keyboard Manager) are specified below and wired against this core.

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
- **`samples/`** — `system-snapshot` (system) and `sha256-checksum` (file).

### Scripts root

`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts\<id>\manifest.json`
(override with the `POWERSCRIPTS_ROOT` env var or `--root`).

## Manifest schema (v1)

```jsonc
{
  "schemaVersion": 1,
  "id": "heic-to-jpg",            // must match the folder name
  "name": "Convert HEIC to JPG",
  "description": "…",
  "kind": "file",                 // "system" | "file"
  "runtime": "powershell",        // prototype: powershell only
  "entry": "run.ps1",
  "input": { "extensions": [".heic"], "minFiles": 1, "maxFiles": 0 }, // file kind
  "output": { "type": "convertedFile", "extension": ".jpg" },
  "parameters": [ { "name": "quality", "type": "int", "default": "90", "min": 1, "max": 100 } ],
  "surfaces": ["contextMenu", "keyboardManager"],
  "capabilities": ["fileWrite"],  // consent string + agent permission contract
  "elevation": "asInvoker"        // prototype always runs non-elevated
}
```

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
skipping). 9 tests, all passing.

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
