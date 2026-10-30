# PowerScripts (prototype)

Write a script once and expose it to compatible PowerToys modules through
`PowerScripts.Host.exe`. PowerScripts supplies discovery, validation, trust, and
execution, not a standalone product launcher. Consumer integrations are developed
separately; a future Explorer surface belongs to File Explorer Add-ons.

Advanced Paste has one PowerScripts master toggle. When enabled, its paste window automatically
shows all scripts compatible with the current clipboard, such as text-input scripts for copied
text. There is no AP-specific script configuration or saved selection list. No-input scripts
are excluded, and parameters are collected inside the paste window when needed.
Consumers that need saved assignments own their binding UI and persist script IDs, not catalogue
copies. There is no standalone script launcher or per-consumer configuration picker in this version.
See the [consumer onboarding guide](CONSUMER-INTEGRATION.md#automatic-discovery-or-saved-bindings).

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
one required primary field: `text` (string), `files` (array of path strings),
`image`, or `audio` (inline media objects or explicitly declared arrays).
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

### Inline image and audio

Media objects require `mimeType` and `data`. The MIME field is a string enum
listing formats the script accepts; `data` is a string with
`contentEncoding: "base64"`. Both input and output use the same representation:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "image": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "mimeType": { "type": "string", "enum": ["image/png", "image/jpeg"] },
        "data": { "type": "string", "contentEncoding": "base64", "minLength": 1 }
      },
      "required": ["mimeType", "data"]
    }
  },
  "required": ["image"]
}
```

The corresponding request/result is
`{"image":{"mimeType":"image/png","data":"<base64 PNG bytes>"}}`.
Audio uses the primary field `audio` and an audio MIME enum.
Optional `contentMediaType` on `data` is supported when the enum names exactly
that one MIME type. These are JSON Schema objects/strings, not new primitive types.

| Category | Supported MIME types |
| --- | --- |
| Image | `image/png`, `image/jpeg`, `image/webp` (static images only) |
| Audio | `audio/wav`, `audio/mpeg` (MP3), `audio/flac` |

For collections, wrap the media item schema in
`{"type":"array","minItems":1,"maxItems":2,"items":{...}}` under the same primary
field. Array shape is preserved even for one item. Omitted `maxItems` permits up
to the global 64-item limit. Each item may use any of the declared MIME types.
Limits apply separately to input and output: **16 MiB decoded per item, 32 MiB
decoded total, at most 64 items**, with a bounded JSON media envelope.
Base64 must be nonempty and contain no whitespace. The Host checks the MIME enum,
base64, format headers, and PNG/WebP animation markers; it does not fully decode
codecs or guarantee that arbitrary media is safe to render. Scripts/consumers
must handle decoding errors. No transcoding or codec installation occurs.
Animated images, SVG, multipage images, video, and large assets remain file-based.
For video scripts, use `files` with `.mp4`, `.webm`, `.mov`, or `.mkv` filters.
There is no document category or streaming transport.

Consumers normalize their source: a clipboard bitmap, capture, or selected image
file can supply image bytes. A file path is not a valid `image.data` value.
Consumer adapters own reading/encoding and any explicit format conversion.
The shared .NET client accepts raw bytes through `PowerScriptMediaItem` and
`PowerScriptMedia`; it base64-encodes only at the JSON boundary.
`PowerScriptInfo.InputSchema`/`OutputSchema` expose MIME choices and scalar/array
shape. Integrations choose compatible contracts; adding media to the shared
engine does not automatically add media pickers or clipboard handling to them.

This is not a complete JSON Schema implementation. Supported node types are
object, array, string, integer, and boolean; arbitrary nested option objects,
number/null types, unions, references, and schema composition are not supported.
Unsupported schema keywords and formats fail explicitly instead of being
silently treated as enforced. Regular-expression matching has a bounded timeout.
Defaults retain JSON types. Missing parameter bags are treated as empty bags,
so declared defaults and required options apply consistently.

Root `x-powerscript` is optional and contains only `capabilities`,
`publisher`, `version`, `source`, and `icon`. Capabilities describe intent, not
permissions. Each consumer module owns script selection and applicability.
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
Input is one UTF-8 JSON object with the declared `text`, `files`, `image`, or `audio`, plus typed
`parameters`. Python receives real booleans and integers.

PowerShell uses ordinary `param(...)`: `$Text`, `$Files`, `$Image`, or `$Audio` for primary data,
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
| Required `image` or `audio` | A JSON object with the declared media primary | The validated object |

These rules are identical for Python, PowerShell, and explicit recipes. Text is
never guessed to be a file path. For file output, stdout must contain only the
result JSON; send diagnostics to stderr. A single file remains an array.
Use temporary workspace files for output, not the installed package.
Consumers own presentation and result lifetime, including deferred clipboard reads.
Inline images/audio use media objects; video and large assets use files.
Inline markup is text.

Optional `x-execute` is an advanced escape hatch for another interpreter or language.
`command` names the executable and fixed arguments; a sibling script token is
resolved to its absolute path. With `stdin: "json"`, the whole schema-shaped
request goes to stdin. Inline media requires this JSON transport.
Otherwise, named options use `argMap` or `--name`,
primary text uses the mapped `text` flag or `--text`, and files are positional.
`exitCodes` supplies nonzero-exit descriptions. Explicit recipes bypass built-in
Python environment selection, not shared trust or isolation.

## Settings and templates

**Create Script** generates editable packages for all 50 runtime/input/output
combinations. Creation never runs code, grants trust, or changes isolation.
Add named parameters with string, integer, boolean, or file-path types, display
names/descriptions, required flags and typed defaults. Strings optionally declare
enum choices (one per line); integers support optional minimum/maximum bounds;
file parameters support extension filters. Parameters become the closed
`inputSchema.properties.parameters` object and matching PowerShell arguments;
Python accesses them through `parameters`. Defaults remain descriptor data.
Names must start with a letter, contain only letters/digits/underscores, be unique
ignoring case, and avoid PowerShell automatic variables and the active primary name.
For CLI creation, add `parameters` entries such as
`{"name":"count","schema":{"type":"integer","minimum":1,"default":2},"required":true}`
to the configuration request. File-path entries can also specify `fileExtensions`.
The creation page starts blank, without metadata presets or prefilled parameters.
Enter your own details and explicitly choose the runtime, input/output types,
and each parameter's name and type. For file input, also enter the minimum and
maximum file counts. Creation generates generic starter code, not specialized
conversion or formatting implementations.
Defaults are optional: a required parameter without a default must be supplied
at invocation, while an optional parameter without a default may be omitted.
An explicit default can be an empty string, zero, or false; it is not the same
as an omitted default. Auxiliary file paths normally come from a consumer picker.
The form's maximum-file value 0 means unlimited and generates an *omitted*
`maxItems`; it is not serialized as `maxItems: 0`.
The CLI equivalent is `template create --configure`, reading creation options
such as `{"projectName":"My script","runtime":"python","input":"files","output":"text"}`.
`template list` and `template create <id>` expose six educational templates,
including image/audio round trips and file-backed video inspection.
Media outputs pass through matching input; otherwise their starters produce a
one-pixel PNG or 0.1-second silent WAV. They do not perform a real conversion.

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

Settings presents isolation as **Run scripts in sandbox environment**, with a
parent On/Off switch and restriction children shown only when on. Turning it off
preserves the saved restriction choices. **Protect desktop interaction** and
**Limit process privileges** are marked **Windows only**. Managed Linux requires
explicitly accepted global exceptions for both; those exceptions also reduce
protection for Windows scripts.

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
workspace), network (inbound/outbound/loopback denied), UI (isolated
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

Global Python mode selects Disabled, Windows, or Linux (WSL).
It never affects PowerShell or explicit recipes and does not approve scripts.
Disabled Python is hidden from normal discovery and Settings counts; management
can use `--include-disabled`. Re-enabling preserves identities and filters.

Linux execution follows global MXC automatically: when enabled (including the
supported-machine default), use the managed WSLC container; when disabled, use
the selected user WSL distribution and its Python/packages. The distribution
picker is shown only when MXC is off. There is no separate container-mode setting.
Older `managedLinux` mode values load as `wsl` and save in that canonical form.
Explicit `wsl.exe` recipes cannot run inside Windows process containers and are
refused whenever MXC is enabled, including the machine-derived default.

For user-distribution execution with MXC off,
Windows input/file-parameter paths are translated in a separate Linux payload;
returned mounted-drive paths are translated back before output validation.
For file-output scripts, the Host supplies a Windows-backed `TMPDIR`, so Python
temporary files remain accessible to Windows consumers after WSL execution.

Managed Linux uses a user-provided container image and MXC WSLC, not the user's
distribution. Enter **Linux container image** in Settings, persisted as
`python.wsl.containerImage` in the module's `config.json`. There are no curated
Python versions, Linux-base presets, or automatic default image. An absent or empty
reference requires user configuration; it never selects another image as a fallback.
Existing saved references, including previously selected presets, are preserved.
Choose a trusted Linux image with `/bin/sh` and `python3` on the runtime PATH
(`/usr/local/bin:/usr/bin:/bin`). Use a tag for a chosen release or a digest to pin
the exact image; third-party dependencies must be included in the image or script
package. Changing this global setting invalidates the displayed readiness and does
not download anything or select a different image as a fallback.
**Download image** (`linux-container-setup`) explicitly downloads and caches
the image, then checks Python. **Check Python** (`linux-container-status`) starts
a temporary container using the cached image and checks that `python3` runs;
it never downloads an image or tests scripts and their dependencies. Neither changes
trust or accepts risk. Setup, checks, and script execution use the same configured
reference. Cached images are reused without automatic updates. Windows Python and
user-distribution WSL execution ignore this setting. WSL 3.0.3 has been verified;
setup never upgrades WSL.
PowerToys does not install Python or dependencies. Prepare the runtime image
before supplying its reference. Managed Linux scripts share one warm container across
Host invocations, using MXC's bundled `wxc-wslc-daemon.exe`. The configured catalogue is
mounted read-only; only selected input files are copied to read-only request staging.
Each invocation gets its own working directory and JSON request, but this is **not**
isolation between scripts: guest filesystem changes, background processes, and access
to other scripts' workspace files can persist across runs. Trust and schema checks
still run for every invocation. Use the supplied `TMPDIR` for file outputs.

The shared container is replaced when the catalogue, image reference, effective
network policy, or bundled runtime changes, and invalidated after a nonzero exit or timeout.
Expired daemon state is recovered before running user code; failed scripts are never
automatically retried. Runs are serialized across Host processes. Image setup and
Python checks also release the shared container before using the image store.
The warm container intentionally survives Host exits; the pinned daemon's idle timeout
only applies after its last container has been released, not while a warm container exists.
PowerScripts requests prompt daemon teardown after that release so setup/checks can reopen
the SDK's exclusively held image store; these constant image operations wait briefly for it,
without retrying any user script.
The pinned daemon uses MXC's default `%TEMP%\mxc-wslc-sessions` image cache; images
prepared by older prototype builds may need **Download image** again. Script execution
never downloads an image automatically.

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
PowerScripts.Host.exe list --json [--input <shape>] [--output <shape>]
PowerScripts.Host.exe run <id> [--files <paths...>] [--set name=value...]
PowerScripts.Host.exe transform <id>                 (JSON stdin -> JSON stdout)
PowerScripts.Host.exe trust approve|revoke <id>
```

See [consumer integration](CONSUMER-INTEGRATION.md), [API design](DESIGN.md), and
[security design](SECURITY-DESIGN.md). Core/Host tests cover schema constraints,
typed parameters, templates, discovery, trust, output validation, isolation,
and real CLI execution. The client is the managed integration boundary; native
consumers use the same process/JSON API. No MCP server is required to author scripts.
