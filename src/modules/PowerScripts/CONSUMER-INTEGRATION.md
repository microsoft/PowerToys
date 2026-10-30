# Integrating a PowerToys module with PowerScripts

This document is the contract for adding a PowerScripts consumer without coupling the module to the
PowerScripts execution engine. A consumer owns discovery filtering and user experience; the shared
Host owns script loading, validation, trust, parameter validation, security policy, and execution.

## Integration rules

1. **Always execute through `PowerScripts.Host.exe`.** Do not launch a script, interpreter, or
   descriptor command directly from the consumer.
2. **Discover by resolved I/O.** A consumer selects scripts whose `io.input` and `io.output`
   it can supply and handle. The catalogue is shared: there is no consumer ID, audience field,
   or module-targeting filter. Scripts do not declare the removed `surface` or `kind` fields.
3. **Treat script IDs as opaque.** Display `name` and `description`; invoke by `id` and persist
   IDs only when the consumer needs saved bindings.
4. **Keep parameter UX in the consumer.** The Host never opens a generic parameter prompt.
5. **Do not bypass trust or MXC.** Both interactive and automatic invocations use the Host so they
   receive the same content-hash trust gate and effective security policy.
6. **Handle PowerScripts being unavailable or disabled.** Hide or disable runtime integration
   rather than crashing. Configuration UIs should show actionable discovery errors, not disguise
   a failed Host as a successful empty catalogue or erase saved bindings.

## Choose the consumer contract

| Consumer behavior | Discovery filter | Invocation |
| --- | --- | --- |
| Trigger an action with no primary input | `input: none` | `run <id>` |
| Operate on selected filesystem items | `input: files`, then validate extensions and count | `run <id> --files <paths>` |
| Transform structured content | Compatible input and output shapes | `transform <id>` with JSON on stdin |
| Browse scripts available to run | Any supported I/O | `list --json` |
| Configure installed/unavailable scripts, including disabled runtimes | No consumer filter | `list --json --include-disabled --include-unconfigured` |

Input and output tokens are:

```text
none, text, files, image, audio
```

`text` carries strings, including inline HTML. `files` carries one or more paths, including images,
audio, video, and HTML documents. File eligibility is determined by extensions and selection count,
not whether the file came from Explorer, a file picker, or the clipboard.

`text` output is always literal content, including literal HTML markup. Rich HTML documents and
file-backed media use `files`; a single file still uses a path list. Inline `image` and `audio`
use MIME/base64 objects or schema-declared arrays; see the [media contract](README.md#inline-image-and-audio).
Consumers decide whether to paste file contents
(for example, a PNG bitmap or rich HTML document) or a file selection. Do not guess HTML from a
string's contents or treat a filename-looking string as a returned file.

## Prefer the C# client

The PowerToys installer deploys the shared, self-contained Host and its private, pinned MXC runtime
under the installation's `PowerScripts` directory. Consumers must not download MXC, expose an
executor-path setting, or perform privileged host preparation. Local Host builds restore the same
architecture-matched dependency automatically; keep the complete build/publish output together
(including `wxc-exec.exe`, `wxc-wslc-daemon.exe`, `wslcsdk.dll`, `plm.exe`, licenses, and SPDX manifest). A missing dependency is an
installation/build failure, not permission to bypass isolation. MXC is preview software, not a
security boundary; see the README for platform limits and the compatibility pin.

.NET consumers should reference `PowerScripts.Client` and use `PowerScriptsClient`. The client
encapsulates Host discovery, safe argument construction, process invocation, JSON parsing, and
timeouts while keeping the consumer decoupled from `PowerScripts.Core`.

```csharp
var client = new PowerScriptsClient();
if (!client.IsAvailable)
{
    return;
}

var scripts = await client.ListAsync(cancellationToken);
var actions = scripts.Where(script => script.Input == PowerScriptIO.None);
```

Useful discovery methods:

- `List()` — installed scripts whose runtimes are enabled.
- `ListActions()` — scripts with `input: none`.
- `ListAccepting(input)` — scripts accepting one I/O shape.
- `ListProducing(output)` — scripts producing one I/O shape.
- `ListForFileExtension(extension)` — file scripts matching one extension.
- `ListForFiles(paths)` — file scripts accepting every selected extension and the selection count.
- `script.AcceptsFiles(paths)` — the same check against an already-enumerated script.

The catalogue is shared; scripts do not target specific modules. Each consumer owns its script
matching and any optional binding configuration. All invocations retain the
same Host trust and MXC enforcement regardless of the consumer.

Managed Linux container reuse is owned by the Host and its MXC daemon, not by consumers.
Send original Windows file paths; the Host stages selected inputs and maps workspace outputs
back to Windows. Do not assume a fresh guest filesystem or that different scripts/consumers
are isolated from each other. Per-invocation trust, validation, and global policy still apply;
there is no new client API or consumer-managed container lifecycle.

Native consumers may invoke the Host contract directly:

```text
PowerScripts.Host.exe list --json [--input <shape>] [--output <shape>] [--no-input]
PowerScripts.Host.exe run <id> [--files <paths...>] [--set name=value...]
PowerScripts.Host.exe transform <id>
```

Use structured process argument APIs where available. Do not concatenate untrusted IDs, paths, or
parameter values into a command line.

Default `list` discovery (JSON or text, with or without I/O filters) omits Python scripts when
Settings has Python execution disabled. Windows and WSL modes both enable Python discovery. Consumers should re-enumerate
when presenting available scripts rather than caching runtime availability indefinitely.

Only configuration/catalogue UIs should opt into `--include-disabled`, so users can still inspect
installed Python scripts before enabling Python. This option changes discovery only; it does not
enable a runtime or bypass execution checks.

## Automatic discovery or saved bindings

**PowerScripts owns the catalogue; consumers own compatibility and presentation.** Prefer
automatic discovery when a consumer can offer every applicable script. Saved assignments and a
configuration picker are only necessary for consumers that need explicit bindings.

Advanced Paste is the automatic-discovery example. Its Settings page has only a PowerScripts
master toggle, off by default. When enabled, the paste window offers every available script with
compatible clipboard input and a pasteable output. Text, files, bitmap images, and audio are
matched against the declared contracts, including file/media restrictions. No-input scripts are
excluded. AP stores no per-script selection or exclusion list.

See `PowerScriptsService.GetApplicableScripts` and `OptionsViewModel` in Advanced Paste for
the filtering and refresh flow. Filter before projecting action rows, rather than displaying
incompatible scripts as disabled. Refresh both the visible actions and their count when the
clipboard, catalogue, or master toggle changes. Parameters remain inside AP's own window;
execution still validates availability, contracts, global policy, and trust through the Host.

### Assign one script to a trigger

A shortcut-based consumer owns its binding UI. Discover compatible actions through the client;
for example, an automatic trigger that cannot collect parameters or handle output can offer:

```csharp
var scripts = await client.ListAsync(cancellationToken);
var actions = scripts.Where(script => script.Input == PowerScriptIO.None
    && script.Output == PowerScriptIO.None
    && !script.Parameters.Any(parameter => parameter.IsRequired && parameter.Default is null));
```

Adapt the predicate to the consumer's capabilities. Persist only the chosen ID and any
consumer-collected parameter values, using the module's normal IPC/save path. Review trust during
configuration; automatic execution must refuse an untrusted or changed script rather than silently
approving it. Revalidate parameters and contracts through the Host when invoking.

Do not duplicate the installed catalogue or turn a local Remove action into a global uninstall.
Keep an unavailable binding with an explicit warning until the user replaces or removes it; Cancel
and discovery errors must not overwrite saved choices. Discovery needs a bounded timeout and
cancellation, with failures shown explicitly.

This version has no shared configuration picker or standalone script launcher. Consumers should
not add a mandatory execution popup: Keyboard Manager needs no selector at shortcut time, while AP
collects parameters inside its own window. Native/WPF consumers may use the Host contract directly;
do not reference the Settings executable from a runtime module or add UI dependencies to the client.

### New-consumer implementation checklist

1. Define supported input/output shapes, file/media bounds, and parameter collection behavior.
2. Reference `PowerScripts.Client` for execution; do not reference Core or launch interpreters.
3. Choose automatic discovery for all applicable scripts, or consumer-owned saved bindings
   when explicit assignment is needed. Do not require per-script configuration by default.
4. For automatic discovery, filter against current input before presenting actions and refresh on
   input/settings changes. For saved bindings, implement consumer-owned assignment UI using
   the shared Host/client discovery contract.
5. Present script names and relevant contract/parameter information. If maintaining bindings,
   retain missing/invalid/incompatible entries with a warning and an explicit Remove action.
6. Persist consumer settings through the module's normal IPC/save path. For picker-based bindings,
   Cancel and discovery errors must not modify settings.
7. Revalidate availability, current contract, parameters, and trust when invoking through the Host.
8. Test input filtering, input changes, master enablement, runtime-disabled behavior, and trust
   invalidation. For saved bindings, also test search-preserved selection, cancellation, stale IDs,
   and persistence. Do not introduce a runtime picker unless that consumer needs one.

## Script presentation and icons

Scripts are discovered from sibling `<script>.tool.json` descriptors. Bare helper files are never
actions. Descriptor schemas are authoritative; there are no per-script settings or policy records.
Runtime is inferred from `.ps1`/`.py`. Inline metadata and Python function-name inference are not
supported. Built-in Python executes top-level statements and reads the schema-shaped request from
JSON stdin. An explicit `x-execute` recipe uses its declared command instead of the built-in runtime.

Present scripts by name and resolved input/output contract, not a separate "System" or "File"
category. Put the runtime first, for example `PowerShell · None → Text` or
`Python · .mp3, .wav → Text`. For `files` input, use accepted extensions where specified and
`All files` for `*`; keep the API's `io.input` value as `files`. `none` refers to primary input and
does not imply that there are no named parameters to configure.

Authors may set `"x-powerscript": { "icon": "assets\\script.ico" }` in the descriptor.
The optional icon is a package-relative ICO,
limited to 1 MiB. Absolute paths, URLs, paths escaping the package, and symbolic links/junctions
are rejected. A missing or invalid optional icon does not remove the script: the Host reports
`iconPath: null` and an `iconWarning`.

`list --json` and `PowerScriptInfo.IconPath` expose the validated absolute path. Display it when
appropriate, use a runtime-specific default script icon when absent or undecodable, and do not execute or fetch icon
metadata. Consumers must tolerate missing icon fields from older Hosts. The prototype Explorer
handler and `shell-menu` feed have been removed. Any future file context menu belongs to
File Explorer Add-ons and should consume the shared JSON/client discovery contract.

## Parameters

The catalogue exposes `string`, `int`, `bool`, `choice`, and `file` parameters, including required
state, labels, descriptions, defaults, choices, and numeric bounds.

Each consumer decides when and how values are collected:

- An interactive command may render controls immediately before execution.
- A configurable trigger may collect values once and persist them with its own assignment.
- A consumer that has no parameter UX should expose only scripts whose required values have
  defaults.

Pass values to `PowerScriptsClient.Run` or as repeated Host `--set name=value` arguments. When a
native consumer must persist arbitrary values in a command line, use the Host's `--set-base64`
transport: Base64-encode UTF-8 `name + NUL + value` and pass one encoded argument per value.

A scalar `file` parameter is consumer-selected auxiliary input. It does not make the script a
primary `files` script and does not make it eligible for Explorer discovery.

Consumers always supply Windows paths for file-picker parameters, even when Python uses WSL.
After validation, the WSL runtime translates declared `file` parameters into `/mnt/<drive>/...`
paths in a separate payload; ordinary strings and the original Windows security context are
unchanged. This uses standard WSL drive mounts. UNC/device paths are explicitly unsupported for
scalar file parameters in WSL.

## Trust and automatic invocation

An interactive consumer normally lets the Host request consent when untrusted content is run.
Automatic consumers must never trigger consent or parameter UI during the event they handle:

1. Approve the selected script during configuration with `TrustApprove`.
2. Persist its ID and any configured parameter values.
3. Invoke it with `noConsent: true`.
4. If the script changes, its content hash invalidates trust and the automatic run safely fails
   until the user approves the new content.

Do not interpret `noConsent` as a trust bypass. It means "fail instead of prompting."

## File-oriented consumers

For a multi-file selection, a script is compatible only when:

- every selected extension matches the declared extensions or `*`;
- the selection count is at least `minFiles`;
- the selection count does not exceed schema `maxItems` (derived `maxFiles`), when present.

An omitted/null maximum is unbounded; zero is a genuine zero maximum.

Pass the complete selection in one invocation. Do not invoke the script once per file unless that is
an explicit consumer behavior.

Selected files are read-only under the default MXC filesystem policy. A consumer must not assume
that declaring `fileWrite` makes the originals writable. Operations that mutate, move, rename, or
delete original items need a separately reviewed security and UX design.

## Structured transforms

Use `TransformAsync` when the consumer supplies content rather than an action or Explorer selection.
Populate the field matching the script's input contract and consume the matching output field:

```csharp
var result = await client.TransformAsync(
    script.Id,
    new PowerScriptTransformInput { Text = sourceText },
    cancellationToken: cancellationToken);

if (result.Succeeded)
{
    UseResult(result.Text);
}
```

The input transfer model contains `text`, `files`, `image`, `audio`, and `parameters`. Inline HTML goes in `Text`,
not a separate `Html` field. PowerShell text transforms receive `-Text`; Python text transforms
read `request["text"]` from JSON stdin. File transforms receive `$Files` and
`POWERSCRIPTS_FILES` in PowerShell, or `request["files"]` in Python.
Named options are declared under `inputSchema.properties.parameters`.
Use `PowerScriptTransformInput.Parameters` with `JsonElement` values to retain JSON integer
and boolean types; numeric/boolean strings are rejected. The CLI `--set` transport remains strings.

For a clipboard bitmap, first encode it as an actual PNG temporary file, then use exactly the same
file discovery and execution contract as Explorer:

```csharp
var files = new[] { clipboardPngPath };
var compatibleScripts = client.ListForFiles(files);

// After the user selects a compatible script:
var result = await client.TransformAsync(
    selectedScript.Id,
    new PowerScriptTransformInput { FilePaths = files },
    cancellationToken: cancellationToken);
```

A script whose input schema declares a required `files` array, `.png` item extension hints,
and a count range including one is
eligible regardless of the PNG's source. Batch-only scripts are not offered for a single clipboard
image. Extension matching is not content validation: scripts must handle invalid file contents.
Keep consumer-created temporary files available until execution completes and clean up only those
temporary files, not user-owned clipboard files.

Transform input JSON uses UTF-8, independently of the Windows console code page. Unicode text and
file paths can be sent literally or as JSON Unicode escapes.

Output JSON is `{}` for `none`, `{"text":"..."}` for `text`, or `{"files":["..."]}` for `files`.
Inline media returns `{"image":...}` or `{"audio":...}` with the scalar/array shape declared in the
output schema. Consumers must decode only supported MIME types and enforce their own rendering limits.
Both schemas are required closed object envelopes. File paths should be absolute and accessible to
the Windows consumer; WSL scripts should produce files on shared mounted drives so returned
`/mnt/<drive>/...` paths can be translated back to Windows.

Python, PowerShell and explicit launch recipes emit literal stdout for text output, or the explicit JSON
object for file output. For example, `@{ files = @($producedPaths) } | ConvertTo-Json -Compress`.
Stdout uses UTF-8. PowerShell authors should set
`[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)` before producing output, particularly
on Windows PowerShell 5.1, so Unicode paths survive the pipe.
For no output, transform stdout is forwarded as diagnostics and the JSON result is `{}`.
File-output stdout must contain only the result object; use stderr for diagnostics.
Malformed JSON, removed fields, and output shapes inconsistent with the descriptor fail explicitly.

Output file-item extension hints can constrain results, but are not content validation. Inspect
actual results before decoding or publishing them, and do not offer a restricted consumer workflow
based solely on that shape. Retain returned files for as long as the consumer needs them, including
deferred clipboard reads. If a returned path reuses a temporary input, transfer its lifetime before
input cleanup. Delete only consumer-owned files, never arbitrary returned user files.
This is the core integration contract, not an automatic change to a consumer's clipboard UI.

This is a breaking profile: legacy root I/O/runtime/function/policy metadata and old payload
fields are rejected, not migrated or inferred. Schema-local extensions refine path strings;
`minItems`/`maxItems` constrain arrays. Descriptor changes require renewed trust.

## Consumer responsibilities

- Keep Host calls off the UI thread.
- Apply a bounded timeout and support cancellation for interactive work.
- Render Host failure and trust-invalidated states as failures, not successful empty results.
- Persist only IDs and consumer-owned configuration, never script bodies or executable paths.
- Re-enumerate scripts when opening configuration UI so removed or changed scripts are reflected.
- Use localized strings and accessible controls following that module's existing conventions.
- Do not add logging in hooks or other hot paths.

## Validation checklist

- The consumer shows only scripts matching its actual input/output capabilities.
- The shared picker is reused for Settings selection; the consumer's default page shows configured
  bindings with input/output metadata, not a duplicate full catalogue.
- Cancel, discovery failure, and search filtering do not erase assignments or change global settings.
- Missing/incompatible saved IDs remain visible with an explicit Remove action.
- Host unavailable, disabled, timeout, malformed output, and non-zero exit paths are handled.
- Required/default/invalid parameter cases are covered.
- Trust approval and trust invalidation are covered for automatic consumers.
- File selection tests cover mixed extensions plus minimum and maximum counts.
- The consumer invokes only `PowerScripts.Host.exe`, never script content directly.
- Existing behavior remains unchanged when PowerScripts is disabled or no compatible scripts exist.
