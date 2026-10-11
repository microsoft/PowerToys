# Workspaces CLI

Implements the `list` and synchronous `launch` surface for #49900. Capture/create are not included.
See [the command contract](..\..\..\..\doc\devdocs\modules\workspaces-cli.md).

Successful `list --json` responses expose `view` and `workspaces` directly at the top level.
Launch results and errors retain their existing envelopes; human-readable list output is unchanged.
Launch results preserve native `Launched` as `launched`, with a localized explanation that arrangement
is unconfirmed. Only explicit arrangement failures use `arrangeFailed`; aggregate exit rules are unchanged.

## Entry point and dependencies

```text
<install>\bin\PowerToys.Workspaces.CLI.exe  (shared native CliShim, on PATH)
             |
             v
<install>\PowerToys.WorkspacesCLI.exe      (actual native CLI, not on PATH)
<install>\PowerToys.WorkspacesWindowArranger.exe
```

Only the shim directory is added to PATH. Do not add the installation root, `WinUI3Apps`, or a
self-contained runtime directory. The shim forwards argument quoting, console streams and exit
codes and resolves the real executable by absolute installed-relative path.

The CLI links `WorkspacesLib` and compiles existing launcher sources by reference. Its private
worker receives an immutable snapshot via an explicitly inherited memory mapping and returns a
result through an inherited pipe. The arranger uses operation-scoped existing IPC, with
bidirectional peer image/version validation, same-session checks, and Microsoft signing
verification in Release. No privileged Runner endpoint or permanent service is added.

Administrator frontends use `WorkerHandoff` instead of inheriting their elevated token into the
worker. The common `run_non_elevated` helper creates a hidden suspended child with Explorer as its parent,
verifies same-user/session and medium integrity, then duplicates only operation capabilities
through a read-only, PID/image-authenticated bootstrap pipe. The snapshot is read-only and the
owner/cancel handles permit waiting only. The existing result path is retained.
No medium context or identity mismatch means a clear error, never an elevated fallback.

Unverified elevated EXEs use the existing GUI confirmation flow end-to-end:
`Launcher::requestApproval` owns the single pending request and talks through `LauncherUIHelper`
to `SignatureWarningWindow`. CLI mode starts that UI lazily without the progress window.
The same rendered acknowledgement, default Skip, file pin and reply validation apply; there
is no CLI-specific approval channel or input state machine. Ctrl+C/deadline cancel the shared
request. Final JSON stdout remains separate from the dialog.
The shared UI helper waits for its receiver pipe to become available before accepting the first
warning. Startup remains bounded and honors cancellation; process existence alone is not readiness.

## Local build

Run the repository essentials workflow when dependencies are missing, then from the worktree root:

```powershell
$root = (Get-Location).Path
& .\tools\build\build.ps1 -Path .\src\modules\Workspaces\WorkspacesCLI `
    -Platform x64 -Configuration Debug -ExtraArgs "/p:SolutionDir=$root\"
& .\tools\build\build.ps1 -Path .\src\modules\Workspaces\WorkspacesWindowArranger `
    -Platform x64 -Configuration Debug -ExtraArgs "/p:SolutionDir=$root\"

.\x64\Debug\bin\PowerToys.Workspaces.CLI.exe list --json
.\x64\Debug\bin\PowerToys.Workspaces.CLI.exe list --name "Development" --details --json
.\x64\Debug\bin\PowerToys.Workspaces.CLI.exe launch --id "{workspace-guid}" --json
```

Building the CLI builds/stages the shim in the output `bin` directory. Use matching binaries;
do not replace the installed application merely to run a local build. Debug permits unsigned
development peers while retaining path/version/session checks; Release does not.

## Persistence and diagnostics

Launch uses a saved snapshot and existing app-resolution/arrangement behavior. Final history
updates acquire `workspaces.json.lock`, re-read the latest JSON, update only the matching
workspace's timestamp, and use checked same-directory replacement/readback. Cooperating native
and managed list writers use the same lock and preserve newer launch history.
The GUI launcher's pre-launch refresh merges only changed application IDs, package names and
paths into a fresh selected workspace under that lock. Concurrent workspace edits/additions/deletions
and unknown fields are preserved. Missing/ambiguous apps or changed target metadata cause a conflict;
the launcher stops rather than overwriting newer data or giving the arranger stale metadata.
For legacy ID-less applications, ordinal matching is allowed only when the complete current
application list matches the original snapshot; otherwise a unique unchanged match is required.
The CLI initializes missing IDs through this same transaction before launching, keeps existing IDs
unchanged, and rejects malformed/duplicate IDs. Failed persistence leaves the in-memory snapshot
unchanged and prevents application startup. Lists do not initialize IDs or write configuration.
Editor saves compare the last loaded/successfully saved snapshot with the current file under the
same lock, ignoring only launch-history changes. Concurrent configuration or application-metadata
changes reject the save and retain the open edits rather than overwriting newer IDs or package paths.
Readers share deletion, and native/managed atomic replacement retries sharing/lock violations and
access-denied replacement errors for up to two seconds to tolerate older short-lived readers.
Windows can report an open destination without delete sharing as access denied; persistent permission
or sharing failures still fail without replacing the original file.

Nonessential metadata failure is a warning after otherwise successful arrangement. A replaced
file that cannot be verified remains a distinct nonzero integrity failure, not a warning or an
asserted rollback. Editors keep edits and display a localized error when saving fails.

CLI diagnostics in `Workspaces\Logs\cli.log` include only fixed command metadata. Shared domain
messages that could contain app names, titles or arguments are not forwarded verbatim. Public
errors are structured in JSON or emitted on stderr.
File-log initialization is optional: filesystem/sink failures retain the installed logger, emit
debugger diagnostics and add `loggingUnavailable` to completed launch results instead of aborting startup.
Private worker error fallbacks use debugger diagnostics, never text mixed into the JSON result pipe.
The frontend restores its console output code page after success or failure and allows a bounded
two-second completion acknowledgement after a launch deadline without extending approval time.

## Tests and release gates

`SignatureVerificationTests`, `CliContractTests`, `WorkspaceStoreTests`, managed `WorkspaceWriteTests`, shared `PipeCallerAuthTests`
and manifest-driven `CliShim` tests cover their respective boundaries.
`WorkspacesLauncherUI.UnitTests` covers the existing dialog, rendered acknowledgement, Allow/Skip,
dismissal and replay rules, including confirmation-only mode without a visible status owner.
Opt-in `CliRuntimeTests` builds `Tests\RuntimeFixture` to verify code-page restoration and JSON-only
worker errors. `CliWorkerTests` retains bounded completion and timeout acknowledgement coverage.

Real worker tests use synthetic snapshots and a disposable window fixture, not saved user launch
configurations. They are opt-in:

```powershell
$root = (Get-Location).Path
& .\tools\build\build.ps1 -Path .\src\modules\Workspaces\WorkspacesCLI\Tests\WindowFixture `
    -Platform x64 -Configuration Debug -ExtraArgs "/p:SolutionDir=$root\"
& .\tools\build\build.ps1 -Path .\src\modules\Workspaces\WorkspacesLib.UnitTests `
    -Platform x64 -Configuration Debug `
    -ExtraArgs @("/p:SolutionDir=$root\", "/p:WorkspacesCliLiveTests=true")
$env:WORKSPACES_CLI_LIVE_TESTS = '1'
vstest.console.exe "$root\x64\Debug\tests\Workspaces\Workspaces.Lib.UnitTests.dll" `
    "/TestCaseFilter:FullyQualifiedName~CliRuntimeTests|FullyQualifiedName~CliContractTests|FullyQualifiedName~CliWorkerTests|FullyQualifiedName~WorkspaceStoreTests|FullyQualifiedName~CliHandoffTests"
```

`CliHandoffTests` additionally builds `Tests\HandoffFixture`, which checks Explorer-mediated
creation through the common helper, token identity, restricted handles and cancellation without launching
workspace apps. A separate run under normal Windows elevation verifies an actual high-integrity
frontend and medium-integrity child; it does not bypass UAC or change machine policy.

The fixture is under `tests\Workspaces`, never installed or placed on PATH. Real UAC accept/deny,
secure-desktop timeout, multi-monitor/mixed-DPI behavior and signed installer testing require an
appropriate interactive environment. Both administrator and normal terminals are supported
when the same user's normal desktop shell is available for de-elevation.
