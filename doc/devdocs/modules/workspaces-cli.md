# Workspaces command-line interface

Related issue: #49900. This interface lists saved workspaces and launches one through the existing
Workspaces engine. It does not capture, create, import, delete or edit workspace configurations.

## Installation

The installer adds only `<PowerToys installation>\bin` to PATH. `PowerToys.Workspaces.CLI.exe`
in that folder is the shared native CLI shim, not the application payload. It launches
`..\PowerToys.WorkspacesCLI.exe` using the installed-relative mapping.

Do not add the installation root or `WinUI3Apps` to PATH. Those folders contain application/runtime
DLLs and are not command directories. The wrapper avoids advertising those DLL locations through
PATH; it does not replace the operating system's DLL security rules.

The manifest and WiX component list are checked together. The real CLI and shared shim are included
in release signing, and installer shutdown recognizes both process names. See
[CLI conventions](..\cli-conventions.md).

## Commands

```text
PowerToys.Workspaces.CLI.exe list [--json]
PowerToys.Workspaces.CLI.exe list --id <guid> [--details] [--json]
PowerToys.Workspaces.CLI.exe list --name <name> [--details] [--json]
PowerToys.Workspaces.CLI.exe launch --id <guid> [--timeout <seconds>] [--json]
PowerToys.Workspaces.CLI.exe launch --name <name> [--timeout <seconds>] [--json]
```

Help/version do not require an enabled module. Both operational commands reject user-disabled
or policy-disabled Workspaces. Configured GPO overrides the user toggle exactly as in Settings.
The Runner need not be running when the effective module configuration is enabled.

Use exactly one selector for launch or detail. GUID matching accepts braces and either case,
without rewriting stored IDs. Name matching is exact, ordinal and case-insensitive; ambiguous
names fail rather than selecting the first workspace. An explicit missing selector target is
an error; an enabled module with no workspace file has an empty unfiltered list.

## Data contract

`--json` returns one UTF-8 document. Successful `list` responses contain only `view` and
`workspaces` at the top level, without a `schemaVersion`, `command`, `state` or `result` wrapper.
Launch responses and errors keep the envelope with `schemaVersion`, `command`, `state` and
`result` or `error`. Failed queries return a structured error and a nonzero exit code, never an
empty successful list.
Public JSON is formatted with line breaks and two-space indentation, followed by a newline,
including when stdout is redirected. It is one JSON object, not a quoted JSON string or
newline-delimited JSON records. Formatting does not alter stored field names or values.
The private worker/confirmation protocol is unchanged.
Warnings are structured separately and never replace an application failure.

Summary entries are a subset of the existing stored workspace JSON:

```json
{
  "view": "summary",
  "workspaces": [
    {
      "id": "{6CF910A2-D2E0-436D-A50E-41432A88452A}",
      "name": "Development",
      "applications": [
        { "application": "Notepad" },
        { "application": "Microsoft Edge" }
      ]
    }
  ]
}
```

Both unfiltered and selected lists contain `view: "summary"` or `"detail"` and a top-level
`workspaces` array, even for one selected workspace or no workspaces. Scripts can read
`.workspaces` directly. Summary preserves application order, duplicates, field names, types
and values; no `apps`, per-app `name`, or count alias is added.

Detail reuses the native workspace serializer, including existing kebab-case names, timestamp
units and monitor/DPI configuration. Paths and command arguments may contain sensitive data:
request detail deliberately and protect redirected output. Runtime handles/process IDs are not
part of stored workspace JSON.

Launch results contain the workspace and operation IDs, per-application results and a separate
persistence status. An observed arranged state does not certify that a document/tab has loaded
or that an application is ready for business use.

## Launch behavior

- Launch waits for an explicit final arranger result. There is no accepted-only/no-wait mode.
- The default total deadline is 120 seconds; `--timeout` accepts 1 through 600.
- Workspaces progress/editor/error windows are suppressed. Application windows and normal
  Windows UAC remain possible.
- Both ordinary and administrator terminals use the same command. An administrator frontend
  starts a non-elevated worker in the same user's desktop session while keeping prompts/output
  in the original terminal. Existing saved elevation flags still determine individual app/UAC
  behavior; the administrator caller does not implicitly elevate every app.
- Existing windows follow the saved `move-existing-windows` setting. Another launch in the same
  session returns busy rather than queueing or taking over.
- Ctrl+C requests stopping further launches. Already-started applications are not killed and
  window positions are not rolled back. A blocked operation without acknowledgment is reported
  as unknown outcome, not canceled.
- A timeout does not mean that a Windows consent dialog was dismissed. Do not automatically retry:
  launch is not idempotent and repeating it may open more applications.
- The worker and arranger use the same immutable selected snapshot. Final history writes read
  the current file under a shared writer lock and do not replace it with the old launch snapshot.

### Administrator terminal handoff

The foreground CLI remains in its original context. It validates the desktop shell's user,
session and medium integrity, creates a suspended worker using Explorer's process-creation
context, and verifies the child's actual token before allowing any code to run.

The child receives its operation handles through a bounded, read-only bootstrap pipe. The
frontend pins the exact created PID; both sides verify the expected module image/version and
Microsoft signature in Release. User SID/session must match, and the child must be non-elevated.
Only a read-only snapshot, wait-only cancellation/owner-lifetime handles and the existing
input/result/approval endpoints are duplicated. No token handle or general administrator
process capability is handed to the worker.

This uses the same Explorer-parent mechanism as the repository's `run_non_elevated` helper,
but retains the suspended process/handles for verification and the private transfer. It does
not use process-name discovery or replace `RunNonElevatedEx` globally. After transfer the
ordinary worker, approval protocol, result formatting and cancellation loop are reused.

No normal same-user Explorer, a different-user administrator, wrong session, identity mismatch
or failed transfer yields `deElevationFailed`; the CLI never falls back to elevated execution.
No extra console is opened. UAC for individual configured elevated apps remains separate.

## Unverified executable confirmation

Workspaces' existing shared launch gate checks eligible EXE targets configured to run as
administrator. This is separate from Windows UAC and from IPC peer authentication. The CLI
does not change the signature policy or create a permanent trust list.

When the shared gate requires a decision, the foreground CLI writes a warning to its stderr
console with the application, checked path, arguments, verification reason and status:

```text
[A] Allow once  [S] Skip (default). Type A or S and press Enter:
```

- A followed by Enter allows only the current checked target/request. Normal Windows UAC may
  still follow. S or an empty Enter skips that application, not the rest of the workspace.
- Invalid input asks again; it never becomes approval by default. Pre-existing type-ahead is
  discarded for each newly displayed warning.
- Both stdin and stderr must be attached to an interactive console. Redirecting stdin or stderr,
  EOF (Ctrl+Z/Ctrl+D), unavailable display/input, or a disconnected presenter cannot approve.
  The per-app result reports `confirmationRequired`, distinct from an explicit `skipped` choice.
- `--json` does not itself prevent interaction: stdout can be redirected while stdin and stderr
  remain on the terminal. Prompt text never appears in the JSON stdout stream.
- Ctrl+C/Escape cancel the pending wait/operation. The existing total launch deadline includes
  decision time; expiry never approves. Already-launched applications remain open.
- The worker retains the signature-checked file handle through the decision and launch. Replies
  are matched to one pending request and cannot approve a later app or overwrite a decision.
- App names/paths/arguments in the warning are terminal-escaped, not interpreted as control
  sequences, and are not copied to diagnostic logs or telemetry.

The private worker retains stdin=NUL. A separate framed request/reply pair carries confirmation;
the final-result pipe and public workspace JSON remain unchanged. Pipe endpoints are connected
and verified inside the parent, then only the intended handles are inherited. Overlapped writes
and polled reads keep waits cancellable; no public approval service is exposed.

## Exit codes

| Code | Meaning |
|---:|---|
| 0 | Requested operation completed, possibly with nonessential history-save warnings |
| 1 | Unexpected command/runtime error |
| 2 | Invalid command, options or selector syntax |
| 3 | Workspace not found |
| 4 | Ambiguous workspace name |
| 5 | Another launch owns the session |
| 6 | Disabled by user/policy, policy lookup failure or component unavailable; inspect `error.code` |
| 7 | Unsupported caller context, failed de-elevation, arranger startup failure or required consent declined |
| 8 | Invalid or unreadable saved configuration |
| 9 | Deadline or unknown outcome |
| 10 | One or more applications failed, were skipped, or could not obtain required confirmation |
| 11 | Persistence replacement could not be verified; app results remain separate |
| 12 | Acknowledged cancellation |
| 9009-9011 | Shared shim could not resolve/find/start its target |

Stable machine error codes distinguish reasons such as `disabledByUser`, `disabledByPolicy`,
`workspaceNotFound`, `deElevationFailed`, `consentDenied` and `outcomeUnknown`; human messages are resource-backed.
Per-app `skipped`, `confirmationRequired`, `confirmationTimedOut`, `confirmationFailed` and
`canceled` are not aliases for a Windows UAC denial. Skipping an app means the requested workspace
was not fully restored, so it is not reported as all-success.

## Compatibility boundaries

The CLI does not enable modules, migrate storage to another format, or add a permanent service.
The public CLI contract covers the reused detail shape: future breaking storage changes must
not silently change that public output, including unversioned successful list responses.
Future creation APIs may reuse appropriate payload types,
but query/result envelopes and system-owned IDs are not automatically writable input.
