# PowerScripts (prototype)

> **Status: prototype.** Write a small script once and use it across compatible PowerToys modules.
> This folder contains the **working core** (script header format, registry, shared executor
> `PowerScripts.Host.exe`) plus sample scripts and integrations across Settings, Explorer,
> Keyboard Manager, LightSwitch, Command Palette, and Advanced Paste.

## Implemented surfaces (prototype)

| Surface | What it does | How |
| --- | --- | --- |
| **Settings module** | Lists every installed script and its typed parameter metadata; configures module state, runtimes, global MXC restrictions, and per-script MXC policy. It does not execute scripts. Enabling/disabling installs/removes the Explorer entries. | Reads `Host.exe list --json`; the toggle runs `Host.exe shell-install`/`shell-uninstall`. |
| **Explorer right-click** | Supports file scripts (`input: files`) whose declared extensions match the selection. Selected files are supplied automatically; the menu does not collect additional scalar parameters. | Registers per-extension verbs that invoke `Host.exe run <id> --files "%1"`. |
| **Keyboard Manager** | Supports action/system scripts (`input: none`). Its editor renders `string`, `int`, `bool`, `choice`, and file-picker parameters and persists their values with the hotkey mapping. | Saves an ordinary `RunProgram` mapping to `Host.exe run <id>` plus one encoded `--set-base64` value per parameter. |
| **LightSwitch** | Supports action/system scripts (`input: none`). Separate scripts and parameter sets can be configured once for switching to light or dark mode; automatic invocations never show a parameter prompt. | Settings renders `string`, `int`, `bool`, `choice`, and file-picker parameters and approves the selected content; LightSwitch invokes `Host.exe run <id> --no-consent` with encoded saved values after scheduled or manual theme changes. |
| **Command Palette** | Supports action/system scripts (`input: none`). Parameterless scripts are direct commands; parameterized scripts open a typed parameters page, including file pickers. | Invokes `Host.exe run <id>` and sends submitted values as repeated `--set name=value` arguments. |
| **Advanced Paste** | Supports scripts with a pasteable transform contract such as `text -> text`. It supplies clipboard data, but currently has no UI for extra scalar parameters. | Invokes `Host.exe transform <id>` and sends a JSON payload on stdin; parameter defaults are resolved by the Host. |

### End-to-end demo

1. **Settings**: open Settings → PowerScripts → see `convert_md_to_txt`, `volume_up`, etc.; toggle on.
2. **Context menu**: right-click a `.md` file → PowerScript → "Convert Markdown to Text" → the command reports the resulting `.txt` path. With filesystem isolation enabled, the result is retained in the per-run MXC workspace; direct execution writes it next to the input.
3. **Keyboard Manager**: KBM editor → add mapping → action "PowerScript" → pick "Volume Up" → assign a shortcut.


## The idea

A **PowerScript** is a single script file with its metadata in a header comment. A script declares
only what it **consumes** and **produces** (its input → output); there is no separate "kind":

- An **action** — input `none`, output `text` (its stdout). "Do something on my PC." Triggered by a
  Keyboard Manager hotkey or Command Palette command.
- A **file script** — input `files`. "Do something with these files", of declared types. Surfaced in
  the Explorer right-click menu. (A script is "file-driven" precisely when its resolved input is `files`.)
- A **data transform** — e.g. input `text` → output `html`. Consumed by Advanced Paste.

Every module is a thin consumer of one **registry** and invokes one **executor**. Modules discover
compatible scripts from the resolved I/O contract rather than author-declared integration names.

## Architecture

```
 Registry (PowerScripts.Core)  ──read──►  consumers:
   scans <root> for @powerscript.*          • Explorer context menu  (file actions)
   header scripts                           • Keyboard Manager editor (system actions)
                                            • LightSwitch / Command Palette / Advanced Paste
        ▲                                          │ invoke
        └──────────── all consumers ───────────────┘
                          ▼
            PowerScripts.Host.exe (executor)
              list [--json] | run <id> [--files ...] [--set k=v ...]
```

- **`PowerScripts.Core`** — manifest model + header parser (`Manifest/`), validation, registry (`Registry/`),
  executor (`Execution/`).
- **`PowerScripts.Host`** — the CLI every surface points at. `list --json` is the structured catalogue
  the KBM editor picker and future agents/MCP consume; `run <id>` executes.
- **`samples/`** — a dozen descriptor-authored (`.tool.json`) scripts spanning PowerShell/Python,
  actions/file scripts/transforms, typed parameters, WSL-capable Python, and an explicit `x-execute`
  recipe (see the Sample gallery below).

### Scripts root

`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts\<id>.ps1` (or `.py`)
(override with the `POWERSCRIPTS_ROOT` env var or `--root`).

## Script format (header metadata)

A PowerScript is a **single self-contained file**. Its metadata lives in the file's **leading comment
block** as `@powerscript.*` directives (the Raycast-style model), so one file is the whole thing and is
trivial to share — there is no separate `manifest.json`. Directives are `#`-comment lines of the form
`# @powerscript.<key> <value>`:

```powershell
# @powerscript.id           whats-my-ip
# @powerscript.name         What's my IP
# @powerscript.description   Look up this PC's public IP address and show it.
# @powerscript.capability   network
# @powerscript.param        name=greeting type=string label="Greeting" default=Hello

Write-Host 'script body starts here'
```

**Field reference** (all optional unless noted):

| Directive | Meaning |
| --- | --- |
| `id` **(required)** | Portable identity; unique across the catalogue. Letters/digits/`. _ -` only. |
| `name` **(required)** | Display name. |
| `description` (`desc`) | One-line description. |
| `input` / `output` | Explicit I/O shapes (`none`/`text`/`html`/`image`/`audio`/`video`/`files`). Otherwise inferred (Python fn name, declared `extensions`, else `none`→`text`). |
| `runtime` | `powershell` or `python`. Inferred from the extension (`.py`→Python, else PowerShell) if omitted. |
| `function` (`entryfunction`) | Python only: the entry function to call. Omit to use the `powerscript_from_<in>_to_<out>` naming convention. |
| `extensions` | File scripts: accepted extensions (`.md .txt` or `*`). A declared `extensions` list makes input resolve to `files`. Repeatable / comma- or space-separated. |
| `minfiles` / `maxfiles` | File scripts: selection bounds (`maxfiles` 0 = unbounded). |
| `outputextension` | File scripts: the produced extension. |
| `capability` | Declared capability (consent string + agent permission). Repeatable. |
| `param` | A typed parameter (see Parameters). Repeatable. |
| `publisher` (`author`), `version`, `icon`, `source` | Informational metadata. |

The header ends at the first non-blank, non-comment line; non-directive comments (e.g. a license
header) are ignored, and a file with **no** `@powerscript.*` directives is skipped — so a plain helper
script is never mistaken for a PowerScript. Scripts are discovered as a **loose file directly under the
scripts root** (e.g. `scripts\whats-my-ip.ps1`) or as the one header script inside a sub-folder (which
lets a script keep companion assets next to it).

### Consumer discovery

Authors don't hand-list which PowerToys modules expose a script. Each consumer filters the resolved
**input → output** contract it supports: Explorer selects file inputs, action consumers select
`input: none`, and Advanced Paste selects compatible transform contracts.

### Parameters (optional)

A script may declare typed parameters. PowerScripts publishes their names, types, required state,
labels, descriptions, defaults, choices, and numeric bounds; each consuming module decides how to
present that contract. For example, Keyboard Manager renders controls while configuring a hotkey
and persists the selected values. The Host itself never opens a parameter dialog.

Header-authored parameters use quote-aware `key=value` tokens (`name=`, `type=`, `required=`,
`label=`, `description=`, `default=`, `options=`, `min=`, `max=`), with the first two bare tokens
treated as name and type. Descriptor-authored parameters use JSON Schema properties and its
`required` array. Supported types:

- `choice` — one value from a fixed `options` list.
- `bool` — `true` or `false`.
- `int` — an integer honoring `min`/`max`.
- `string` — text.
- `file` — a path selected through the consumer's file picker. The Host requires the path to name
  an existing file. In a descriptor, declare this as a string with `"format": "file-path"`; an
  optional `contentMediaType` remains descriptive and does not make the script file-driven. Under
  MXC, resolved file-parameter paths are granted read-only access just like primary file inputs.

Consumers pass values with `run <id> --set name=value`. The Host validates names and types, applies
declared defaults, and rejects missing required values. Values arrive at scripts as strings, so a
`bool` parameter arrives as the literal `"true"` or `"false"`.

## Script format — explicit `.tool.json` descriptor (MCP Tool shape)

Besides the header comment, a script can be described by an explicit **sidecar descriptor** named
`<script>.tool.json` next to it (e.g. `narrate_image.py.tool.json` beside `narrate_image.py`). This
is the "write the contract by hand" path, and it is deliberately **not a new protocol**: the
descriptor is a standard **MCP `Tool`** object — the same `name` + `description` + `inputSchema`
shape an AI agent already understands — so one artifact doubles as the authoring format and the
agent/MCP contract, with no server to run. It is also **language-agnostic**: it works for `.py`,
`.ps1`, `.cmd`, `.sh` — even scripts that can't carry a comment header.

```jsonc
{
  "name": "narrate-image",
  "title": "Narrate image",
  "description": "Describe an image in one sentence.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "image": { "type": "string", "contentMediaType": "image/*" },
      "voice": { "type": "string", "enum": ["neutral", "cheery"], "default": "neutral" }
    },
    "required": ["image"]
  },

  // The one key MCP does not model: how to turn the JSON inputs into a command line.
  // "x-" prefixed, so any MCP / JSON-Schema reader ignores it and the file stays a valid tool.
  "x-execute": {
    "command": ["python", "narrate_image.py"],
    "argMap": { "voice": "--voice" },
    "stdin": "none"
  },

  // Optional PowerToys-specific residue with no language-native home.
  "x-powerscript": {
    "extensions": [".png", ".jpg"],
    "capabilities": ["fileRead", "network"],
    "mxc": {
      "recommendedPolicies": ["filesystem", "network", "ui", "leastPrivilege"]
    }
  }
}
```

How the descriptor maps onto the manifest the rest of PowerScripts already uses:

| Descriptor field | Becomes |
| --- | --- |
| `name` | `id` (portable identity). |
| `title` / `annotations.title` → else `name` | display name. |
| `description` | description. |
| each **scalar** `inputSchema` property | a typed parameter (`string`/`int`/`bool`; an `enum` → `choice`; `format: file-path` → `file`, with `default`, `minimum`/`maximum`). |
| a primary **file input** property (`contentMediaType` without `format: file-path`) or `x-powerscript.extensions` | makes the script **file-driven** (input resolves to `files`) and sets the Explorer extension filter. |
| `x-execute` | an **optional** explicit launch recipe (see below). |
| `x-powerscript.*` | runtime override, explicit `input`/`output`, Python `function` entry, capabilities, publisher/version/icon/source, and min/max files. |
| `x-powerscript.mxc.recommendedPolicies` | Display-only author recommendation using `filesystem`, `network`, `ui`, and `leastPrivilege`; it never weakens the user's effective policy. Defaults to all four. |

**`x-execute` is optional — it is the escape hatch, not the default.** Omit it and PowerScripts
launches the script with the **built-in runtime** for its language, exactly as a header-authored
script would:

- a `.ps1` runs through Windows PowerShell / `pwsh` (`-NoProfile -NonInteractive -ExecutionPolicy Bypass`);
- a `.py` runs through the **Python function-convention runtime** (`powerscript_from_<in>_to_<out>`),
  which keeps **WSL** execution, the interpreter-path setting, and Windows↔`/mnt/…` path translation.

Add `x-execute` only when you need a launch the built-in runtimes don't give you — a different
interpreter, custom fixed arguments, a language with no built-in runtime (`.cmd`, `.sh`, …), or an
explicit argv/stdin mapping. When present: `command` is the interpreter plus fixed leading args (a
token that names the entry script is resolved to its full path); scalar inputs go on argv (an
`argMap` flag, else `--name`); selected files are passed positionally and via `POWERSCRIPTS_FILES`;
and `"stdin": "json"` instead writes the whole input object to the process' stdin as one JSON
document — the channel for large / structured input. `exitCodes` gives non-zero codes a human
meaning surfaced in errors. Note `x-execute` bypasses the Python function-convention runtime, so
**Python scripts that need WSL should omit it**.

A script with a sibling descriptor is registered from the descriptor only; its body is **not** also
parsed as a header script, so the two authoring paths never collide. Introspection (deriving the
descriptor from a script's native signature) is a planned follow-up; today the descriptor is written
explicitly.

### Sample gallery

All scripts under `samples/` are authored with `.tool.json` descriptors (the header format still
works as an alternative). They cover every path:

| Sample | Language | Input → Output | Launch |
| --- | --- | --- | --- |
| `system-snapshot`, `volume_up`, `whats-my-ip` | PowerShell | none → text | built-in PS runtime |
| `greet` | PowerShell | none → text + **typed parameters** | built-in PS runtime |
| `sha256-checksum`, `convert_md_to_txt`, `copy-as-unc` | PowerShell | files → files/none | built-in PS runtime |
| `py_beep`, `py_greet` | Python | none → text | function-convention runtime (**WSL-capable**) |
| `uppercase` | Python | text → text transform | function-convention runtime (**WSL-capable**) |
| `py_uppercase_files` | Python | files → files | function-convention runtime (**WSL-capable**) |
| `word-count` | PowerShell | files → none | **explicit `x-execute`** recipe |

> There is no separate `system`/`file` "kind": a script is *file-driven* precisely when its resolved
> **input** shape is `files` (declared via `input`/`extensions`), and everything else follows from its
> resolved input → output.

## Security posture — MXC by default when supported, scripts never run elevated

When no explicit preference has been saved, PowerScripts enables MXC only when the host is Windows
11 24H2 (build 26100) or newer and `wxc-exec.exe --probe` reports an available containment tier.
Unsupported hosts default MXC off and show the reason in Settings. Supported hosts run every
PowerShell, descriptor, native Python, and WSL Python launch through the native MXC executor using
stable schema `0.8.0-alpha` and Windows `processcontainer`. The enabled default applies all four
PowerScripts policies:

- `filesystem`: the scripts root/package, runtime/helper paths, and selected inputs are read-only;
  only a unique per-run temporary workspace is read-write. File-producing scripts should write beneath
  the supplied `TEMP` directory and report the resulting path. On Windows process-container tiers,
  MXC also receives read-only grants for the volume roots containing those paths so the narrower
  read-only rules can override rights inherited from the user's token.
- `network`: outbound, inbound, and host-loopback access are denied.
- `ui`: UI is confined to the container desktop; clipboard access, input injection, desktop/system
  control, settings changes, and IME access are denied. Win32k remains available because console
  runtimes such as PowerShell require it during initialization.
- `leastPrivilege`: ProcessContainer least-privilege mode is enabled.

The module `config.json` contains an optional typed `mxc` object:

```json
{
  "mxc": {
    "enabled": true,
    "executorPath": "",
    "disabledPolicies": [],
    "riskAccepted": false,
    "scripts": {
      "stable-script-id": {
        "enabled": null,
        "enabledPolicies": [],
        "disabledPolicies": [],
        "riskAccepted": false
      }
    }
  }
}
```

Any global weakening requires global `riskAccepted: true`; any per-script weakening requires that
script override's `riskAccepted: true`. Otherwise PowerScripts falls back to fully restricted MXC.
Per-script `enabledPolicies` can safely restore restrictions disabled globally, while
`disabledPolicies` grants that script broader access. Settings provides a draft editor with
**Use author recommendation**, **Maximum isolation**, and **Use global policy** presets. For example,
a network-only script can keep filesystem, UI, and least-privilege restrictions while disabling the
network restriction. Author recommendations are informational until the user applies them and accepts
any reduced-isolation risk. Script-package immutability is a non-disableable MXC baseline: whenever
MXC applies, the scripts root and active package remain readable but cannot be modified, created in,
deleted from, or renamed by the script. Disabling the optional `filesystem` restriction grants broad
read-write access to available host volumes, subject to the user's normal permissions, but never
reopens the installed script package. If MXC itself is disabled with accepted risk, this baseline no
longer applies. MXC is an early preview and is not shipped by this
prototype: for end-to-end execution, install a compatible native `wxc-exec.exe` and either configure
`executorPath`, set `POWERSCRIPTS_MXC_EXECUTOR`, place it beside `PowerScripts.Host.exe`, or put it
on `PATH`. Run `PowerScripts.Host.exe mxc-support --json` to inspect the effective platform probe.
An explicit request to enable MXC still fails closed if MXC later becomes unavailable; only the
machine-derived unsupported default runs directly without requiring a risk-acceptance preference.

PowerScripts' WSL mode uses the user's existing `wsl.exe` and selected distribution; it does not use
MXC's experimental WSLC backend. Because Windows `processcontainer` cannot host `wsl.exe`, an
implicit machine-derived MXC default is treated as unsupported for WSL launches and those launches
run directly with a Settings warning. If MXC was explicitly required, the WSL launch is refused
instead of silently bypassing isolation. Users do not need the WSLC SDK, a WSLC-enabled MXC build,
or pre-pulled WSLC images.

PowerToys can run as administrator, but a PowerScript **must never inherit that token**. Every launch
— PowerShell, Python, and descriptor-driven — funnels through a single chokepoint
(`Security/ProcessRunner`) that enforces this in one place:

- When the host is **not** elevated, `wxc-exec.exe` launches under the user's rights.
- When the host **is** elevated, `wxc-exec.exe` itself is launched with the interactive shell user's
  token. If that de-elevation cannot be performed, the run **fails closed** (exit code `126`) —
  PowerScripts never falls back to running MXC or the script elevated.

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

`PowerScripts.Core.Tests` (MSTest) covers manifest serialization/validation, the registry
(extension + wildcard matching, multi-file selection min/max, I/O-based filtering, invalid-script
skipping), the `.tool.json` descriptor path (parsing scalar/file inputs, explicit Python `function`
entry, registry discovery, end-to-end descriptor execution), the host CLI contract (spawning the real
exe), the never-elevated process runner, MXC policy, typed/file parameters, and consumer discovery.

## Agent / AI tie-in (designed-for)

`Host.exe list --json` already yields a structured, permissioned capability list and `run <id>` is
the invoke — so an MCP server can expose installed PowerScripts as user-consented tools. AI authoring
("generate a PowerScript that…") emits a manifest + script folder the user reviews once.
