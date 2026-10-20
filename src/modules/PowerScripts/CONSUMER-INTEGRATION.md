# Integrating a PowerToys module with PowerScripts

This document is the contract for adding a PowerScripts consumer without coupling the module to the
PowerScripts execution engine. A consumer owns discovery filtering and user experience; the shared
Host owns script loading, validation, trust, parameter validation, security policy, and execution.

## Integration rules

1. **Always execute through `PowerScripts.Host.exe`.** Do not launch a script, interpreter, or
   descriptor command directly from the consumer.
2. **Discover by resolved I/O, not module names.** Scripts do not declare `surface`, `kind`, or a
   list of PowerToys modules. A consumer selects scripts whose `io.input` and `io.output` it can
   supply and handle.
3. **Treat script IDs as opaque.** Display `name` and `description`; persist and invoke `id`.
4. **Keep parameter UX in the consumer.** The Host never opens a generic parameter prompt.
5. **Do not bypass trust or MXC.** Both interactive and automatic invocations use the Host so they
   receive the same content-hash trust gate and effective security policy.
6. **Handle PowerScripts being unavailable or disabled.** Hide or disable the integration rather
   than treating a missing Host or an empty catalogue as an application error.

## Choose the consumer contract

| Consumer behavior | Discovery filter | Invocation |
| --- | --- | --- |
| Trigger an action with no primary input | `input: none` | `run <id>` |
| Operate on selected filesystem items | `input: files`, then validate extensions and count | `run <id> --files <paths>` |
| Transform structured content | Compatible input and output shapes | `transform <id>` with JSON on stdin |
| Browse or configure scripts without executing | Any supported I/O | `list --json` |

The currently recognized I/O tokens are:

```text
none, text, html, image, audio, video, files
```

`files` means one or more filesystem paths. `image`, `audio`, and `video` are semantic transform
shapes and are not aliases for `.jpg`, `.mp3`, or `.mp4` file extensions.

## Prefer the C# client

The PowerToys installer deploys the shared, self-contained Host and its private, pinned MXC runtime
under the installation's `PowerScripts` directory. Consumers must not download MXC, expose an
executor-path setting, or perform privileged host preparation. Local Host builds restore the same
architecture-matched dependency automatically; keep the complete build/publish output together
(including `wxc-exec.exe`, `plm.exe`, the license, and SPDX manifest). A missing dependency is an
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

var scripts = client.ListActions();
```

Useful discovery methods:

- `List()` — all scripts.
- `ListActions()` — scripts with `input: none`.
- `ListAccepting(input)` — scripts accepting one I/O shape.
- `ListProducing(output)` — scripts producing one I/O shape.
- `ListForFileExtension(extension)` — file scripts matching one extension.
- `ListForFiles(paths)` — file scripts whose extension contract accepts the selection.

Native consumers may invoke the Host contract directly:

```text
PowerScripts.Host.exe list --json [--input <shape>] [--output <shape>] [--no-input]
PowerScripts.Host.exe run <id> [--files <paths...>] [--set name=value...]
PowerScripts.Host.exe transform <id>
```

Use structured process argument APIs where available. Do not concatenate untrusted IDs, paths, or
parameter values into a command line.

## Script presentation and icons

Present scripts by name and resolved input/output contract, not a separate "System" or "File"
category. For example, show `None → Text` or `Files → None`; `none` refers to primary input and
does not imply that there are no named parameters to configure.

Authors may set `@powerscript.icon assets\script.ico` or
`"x-powerscript": { "icon": "assets\\script.ico" }`. The optional icon is a package-relative ICO,
limited to 1 MiB. Absolute paths, URLs, paths escaping the package, and symbolic links/junctions
are rejected. A missing or invalid optional icon does not remove the script: the Host reports
`iconPath: null` and an `iconWarning`.

`list --json` and `PowerScriptInfo.IconPath` expose the validated absolute path. Display it when
appropriate, use a default script icon when absent or undecodable, and do not execute or fetch icon
metadata. Consumers must tolerate missing icon fields from older Hosts. Explorer requests
`shell-menu --icons --files ...`, which adds a third tab-separated icon-path column; without
`--icons`, the existing two-column id/name protocol is unchanged.

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
- the selection count does not exceed `maxFiles`, unless `maxFiles` is `0` (unbounded).

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

The transfer model supports text, HTML, image/audio/video paths, file paths, and extra parameters.
A module must verify that its required shape is wired end to end before advertising that shape;
declaring compatibility in discovery alone is not sufficient.

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
- Host unavailable, disabled, timeout, malformed output, and non-zero exit paths are handled.
- Required/default/invalid parameter cases are covered.
- Trust approval and trust invalidation are covered for automatic consumers.
- File selection tests cover mixed extensions plus minimum and maximum counts.
- The consumer invokes only `PowerScripts.Host.exe`, never script content directly.
- Existing behavior remains unchanged when PowerScripts is disabled or no compatible scripts exist.
