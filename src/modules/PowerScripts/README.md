# PowerScripts (prototype)

> **Status: prototype.** Write a small script once and use it across compatible PowerToys modules.
> This folder contains the **working core** (MCP-style descriptors, registry, shared executor
> `PowerScripts.Host.exe`), starter templates, and sample scripts. Integrations with
> other PowerToys modules are developed in separate branches.

## Consumer-owned capabilities

PowerScripts alone adds no end-user action surface. It supplies script discovery, the metadata
protocol and shared execution/security policy to compatible PowerToys modules. An Explorer
integration belongs in **File Explorer Add-ons**, not PowerScripts. The earlier prototype shell
handler and its `shell-menu`, `shell-install`, `shell-uninstall` verbs have been removed; no new
File Explorer Add-ons integration is implemented here.


## The idea

A **PowerScript** is a script file paired with a required sibling `.tool.json` descriptor.
A script declares what it **consumes** and **produces** (its input → output); there is no separate "kind":

- An **action** — input `none`, output `text` (its stdout). "Do something on my PC." Triggered by a
  Keyboard Manager hotkey or Command Palette command.
- A **file script** — input `files`. "Do something with these files", of declared types. Surfaced in
  a compatible file consumer. (A script is "file-driven" precisely when its resolved input is `files`.)
- A **data transform** — e.g. input `text` → output `files` containing an HTML document. Consumed by Advanced Paste.

Every module is a thin consumer of one **registry** and invokes one **executor**. Modules discover
compatible scripts from the resolved I/O contract, optionally narrowed by discovery audience.

## Architecture

```
 Registry (PowerScripts.Core)  ──read──►  consumers:
   scans <root> for *.tool.json             • File Explorer Add-ons (future file consumer)
   descriptors                              • Keyboard Manager editor (system actions)
                                            • LightSwitch / Command Palette / Advanced Paste
        ▲                                          │ invoke
        └──────────── all consumers ───────────────┘
                          ▼
            PowerScripts.Host.exe (executor)
              list [--json] | run <id> [--files ...] [--set k=v ...]
```

- **`PowerScripts.Core`** — manifest model + descriptor parser (`Manifest/`), validation, registry (`Registry/`),
  executor (`Execution/`).
- **`PowerScripts.Host`** — the CLI every surface points at. `list --json` is the structured catalogue
  the KBM editor picker and future agents/MCP consume; `run <id>` executes.
- **`CONSUMER-INTEGRATION.md`** — conventions and validation checklist for adding another PowerToys
  consumer without bypassing the shared Host, trust, or security model.
- **`samples/`** — a dozen descriptor-authored (`.tool.json`) scripts spanning PowerShell/Python,
  actions/file scripts/transforms, typed parameters, WSL-capable Python, and an explicit `x-execute`
  recipe (see the Sample gallery below).

### Scripts root

`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts\<id>.ps1` (or `.py`)
(override with the `POWERSCRIPTS_ROOT` env var or `--root`).

## Temporary Debug test window

**Open launcher** on the PowerScripts Settings page is available in **Debug builds only**.
There is no global shortcut or standalone Runner module. This is a removable testing aid,
not a planned product capability. Release builds do not contain the window or expose its route.

Search scripts with **no primary input**, select one, and complete its typed parameter form when
it has parameters. Scripts without parameters run immediately on selection. Execution still goes
through the shared Host, including normal trust consent, runtime availability, parameter validation,
and MXC policy. Results show literal text, completion without output, or a file list. **Copy text**
copies text; **Save as** saves a separate copy of one result file; **Copy files** places real file
items on the clipboard for pasting into Explorer. Returned files are never moved or deleted.
Failures show the Host exit code and diagnostics; runs can be cancelled and have a 60-second limit.
Escape cancels an active run or hides an idle launcher.

This is a small removable consumer: `PowerScriptLauncherWindow` owns the three-screen
UI and uses `PowerScripts.Client`, not the execution engine. It adds no service, execution protocol,
or security-policy exception. Remove the Debug launcher route/window/card,
and launcher model/tests to retire the experiment without changing script execution.

## Script packages

For distribution, use a sibling descriptor: `example.py` plus `example.py.tool.json`, or
`example.ps1` plus `example.ps1.tool.json`. Descriptors are discovered directly under the scripts
root and one sub-folder below it, so a package can keep companion assets beside its script.
Script bodies are not scanned for metadata or special function names.

### Templates and authoritative metadata

Settings offers **Greeting with parameters**, **Create a text file**, and **Describe selected files**
starter templates. **Set up...** asks for a project name and description before creating anything.
The file-input example also asks for allowed extensions: `.txt, .md`, `*.png`, or plain `txt`
are accepted and normalized; leave blank for all types. Only relevant options are shown.
**Create script** writes those choices directly into the descriptor and creates a new package
folder derived from the project name with a unique action ID, editable `script.ps1`,
`script.ps1.tool.json`, and a README explaining every example field and the safe MXC policy.
It opens that package in Explorer. Creation never runs code, approves trust, or relaxes policy.
All examples work with the default MXC restrictions enabled. The file-output example writes inside
the isolated temporary workspace and returns explicit `file_paths`; the file-input example reads
only consumer-supplied files staged by the Host. The Debug test window lists only no-input scripts.

The CLI equivalents are `template list` and `template create <template-id> [--configure] [--root <dir>]`.
With `--configure`, pass a JSON object on stdin, for example
`{"projectName":"My file helper","description":"Describe my files.","fileExtensions":".txt, .md"}`.
Omit `fileExtensions` for templates without file input. Without `--configure`, template defaults apply.
To add a template, add its assets under `PowerScripts.Core/Templates/<id>/` and one catalogue
entry in `ScriptTemplates.All`. Assets are embedded automatically; Settings builds its cards
from the Host catalogue.

The installed script list supports live, case-insensitive name search combined with input and output
type filters. The selected sort order applies to the matching scripts.

The `.tool.json` descriptor is authoritative for names, I/O, parameters, consumers and execution
recipes. Script pages display resolved metadata as grouped, read-only property/value cards, including
parameter types, defaults, choices and bounds. **Edit metadata** and **Edit script** request the Windows
**Edit** action rather than the default Open action (which can execute scripts or launch a non-editor).
If no Edit association exists, Settings opens Notepad and reports that fallback.
**Show in File Explorer** opens the containing folder with the script selected. The visible
**Execution isolation (MXC)** section above the metadata provides per-script policy configuration,
effective isolation and the author's recommendations without expanding advanced details.
After saving, select **Refresh** to reload the properties, catalogue, trust and MXC status. Metadata and
script edits require trust review again. Raw JSON remains available in a collapsed advanced section.
There are no normal metadata overrides, pencil/reset editors or descriptor-free registrations.
Bare helper scripts are never actions. Descriptor-backed packages are discovered automatically;
there is no manual registration step. Use a template for a new package. A pair such as
`scripts\my-script\main.ps1` and `scripts\my-script\main.ps1.tool.json` is supported.
Discovery scans the root and its immediate subfolders, not arbitrary nested folders.

Separate records under
`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\script-settings\<catalogue-path-hash>\<source-path-hash>.json`
are retained only for per-script MXC policy and content-bound risk acceptance. They use the
existing atomic writes, size limits and source/root identity checks. Descriptor-backed scripts
need no record unless their security settings differ. Trust and runtime gates remain unchanged.

**Migration:** existing prototype metadata overrides are ignored, never copied into descriptors.
Their stored values are retained until security settings are edited/reset so source files are
not silently rewritten. Old bare registrations remain unavailable until a descriptor is supplied
or the record is forgotten. Changed effective content invalidates content-bound policy acceptance.
Reset inherits global policy without reviving legacy per-ID relaxations. Ordinary same-user
JSON records are not a tamper-resistant boundary.

`script-settings get|save|reset|forget --entry <path> [--root <dir>]` now saves only
`{"mxc":{...}}`; `"mxc":null` restores global policy. Requests containing `overrides` are rejected.
The read snapshot retains its old descriptor-shaped fields for compatibility, with empty overrides.

**Migration:** inline metadata is no longer supported. Move identity, parameters, file filters,
icons, and capabilities into `.tool.json`. Built-in Python scripts must declare
`x-powerscript.function` and should explicitly declare `input` and `output`; omitting the function
is a validation error, not a request to infer it from Python source. Script or descriptor edits
invalidate existing content-hash trust and require approval again.

### Consumer discovery

Each consumer filters the resolved
**input → output** contract it supports: file consumers select file inputs, action consumers select
`input: none`, and Advanced Paste selects compatible transform contracts.

Optional `"x-powerscript": { "consumers": ["color-picker"] }` narrows normal discovery for
`list --consumer color-picker`. Omitted/null means every compatible consumer; a nonempty list
targets stable, nonlocalized module IDs, matched case-insensitively. An explicit empty list is
invalid. Omit `--consumer` for a complete management catalogue. This is a discovery preference,
**not** execution authorization: direct invocation still uses the normal trust/MXC gate.
Each consuming module supplies its own ID.

Inputs and outputs are only `none`, `text`, and `files`. Inline HTML is a string (`text`); images,
audio, video, and HTML documents use `files` with extension and selection-count filters. A PNG
materialized from the clipboard and a PNG selected in Explorer match the same scripts. For example:

```json
{
  "name": "inspect-images",
  "x-powerscript": {
    "function": "inspect_images",
    "input": "files",
    "output": "text",
    "extensions": [".png", ".jpg"],
    "minFiles": 1,
    "maxFiles": 0
  }
}
```

`inspect_images(paths)` receives the complete file-path list once. `maxFiles: 0` is unbounded;
set it to `1` for single-file scripts. Consumers use `ListForFiles` to check both extensions and
count, irrespective of where those files originated.

Old `input` or `output` declarations of `html/image/audio/video` are rejected. Use `text` for literal
markup and `files` for rich HTML documents or media, and update the payloads accordingly.

### Transform results

The descriptor's `output` must match the returned result:

| Output | Transform JSON | Built-in Python return |
| --- | --- | --- |
| `none` | `{}` | `None` |
| `text` | `{"text":"literal content"}` | A string, or the explicit JSON-shaped dictionary |
| `files` | `{"file_paths":["C:\\output\\result.png"]}` | A path list/tuple, or the explicit dictionary |

A single file is still a list. A string containing a path is text, not an inferred file result.
Unknown fields (including old `html`, `image_path`, `audio_path`, and `video_path` fields), malformed
JSON, and results that do not match the descriptor are errors rather than successful empty output.
Python functions must return their result; diagnostics belong on stderr, not stdout.

PowerShell and explicit launch recipes use stdout as literal content for `output: text`.
For `output: files`, emit the explicit JSON object, for example:

```powershell
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
@{ file_paths = @($producedPaths) } | ConvertTo-Json -Compress
```

The output protocol uses UTF-8. Set the encoding explicitly in PowerShell scripts, especially
Windows PowerShell 5.1, to preserve Unicode filenames rather than replacing characters with `?`.

For `output: none`, transform stdout is diagnostics and the Host emits `{}`.
The `run` verb retains its human-readable stdout behavior.

Consumers decide whether to paste a returned PNG as an image or a file, and whether to read an
HTML file as rich content. The core does not inspect extensions to guess presentation intent.
Keep produced files available until the consumer has finished reading them; clipboard file
references may need a lifetime longer than the script invocation.

Consumer `list` discovery omits Python scripts while Python execution is disabled in Settings.
Windows and WSL modes both enable discovery. The Settings catalogue uses
`list --json --include-disabled --include-unconfigured` to keep all installed and unavailable
registered scripts visible for configuration; these flags
do not enable Python or bypass execution checks.

### Parameters (optional)

A script may declare typed parameters. PowerScripts publishes their names, types, required state,
labels, descriptions, defaults, choices, and numeric bounds; each consuming module decides how to
present that contract. For example, Keyboard Manager renders controls while configuring a hotkey
and persists the selected values. The Host itself never opens a parameter dialog.

Parameters use JSON Schema properties and its `required` array in the descriptor. Supported types:

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

Every script is described by a **sidecar descriptor** named
`<script>.tool.json` next to it (e.g. `narrate_image.py.tool.json` beside `narrate_image.py`). This
is the "write the contract by hand" path, and it is deliberately **not a new protocol**: the
descriptor is a standard **MCP `Tool`** object — the same `name` + `description` + `inputSchema`
shape an AI agent already understands — so one artifact doubles as the authoring format and the
agent/MCP contract, with no server to run. It is also **language-agnostic**: it works for `.py`,
`.ps1`, `.cmd`, and `.sh`.

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
    "output": "text",
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
| a primary **file input** property (`contentMediaType` without `format: file-path`) or `x-powerscript.extensions` | makes the script **file-driven** (input resolves to `files`) and sets the consumer file filter. |
| `x-execute` | an **optional** explicit launch recipe (see below). |
| `x-powerscript.*` | runtime override, explicit `input`/`output`, Python `function` entry, capabilities, publisher/version/icon/source, and min/max files. |
| `x-powerscript.mxc.recommendedPolicies` | Display-only author recommendation using `filesystem`, `network`, `ui`, and `leastPrivilege`; it never weakens the user's effective policy. Defaults to all four. |

**`x-execute` is optional — it is the escape hatch, not the default.** Omit it and PowerScripts
launches the script with the **built-in runtime** for its language:

- a `.ps1` runs through Windows PowerShell / `pwsh` (`-NoProfile -NonInteractive -ExecutionPolicy Bypass`);
- a `.py` runs through the **Python runtime**, calling the descriptor's `x-powerscript.function`,
  which keeps **WSL** execution, the interpreter-path setting, and Windows↔`/mnt/…` path translation.

Add `x-execute` only when you need a launch the built-in runtimes don't give you — a different
interpreter, custom fixed arguments, a language with no built-in runtime (`.cmd`, `.sh`, …), or an
explicit argv/stdin mapping. When present: `command` is the interpreter plus fixed leading args (a
token that names the entry script is resolved to its full path); scalar inputs go on argv (an
`argMap` flag, else `--name`); selected files are passed positionally and via `POWERSCRIPTS_FILES`;
and `"stdin": "json"` instead writes the whole input object to the process' stdin as one JSON
document — the channel for large / structured input. `exitCodes` gives non-zero codes a human
meaning surfaced in errors. Note `x-execute` bypasses the built-in Python runtime, so
**Python scripts that need WSL should omit it**.

For built-in Python execution, the function name is arbitrary and its I/O is declared in metadata:

```json
{
  "name": "list-files",
  "description": "Return selected file paths as text.",
  "inputSchema": { "type": "object", "properties": {} },
  "x-powerscript": {
    "function": "process_files",
    "input": "files",
    "output": "text",
    "extensions": [".txt", ".md"]
  }
}
```

The sibling `list-files.py` needs only normal Python code:

```python
def process_files(paths):
    return "\n".join(paths)
```

Metadata is never inferred from that function's name. The Host passes the full path list once;
the function decides how to process the batch. Windows and WSL share this same entry contract.

### Sample gallery

All scripts under `samples/` are authored with `.tool.json` descriptors. They cover every path:

| Sample | Language | Input → Output | Launch |
| --- | --- | --- | --- |
| `system-snapshot`, `volume_up`, `whats-my-ip` | PowerShell | none → text | built-in PS runtime |
| `greet` | PowerShell | none → text + **typed parameters** | built-in PS runtime |
| `sha256-checksum`, `convert_md_to_txt`, `copy-as-unc` | PowerShell | files → files/none | built-in PS runtime |
| `py_beep` | Python | none → none | descriptor-selected function (**WSL-capable**) |
| `py_greet` | Python | none → text | descriptor-selected function (**WSL-capable**) |
| `uppercase` | Python | text → text transform | descriptor-selected function (**WSL-capable**) |
| `py_uppercase_files` | Python | files → files | descriptor-selected function (**WSL-capable**) |
| `word-count` | PowerShell | files → none | **explicit `x-execute`** recipe |

> There is no separate `system`/`file` "kind": a script is *file-driven* precisely when its resolved
> **input** shape is `files` (declared via `input`/`extensions`), and everything else follows from its
> resolved input → output.

## Security posture — MXC by default when supported, scripts never run elevated

When no explicit preference has been saved, PowerScripts enables MXC only when the host is Windows
11 24H2 (build 26100) or newer and `wxc-exec.exe --probe` reports an available containment tier.
Unsupported hosts default MXC off and show the reason in Settings. Supported hosts run every
PowerShell, descriptor, and native Python launch through the native MXC executor using
schema `0.8.0-alpha` and Windows `processcontainer` (see the WSL exception below). The enabled default applies all four
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
longer applies. MXC remains an early preview; its profiles must not be treated as a security boundary.
PowerScripts now bundles an architecture-matched, private MXC runtime beside `PowerScripts.Host.exe`;
users do not install MXC separately or fill in an executor path. Builds restore the pinned official
release declared in `Mxc.Runtime.targets`, verify its SHA-256, and copy the executor, diagnostic
sidecar, license, and upstream SPDX manifest into build/publish output. The PowerToys installer
includes that self-contained Host payload under `PowerScripts`. The compatibility pin is v0.8.0:
later releases do not support this prototype's least-privilege BaseContainer policy on existing hosts.
Updating the pin requires real execution checks, not just a successful platform probe.
No elevated host-preparation utility or host-DACL mutation is enabled. Missing or incompatible
runtime files require repair/reinstall, not a silent reduction in restrictions. Legacy `executorPath`
and `POWERSCRIPTS_MXC_EXECUTOR` overrides remain supported for development, but are not required.
Run `PowerScripts.Host.exe mxc-support --json` to inspect the effective platform probe.
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
