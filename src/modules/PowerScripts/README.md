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

Open **Settings > PowerScripts > Open PowerScripts editor** to manage PowerScripts in a
standalone WinUI 3 window. **My scripts** contains the library, creation flow, descriptor details,
and per-script review. **Activity** contains attributed execution history, refresh, and safe history
clearing. PowerToys Settings owns enablement, the scripts folder, Windows/Linux runtime and sandbox
controls, integration switches, and AI agent skills. The editor shows effective configuration and
links back to Settings; it refreshes configuration when reactivated.
The editor uses the existing Host and storage; moving the UI does not migrate scripts, trust, or bindings.

Integration switches for Advanced Paste and Keyboard Manager live in **Settings > PowerScripts > Use PowerScripts in**.
The shared Host enforces them for discovery and execution, retaining existing bindings when disabled.
Advanced Paste's legacy preference is preserved; Keyboard Manager defaults on. Group Policy can
manage the module and each integration without replacing saved preferences. A disabled PowerScripts
module or a policy-disabled parent utility always wins over a consumer enable policy.
See [central settings and policy precedence](CONSUMER-INTEGRATION.md#central-integration-settings-and-group-policy).

## AI agent skills

**Settings > PowerScripts > AI agent skills** installs the bundled
[`powertoys-powerscripts` skill](AgentSkills/powertoys-powerscripts/SKILL.md) into a selected local
agent's personal skill directory:

| Agent | Destination |
|---|---|
| Copilot CLI / VS Code Copilot | `%USERPROFILE%\.copilot\skills\powertoys-powerscripts` |
| Claude Code | `%USERPROFILE%\.claude\skills\powertoys-powerscripts` |
| Codex | `%USERPROFILE%\.agents\skills\powertoys-powerscripts` |
| Custom | `powertoys-powerscripts` inside a user-selected skills folder |

Install the agent separately. This installs the complete instruction/reference bundle, not an MCP
server, an execution permission, a trust approval, or a hotkey binding. Remote, cloud and WSL agents
need separate setup. The portable skill uses existing Host commands to scaffold and validate scripts
after planning the consumer, input/output contract, typed parameters, runtime/environment,
dependencies and failure behavior. It documents Advanced Paste, Keyboard Manager and conditional
File Explorer integration capabilities; a profile does not mean that integration is installed.
Python guidance distinguishes Windows, the user's WSL distribution, and managed Linux containers,
including environment-specific APIs, dependencies and paths. Keyboard Manager instructions are
optional hotkey handoff guidance, not a separate skill or a prerequisite for other consumers.
No KBM configuration API or direct JSON editing is introduced. It cannot promise host VPN changes
work under sandboxing. Integration contributors must update the
[consumer profiles](AgentSkills/powertoys-powerscripts/references/consumers.md) and bundle version
when adding/changing adapters.

Choose one of the three agents or **Custom**, which opens a folder picker. Select the agent's
skills parent folder; PowerToys creates `powertoys-powerscripts` inside it. Custom selections are
remembered, and **Choose folder** is available only for Custom. The destination and installed/bundled
versions are visible before installing/updating. A single action shows **Install** when absent,
**Update** for an intact managed copy with another bundled version, or a disabled **Update** when
already current. Modified/unmanaged copies also disable Update with an explanation, never overwrite.
**Keep this skill updated when PowerScripts Settings opens** is opt-in. PowerToys records managed
copies and per-file hashes in the separate `PowerScripts\agent-skills.json` state file. When the page
opens, intact opted-in copies are synchronized with the bundle shipped by the installed PowerToys
release. User changes (including added or missing files) pause replacement; unknown existing folders
are never overwritten. Install to a different Custom folder to review a separate bundled copy. Source and
destination reparse paths are rejected; concurrent management is serialized. Failures are reported.
The installer only packages the bundle; the per-user Settings process manages personal copies.
Custom copies use the same ownership checks and optional updates as named-agent copies. Switching
folders preserves each managed copy and its update choice; selecting a named agent's folder through
Custom uses the existing record. Older exported/manual copies remain unmanaged and are not adopted.
Start a fresh session after changing skills, or use Copilot CLI's
`/skills reload` and `/skills info powertoys-powerscripts`. Discovery does not guarantee selection;
explicitly request the skill when needed, for example "Use the powertoys-powerscripts skill to create
a script for this task." Existing conversations may retain old instructions.

When changing the shipped skill, update `AgentSkills\powertoys-powerscripts\bundle.json`'s version
in the same change. The Host build/publish includes the bundle under `AgentSkill`, covered by the
existing recursive PowerScripts installer payload. Settings builds also copy it locally for
development. No consumer discovery/execution JSON contracts or script/trust/binding formats change.

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
maximum file counts. The same generator is ready for a future sentence-based
creation UI: input maps to `input`, questions to the `parameters` list (empty
means "ask nothing"), result to `output`, and runtime to `runtime`.
The UI itself is unchanged.
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
Every selection generates a runnable example and a README explaining its task:

| Input | No result | Text result | File result | Image result | Audio result |
|---|---|---|---|---|---|
| Nothing | Greeting diagnostic | Greeting | UTF-8 greeting file | Greeting fingerprint PNG | Greeting tone WAV |
| Text | Inspect text | Uppercase text | UTF-8 text file | Text fingerprint PNG | Text-derived tone WAV |
| Files | Inspect selection | Name/size report | UTF-8 name/size report | Selection-metadata fingerprint PNG | Selection-metadata tone WAV |
| Image | Inspect MIME/size/digest | MIME/size/SHA-256 report | Export original image | Unchanged image | Image-digest tone WAV |
| Audio | Inspect MIME/size/digest | MIME/size/SHA-256 report | Export original audio | Audio-digest fingerprint PNG | Unchanged audio |

This matrix works in both runtimes, with or without typed questions: 100
representative combinations. Python uses only its standard library; PowerShell
uses built-in Windows/.NET APIs. Visual fingerprints are 64 x 64 PNGs and tones
are quiet, 0.25-second PCM WAVs. These are input-driven diagnostic examples,
**not** OCR, transcription, speech synthesis, or transcoding.
No-result examples perform input inspection and write diagnostics to stderr,
not stdout or a persistent log. They do not modify selected files.
Parameters are read as typed values and included in reports, fingerprints,
tones and stderr. Media round trips/exports preserve the original bytes and
demonstrate parameters in stderr only. Arbitrary names do not invent behavior:
authors replace the marked task with their own logic. Omitted optional answers
stay absent, while false, zero and empty-string defaults retain their meaning.
Generated output files use the temporary workspace, never the source package.

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
**Limit process privileges** are marked **Windows only**. Managed Linux ignores
these two restrictions; they can remain enabled to protect Windows scripts.

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
the image, then checks Python. **Check Python** (`linux-container-status`) checks
`python3` in the matching, network-blocked warm session without resetting it.
If no session exists, it uses a temporary container with the cached image;
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
automatically retried. Runs are serialized across Host processes. Image setup
also releases the shared container before using the image store.
Readiness checks never tear down an existing session. An interrupted session,
different image/network policy, or lost WSL backend connection is reported separately
from image availability.

**Restart Linux runtime** (`linux-container-recover`) provides explicit recovery.
It acquires the PowerScripts session lock without waiting and admits a constant SDK-worker
check before cleanup; busy operations, timeouts, and unknown failures block recovery.
If SDK cleanup fails with RPC server unavailable (`0x800706BA`), recovery reports that
Windows needs to be restarted; it never forcibly terminates a daemon. Forced termination
can leave the SDK's image VHD attached even after the process exits. Failed session
metadata is retained for cleanup, not silently discarded. Daemon discovery is scoped
to the settings store's `linux-session\daemon` directory. Other apps' daemons and user
WSL distributions are not restarted. Shared image-store contention remains an explicit error:
close other MXC Linux apps and retry; if contention persists, save work and restart Windows.
Cached images, settings, trust approvals, and returned files are kept; container changes
are discarded, and no user script is replayed. Older shared-daemon sessions are cleaned
up by sandbox ID when possible, but a disconnected legacy daemon is never forcibly
stopped: save work and restart Windows, then check Python again.
The initial cause of an SDK RPC disconnection is not yet established.
The warm container intentionally survives Host exits; the pinned daemon's idle timeout
only applies after its last container has been released, not while a warm container exists.
PowerScripts requests prompt daemon teardown after that release so setup/checks can reopen
the SDK's exclusively held image store; these constant image operations wait briefly for it,
without retrying any user script.
The pinned daemon uses MXC's default `%TEMP%\mxc-wslc-sessions` image cache; images
prepared by older prototype builds may need **Download image** again. Script execution
never downloads an image automatically.

Managed Linux requires global MXC and filesystem protection. Unsupported Windows
UI/least-privilege policies do not apply to Linux and do not block its execution.
Python runs as guest root, not Windows administrator. Networking is globally blocked or allowed.
Only the package, request, selected files, and declared file parameters are
projected read-only; Windows writes are limited to the run workspace.
Return existing workspace files created with `tempfile`/`TMPDIR`; symlinks,
traversal escapes, Linux-only paths, and outside-workspace output are refused.
Unavailable preparation or incompatible policy never switches environments.
The bundled native WSLC runtime can reject non-ASCII projected input paths before
Python starts; such invocations fail explicitly.

## Activity

PowerScripts editor > Activity shows the latest 200 invocations, including the calling module,
script, start time, status and duration. History stays on this device and never stores input,
parameters or output. Clear history preserves running invocations. Interrupted Hosts (including
consumer cancellation) are identified without inventing a completion time or reason.

Every integration must identify itself using the client constructor or `--consumer`; this is
self-reported attribution, never authorization. See the [consumer contract](CONSUMER-INTEGRATION.md#caller-identity-and-activity)
for identity rules, native-call migration, privacy, retention and lifecycle semantics.

## Build and integrate

Use `tools\build\build.ps1 -Platform x64 -Configuration Debug` with `-Path` pointing
to the Host or Core.Tests project. Build tests first, then use Visual Studio Test
Explorer or `vstest.console.exe`, not `dotnet test`.

```text
PowerScripts.Host.exe list --json [--input <shape>] [--output <shape>]
PowerScripts.Host.exe run <id> --consumer command-line [--files <paths...>] [--set name=value...]
PowerScripts.Host.exe transform <id> --consumer command-line (JSON stdin -> JSON stdout)
PowerScripts.Host.exe activity list
PowerScripts.Host.exe activity clear
PowerScripts.Host.exe trust approve|revoke <id>
```

See [consumer integration](CONSUMER-INTEGRATION.md), [API design](DESIGN.md), and
[security design](SECURITY-DESIGN.md). Core/Host tests cover schema constraints,
typed parameters, templates, discovery, trust, output validation, isolation,
and real CLI execution. The client is the managed integration boundary; native
consumers use the same process/JSON API. No MCP server is required to author scripts.
