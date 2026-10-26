# PowerScripts (prototype)

Write a script once and expose it to compatible PowerToys modules through
`PowerScripts.Host.exe`. PowerScripts supplies discovery, validation, trust, and
execution, not a standalone product launcher. Consumer integrations are developed
separately; a future Explorer surface belongs to File Explorer Add-ons.

## Packages and discovery

A package contains a script and its required sibling descriptor:
`script.ps1` + `script.ps1.tool.json`, or `script.py` + `script.py.tool.json`.
The default root is `%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts`;
override it with `--root` or `POWERSCRIPTS_ROOT`.
Discovery scans that root and its immediate subfolders. Bare helper scripts are
not actions. Inline metadata and source/function introspection are not supported.

The descriptor uses the MCP Tool shape, with a deliberately constrained
PowerScripts JSON Schema profile. Both `inputSchema` and `outputSchema` are
mandatory here, even though MCP itself makes output schemas optional.
The runtime is inferred from `.ps1` or `.py`.

```json
{
  "name": "describe-files",
  "title": "Describe files",
  "description": "Read selected text files with typed options.",
  "inputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": {
      "files": {
        "type": "array",
        "minItems": 1,
        "items": {
          "type": "string",
          "x-powerscript": { "extensions": [".txt", ".md"] }
        }
      },
      "parameters": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "count": { "type": "integer", "minimum": 1, "default": 2 },
          "enabled": { "type": "boolean", "default": false },
          "mode": { "type": "string", "enum": ["short", "long"], "default": "short" }
        }
      }
    },
    "required": ["files"]
  },
  "outputSchema": {
    "type": "object",
    "additionalProperties": false,
    "properties": { "text": { "type": "string" } },
    "required": ["text"]
  },
  "x-powerscript": { "capabilities": ["fileRead"] }
}
```

### Schema profile

Each envelope is a closed object (`additionalProperties: false`) with at most
one required primary field: `text` (string) or `files` (array of path strings).
No primary field means no primary input/output. An empty closed output object
declares no result. Only input may also contain a `parameters` object.

Named options belong in `inputSchema.properties.parameters.properties`.
Supported option types are string, 32-bit integer, and boolean. String `enum`
provides choices; `format: "file-path"` identifies a consumer-picked auxiliary
file, not primary file input. Options support `title`, `description`, `default`,
their object's `required`, integer `minimum`/`maximum`, and string
`minLength`/`maxLength`/`pattern`. Required means presence; use `minLength` to
reject empty strings. Consumers supply Windows paths; file parameters must name
existing files. Named options cannot shadow the active primary argument.

File arrays use standard `minItems` and `maxItems`. **Omitted `maxItems` is
unbounded; `maxItems: 0` permits only an empty array.**
Their string items may carry `x-powerscript.extensions`, such as `[".wav",".mp3"]`.
Omitted extensions or `["*"]` accept all extensions. Scalar file parameters
use the same annotation. Extension matching is case-insensitive; it does not
validate file contents or grant filesystem permissions.
The Host enforces schema constraints on input and output, not just discovery.

This is not a complete JSON Schema implementation. Supported node types are
object, array, string, integer, and boolean; arbitrary nested option objects,
number/null types, unions, references, and schema composition are not supported.
Unsupported schema keywords and formats fail explicitly instead of being
silently treated as enforced. Regular-expression matching has a bounded timeout.
Defaults retain JSON types. Missing parameter bags are treated as empty bags,
so declared defaults and required options apply consistently.

Root `x-powerscript` is optional and contains only `capabilities`, `consumers`,
`publisher`, `version`, `source`, and `icon`. Capabilities describe intent, not
permissions. Consumers narrow discovery, not execution authorization.
Icons are package-relative ICO files, at most 1 MiB, with no traversal,
absolute/URL paths, or symlink/junction escapes.
There are no runtime, function, I/O, file-filter, or MXC fields at the root.

## Script execution

Python runs directly from its first top-level statement:

```python
import json
import sys

request = json.load(sys.stdin)
options = request["parameters"]
print("\n".join(request["files"][:options["count"]]))
```

There is no named entry function, import wrapper, or keyword-argument runner.
Input is one UTF-8 JSON object with the declared `text` or `files`, plus typed
`parameters`. Python receives real booleans and integers.

PowerShell uses ordinary `param(...)`: `$Text` or `$Files` for primary data,
plus named options. The Host uses JSON and native splatting to preserve booleans,
empty strings, and complete path arrays in PowerShell 7 and Windows PowerShell 5.1.
`POWERSCRIPTS_FILES` also contains the newline-separated selection.
Profiles are disabled and execution policy Bypass applies only to that process.
Set `[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)` in scripts.

| Declared output | Script stdout | Host `transform` result |
| --- | --- | --- |
| No primary field | Diagnostics only | `{}` |
| Required `text` string | Literal text | `{"text":"..."}` |
| Required `files` array | `{"files":["absolute path"]}` | The validated object |

These rules are identical for Python, PowerShell, and explicit recipes. Text is
never guessed to be a file path. For file output, stdout must contain only the
result JSON; send diagnostics to stderr. A single file remains an array.
Use temporary workspace files for output, not the installed package.
Consumers own presentation and result lifetime, including deferred clipboard reads.
Images, audio, video, and rich documents are files; inline markup is text.

Optional `x-execute` is an advanced escape hatch for another interpreter or language.
`command` names the executable and fixed arguments; a sibling script token is
resolved to its absolute path. With `stdin: "json"`, the whole schema-shaped
request goes to stdin. Otherwise, named options use `argMap` or `--name`,
primary text uses the mapped `text` flag or `--text`, and files are positional.
`exitCodes` supplies nonzero-exit descriptions. Explicit recipes bypass built-in
Python environment selection, not shared trust or isolation.

## Settings and templates

**Create Script** generates editable packages for all 18 runtime/input/output
combinations. Creation never runs code, grants trust, or changes isolation.
The form's maximum-file value 0 means unlimited and generates an *omitted*
`maxItems`; it is not serialized as `maxItems: 0`.
The CLI equivalent is `template create --configure`, reading creation options
such as `{"projectName":"My script","runtime":"python","input":"files","output":"text"}`.
`template list` and `template create <id>` expose the three educational templates.

The catalogue supports name search, sorting, and independent input/output filters.
Details are read-only descriptor metadata and global effective execution policy.
**Edit metadata** and **Edit script** use the Windows Edit verb, with a reported
Notepad fallback. **Refresh** reloads metadata and trust status. There are no
per-script settings, local overrides, policy recommendations, or script-settings CLI.
Old local settings are not read. Body or descriptor edits invalidate exact-content
trust; changed scripts require renewed consent.

The Debug-only **Open launcher** is a removable testing aid, not a product surface.
It lists no-primary-input scripts, collects typed options, and uses the shared
client/Host. Text and file results support copying or saving separate copies;
returned files are never moved/deleted. Cancellation and a 60-second limit apply.

## Global isolation

All scripts use the global `mxc` configuration:

```json
{ "mxc": { "enabled": true, "disabledPolicies": [], "riskAccepted": false } }
```

Without a saved preference, MXC defaults on only on Windows 11 24H2/build 26100+
when its platform probe reports support; unsupported machines show the reason.
Explicitly required but unavailable MXC fails closed. Global weakening requires
explicit `riskAccepted: true`; otherwise all restrictions remain enabled.
There are no per-script exceptions.

The four restrictions are filesystem (read-only source and inputs; writable
per-run workspace), network (inbound/outbound/loopback denied), UI (isolated
desktop, clipboard/input/system-control restrictions), and least privilege.
Script-package immutability remains mandatory whenever MXC is enabled, even if
the optional broad filesystem restriction is disabled. Disabling MXC removes that
baseline. MXC is an early preview, not a guaranteed security boundary.

Every launch is non-elevated. An elevated Host must de-elevate through the
interactive shell token; inability to do so fails closed with exit 126.
There is no fallback from requested isolation to direct or elevated execution.

The Host privately bundles verified, architecture-matched MXC 0.8.0 and WSL
Containers SDK 3.0.3 payloads, licenses, and notices via `Mxc.Runtime.targets`.
The Windows pin preserves this prototype's least-privilege compatibility.
Updating it requires execution checks, not just a probe. No administrator
host-preparation tool or host-DACL mutation is enabled.
`mxc-support --json` reports availability; missing payloads require repair.

### Python environments

Global Python mode selects Disabled, Windows, Existing WSL, or Managed Linux.
It never affects PowerShell or explicit recipes and does not approve scripts.
Disabled Python is hidden from normal discovery and Settings counts; management
can use `--include-disabled`. Re-enabling preserves identities and filters.

Existing WSL uses the selected user distribution and its Python/packages.
Windows process containers cannot host `wsl.exe`: the implicit platform default
runs WSL directly with a warning; explicitly required MXC refuses the launch.
Windows input/file-parameter paths are translated in a separate Linux payload;
returned mounted-drive paths are translated back before output validation.
For file-output scripts, the Host supplies a Windows-backed `TMPDIR`, so Python
temporary files remain accessible to Windows consumers after WSL execution.

Managed Linux uses a separate `python:3.12-alpine` image and MXC WSLC, not the user's
distribution. **Prepare environment** (`linux-container-setup`) explicitly downloads
the image; **Check status** (`linux-container-status`) only probes. Neither changes
trust or accepts risk. WSL 3.0.3 has been verified; setup never upgrades WSL.

Managed Linux requires global MXC and filesystem protection. Unsupported Windows
UI/least-privilege policies require acknowledged global exceptions; Python runs
as guest root, not Windows administrator. Networking is globally blocked or allowed.
Only the package, request, selected files, and declared file parameters are
projected read-only; Windows writes are limited to the run workspace.
Return existing workspace files created with `tempfile`/`TMPDIR`; symlinks,
traversal escapes, Linux-only paths, and outside-workspace output are refused.
Unavailable preparation or incompatible policy never switches environments.
The bundled native WSLC runtime can reject non-ASCII projected input paths before
Python starts; such invocations fail explicitly.

## Build and integrate

Use `tools\build\build.ps1 -Platform x64 -Configuration Debug` with `-Path` pointing
to the Host or Core.Tests project. Build tests first, then use Visual Studio Test
Explorer or `vstest.console.exe`, not `dotnet test`.

```text
PowerScripts.Host.exe list --json [--consumer <id>] [--input <shape>] [--output <shape>]
PowerScripts.Host.exe run <id> [--files <paths...>] [--set name=value...]
PowerScripts.Host.exe transform <id>                 (JSON stdin -> JSON stdout)
PowerScripts.Host.exe trust approve|revoke <id>
```

See [consumer integration](CONSUMER-INTEGRATION.md), [API design](DESIGN.md), and
[security design](SECURITY-DESIGN.md). Core/Host tests cover schema constraints,
typed parameters, templates, discovery, trust, output validation, isolation,
and real CLI execution. The client is the managed integration boundary; native
consumers use the same process/JSON API. No MCP server is required to author scripts.
