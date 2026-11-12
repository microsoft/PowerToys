# Workspaces CLI

Implements the `list` and synchronous `launch` surface for #49900. Capture/create are not included.
See [the command contract](..\..\..\..\doc\devdocs\modules\workspaces-cli.md).

Successful `list --json` responses expose `view` and `workspaces` directly at the top level.
Launch results and errors retain their existing envelopes; human-readable list output is unchanged.
Launch results preserve native `Launched` as `launched`, with a localized explanation that arrangement
is unconfirmed. Only explicit arrangement failures use `arrangeFailed`. Any successful launch or
arrangement counts as partial progress unless every app is arranged; `skipped` plus `launched`
therefore yields `partial`. Exit 0 still requires all apps arranged, and incomplete results retain exit 10.
The final-result boundary rejects pending `Waiting` states and missing/invalid entries before
acknowledging completion, reporting `outcomeUnknown` rather than inventing an app-level `unknown`.

## Entry point and dependencies

```text
<install>\bin\PowerToys.Workspaces.CLI.exe  (shared native CliShim, on PATH)
             |
             v
<install>\PowerToys.WorkspacesCLI.exe      (actual native CLI, not on PATH)
<install>\PowerToys.WorkspacesLauncher.exe (shared session entry and execution host)
<install>\PowerToys.WorkspacesWindowArranger.exe
```

Only the shim directory is added to PATH. Do not add the installation root, `WinUI3Apps`, or a
self-contained runtime directory. The shim forwards argument quoting, console streams and exit
codes and resolves the real executable by absolute installed-relative path.
The Editor also resolves its Launcher and Snapshot Tool beside its own executable, with an
explicit module working directory; it does not rely on the directory of the invoking terminal.

The CLI only parses commands, owns console cancellation and formats public output. Both the CLI
and the GUI launcher call `WorkspacesSession::Start` in `WorkspacesLib`. The library compiles the
existing execution engine once; neither adapter duplicates launcher sources or orchestration.

```text
CLI adapter --------------------+
                                +--> shared session entry / context check
GUI launcher adapter -----------+          |
                                 Medium --+--> in-process Execute()
                                 High ----+--> Medium Launcher host --> same Execute()
                                                        |
                                  shared project preparation + Launcher
                                     /                       \
                             AppLauncher                  Arranger / UI IPC
                                                 |
                                    typed outcome + one history-update path
```

The shared session entry captures the selected workspace and validates execution context. Medium
callers execute in their own process without creating a Launcher host or session transport.
Administrator callers use module-shared `SessionHandoff` and the existing `run_non_elevated` helper,
verifying the created host's actual Medium token before running it. Both paths invoke the same
execution core, with only presentation/invocation/deadline policy differences.

Only the elevated path needs a host, read-only snapshot mapping, wait-only cancel/owner handles and a result writer.
It writes an internal session response to that explicit pipe, not to public CLI stdout. The CLI maps
the typed outcome to its existing JSON/exit-code contract. No general administrator handle, new
daemon or privileged Runner endpoint is added; identity failures never fall back to elevated work.

Unverified elevated EXEs use the existing GUI confirmation flow end-to-end:
`Launcher::requestApproval` owns the single pending request and talks through `LauncherUIHelper`
to `SignatureWarningWindow`. CLI mode starts that UI lazily without the progress window.
Without a status-window owner, the shared warning is centered on screen, topmost, and available
in the taskbar so it cannot silently hide behind the launching terminal without a discoverable entry.
The same rendered acknowledgement, default Skip, file pin and reply validation apply; there
is no CLI-specific approval channel or input state machine. Ctrl+C/deadline cancel the shared
request. Final JSON stdout remains separate from the dialog.
The confirmation host reuses `RunnerHelper`'s wait-only process lifetime monitoring; it does not
request broad access to the session host through `Process.EnableRaisingEvents`.
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

Building the CLI builds the shared Launcher host and its UI/arranger dependencies and stages the
shim in the output `bin` directory. Use matching binaries;
do not replace the installed application merely to run a local build. Debug permits unsigned
development peers while retaining path/version/session checks; Release does not.

## Persistence and diagnostics

Launch uses a saved snapshot and existing app-resolution/arrangement behavior. Final history
updates acquire `workspaces.json.lock`, re-read the latest JSON, update only the matching
workspace's timestamp, and use checked same-directory replacement/readback. Cooperating native
and managed list writers use the same lock and preserve newer launch history.
Shared session preparation handles IDs and app resolution once for both entry points. GUI
invocations persist refreshed IDs/package paths; CLI invocations retain their existing policy
of persisting only missing IDs and resolving paths inside the operation snapshot. Unsaved
Launch-and-Edit drafts initialize IDs in memory without updating the saved store or launch history.
The GUI pre-launch refresh merges only changed application IDs, package names and
paths into a fresh selected workspace under that lock. Concurrent workspace edits/additions/deletions
and unknown fields are preserved. Missing/ambiguous apps or changed target metadata cause a conflict;
the launcher stops rather than overwriting newer data or giving the arranger stale metadata.
For legacy ID-less applications, ordinal matching is allowed only when the complete current
application list matches the original snapshot; otherwise a unique unchanged match is required.
The shared execution core initializes missing IDs through this same transaction before launching, keeps existing IDs
unchanged, and rejects malformed/duplicate IDs. Failed persistence leaves the in-memory snapshot
unchanged and prevents application startup. Lists do not initialize IDs or write configuration.
General Editor snapshot-conflict detection and save-workflow changes are outside this CLI's scope.
The existing Editor save behavior is retained. The shared writer provides atomic replacement and
best-effort history preservation, not a merge of cached Editor configuration with concurrent edits.
An Editor save from an older snapshot can still overwrite newer configuration/application metadata.
Readers share deletion, and native/managed atomic replacement retries sharing/lock violations and
access-denied replacement errors for up to two seconds to tolerate older short-lived readers.
Windows can report an open destination without delete sharing as access denied; persistent permission
or sharing failures still fail without replacing the original file.

Nonessential metadata failure is a warning after otherwise successful arrangement. A replaced
file that cannot be verified remains a distinct nonzero integrity failure, not a warning or an
asserted rollback. Additional Editor conflict notifications and edit-retention guarantees are deferred.

CLI diagnostics in `Workspaces\Logs\cli.log` include only fixed command metadata. Shared domain
messages that could contain app names, titles or arguments are not forwarded verbatim. Public
errors are structured in JSON or emitted on stderr.
Separately, optional usage telemetry emits one `Workspaces_CLICommand` event in the frontend with
only a fixed command category and success status, following the Settings CLI. The Medium host does
not emit this command event. Workspace names/IDs, paths, argument values and error text are excluded.
File-log initialization is optional: filesystem/sink failures retain the installed logger, emit
debugger diagnostics and add `loggingUnavailable` to completed launch results instead of aborting startup.
Private host errors use the session protocol; console/resource diagnostics cannot contaminate its explicit result pipe.
The frontend changes its console output code page only while writing output. Both paths allow a bounded
two-second completion acknowledgement after cancellation or a launch deadline without extending approval time.
The in-process worker owns all execution state. If a Shell call cannot acknowledge stopping, the adapter
reports `outcomeUnknown` and exits; it does not force-kill a thread or report successful cancellation.

## Tests and release gates

`SignatureVerificationTests`, `CliContractTests`, `WorkspaceStoreTests`, managed `WorkspaceWriteTests`, shared `PipeCallerAuthTests`
and manifest-driven `CliShim` tests cover their respective boundaries.
`WorkspacesLauncherUI.UnitTests` covers the existing dialog, rendered acknowledgement, Allow/Skip,
dismissal and replay rules, including confirmation-only mode without a visible status owner.
Opt-in `CliRuntimeTests` builds `Tests\RuntimeFixture` to verify code-page restoration and the shared
GUI/CLI session entry with synthetic snapshots and no additional Launcher host, plus inline cancellation
and startup deadlines. `CliWorkerTests` retains bounded completion, timeout
acknowledgement and actual Launcher-host/arranger coverage.

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
