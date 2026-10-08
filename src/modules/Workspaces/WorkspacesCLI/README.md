# Workspaces CLI

Implements the `list` and synchronous `launch` surface for #49900. Capture/create are not included.
See [the command contract](..\..\..\..\doc\devdocs\modules\workspaces-cli.md).

Successful `list --json` responses expose `view` and `workspaces` directly at the top level.
Launch results and errors retain their existing envelopes; human-readable list output is unchanged.

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
worker. It creates a hidden suspended child with Explorer as its process-creation parent,
verifies same-user/session and medium integrity, then duplicates only operation capabilities
through a read-only, PID/image-authenticated bootstrap pipe. The snapshot is read-only and the
owner/cancel handles permit waiting only. The existing result/approval paths are retained.
No medium context or identity mismatch means a clear error, never an elevated fallback.

Unverified elevated EXE confirmation uses the same signature gate as the GUI. A narrow CLI
presenter sends a per-request warning from the worker to the foreground process using private
framed pipes. The foreground uses stderr/console input for A+Enter or S/Enter, with default Skip.
`PendingLaunchApproval` enforces shown acknowledgment, matching IDs and one-time decisions.
Redirected input/stderr and EOF fail closed; Ctrl+C/deadline never approve. Final JSON stdout
remains separate. See the command contract for skipped versus unavailable/denied outcomes.

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

Nonessential metadata failure is a warning after otherwise successful arrangement. A replaced
file that cannot be verified remains a distinct nonzero integrity failure, not a warning or an
asserted rollback. Editors keep edits and display a localized error when saving fails.

CLI diagnostics in `Workspaces\Logs\cli.log` include only fixed command metadata. Shared domain
messages that could contain app names, titles or arguments are not forwarded verbatim. Public
errors are structured in JSON or emitted on stderr.

## Tests and release gates

`CliApprovalTests`, `CliContractTests`, `WorkspaceStoreTests`, managed `WorkspaceWriteTests`, shared `PipeCallerAuthTests`
and manifest-driven `CliShim` tests cover their respective boundaries.
Opt-in `CliApprovalConsoleTests` builds `Tests\ApprovalConsoleFixture` and runs the real presenter
in separate hidden consoles. Inputs target only those fixture consoles; the fixture exchanges
decisions but never executes a target or requests UAC. It checks Allow, default/explicit Skip,
EOF, redirection, cancellation/deadline, mode restoration, control-character escaping, fresh
approval per app, discarded type-ahead and uncontaminated JSON stdout.

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
    "/TestCaseFilter:FullyQualifiedName~CliApprovalTests|FullyQualifiedName~CliApprovalConsoleTests|FullyQualifiedName~CliContractTests|FullyQualifiedName~CliWorkerTests|FullyQualifiedName~WorkspaceStoreTests"
```

`CliHandoffTests` additionally builds `Tests\HandoffFixture`, which checks Explorer-mediated
creation, token identity, restricted handles and Allow/Skip/cancel round-trips without launching
workspace apps. A separate run under normal Windows elevation verifies an actual high-integrity
frontend and medium-integrity child; it does not bypass UAC or change machine policy.

The fixture is under `tests\Workspaces`, never installed or placed on PATH. Real UAC accept/deny,
secure-desktop timeout, multi-monitor/mixed-DPI behavior and signed installer testing require an
appropriate interactive environment. Both administrator and normal terminals are supported
when the same user's normal desktop shell is available for de-elevation.
