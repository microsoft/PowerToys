# winappcli Sandbox integration feedback

Updated: October 14, 2026

The results below are historical evidence for the cited revision, not validation
of subsequent PR changes. Current MWB validation uses a separately marked Release
UI-test variant for non-console sessions; shipping Release remains console-only.
See the [maintained suite](../../../src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/README.md)
for its build and validation contract.

## Summary

**Sandbox mode is viable: the complete eleven-case shared Release pilot now
passes on both Windows versions. Automated integrations still need substantial
lifecycle and orchestration code around it.** The highest-value improvements
remain faster, observable cold preparation, first-class owned sessions with
interruption-safe cleanup, and an explicit record-and-interact workflow API.

This report describes our Mouse Without Borders (MWB) integration using the
official, unpatched [winappcli v0.7.0 release][release], source commit
`2fdd020c5b7db093e8b3e317b22bdd8135a47e89`. It does not claim that later versions
have the same limitations.

The modern scenario runs on a Windows 11 VM with a nested Windows Sandbox as the
second endpoint. Windows 10 uses our separate Legacy Sandbox adapter, not
winappcli's modern Sandbox backend. Both endpoints use the same coherent
self-contained **Release** product archive, with existing authentication and
valid matching signatures preserved. This is a dedicated-VM user-desktop pilot,
not installed-path, physical-machine, service/System, secure-desktop, ARM64 live,
or complete MWB module sign-off.

[CI build 160517346][ci-full] passed **11/11 cases on Win10 and 11/11 on Win11**
at PowerToys implementation source
`b7be098373ebb0b41a2de352f46abf00f05e719f`. Both jobs passed all eight smoke
phases, including real Settings pairing, owned TCP transport, physical remote
input, clipboard isolation/bidirectional transfer, recordings, and clean
restoration. The four injected recovery cases also passed their explicit
expected-failure and ownership assertions. This supersedes the earlier
smoke-only Debug evidence; it is not a universal no-flake or performance claim.

The successful modern run confirms that official target transfer, attached
guest execution, passive snapshots, cooperative UI/recording workflows and
recording finalization can work together without patching the released binary
or weakening authentication. The feedback below concerns the remaining
integration burden, not a claim that these capabilities are absent or unusable.

### How to read the snippets

The C# and PowerShell snippets below are excerpts from our implemented adapter.
They depend on surrounding helpers and are not standalone programs.
`Run`, `Budget`, `ValidateOwnership`, and `WinAppSandboxProtocol` are
**PowerToys adapter helpers**, not public winappcli APIs.

No passwords, pairing keys, guest-agent authentication material, or private
target-state contents are included.

| Priority | Improvement | Integration code it would replace or simplify |
| --- | --- | --- |
| P1 | Efficient, observable cold preparation | Large bootstrap budgets and custom phase timing |
| P1 | Owned session, expected-instance acquisition and durable cleanup receipts | Raw `wsb` orchestration, private state inspection and interrupted-creation journals |
| P1 | First-class cooperative recording workflow | Environment propagation across every host/guest child |
| P2 | CI-friendly prerequisite/setup contract | Per-account installer, inventories, and setup reporting |
| P2 | Explicit readiness and owned teardown | Logon probes, retained process ancestry, cleanup journals |
| P2 | Structured operation/recording control | Custom stream draining, readiness parsing, stdin stop protocol |
| P2 | Maintained integration example or .NET wrapper | Repeated consumer-specific lifecycle glue |

## 1. Optimize cold guest-agent preparation

### Observed friction

Earlier `target push` measurements were approximately **402-744 seconds**
locally and about **76 seconds** in earlier CI. These are historical
preparation measurements, not a new benchmark of the final green build, and
the cost is highly environment-dependent. The first push also prepares the
guest agent; it is not merely copying files.

The [released implementation][upstream-firewall] enumerates active firewall
rules and invokes `Get-NetFirewallApplicationFilter` separately for each rule.
This causes repeated NetSecurity/CIM work in a cold guest.

### What we implemented

We keep separate provider-start, guest-bootstrap and whole-run ceilings, and
give the first push only the remaining bootstrap time. Current adapter values
are:

```csharp
// SandboxTimeouts.cs
internal static readonly TimeSpan ModernBootstrap = TimeSpan.FromMinutes(35);
internal static readonly TimeSpan ModernRun = TimeSpan.FromMinutes(90);
internal static readonly TimeSpan ModernStart = TimeSpan.FromMinutes(15);

// WinAppSandbox.cs: Target/Budget clamp this call to the remaining deadline.
Target(
    ["push", "sandbox", stagedPayload, "MwbBootstrap", "--json"],
    SandboxTimeouts.ModernBootstrap,
    bootstrap: true,
    firstBootstrap: true);
```

We also bundle the payload into one transfer rather than initiating a separate
target preparation for every component. Increasing the allowance did not make
the firewall query faster; it allowed slow preparation to finish.

Later cold local Win11 runs also exhausted the **900-second raw `wsb start`**
ceiling before the first target push or PowerToys startup. That is a separate
Windows provider/environment boundary, not evidence that the firewall query or
winapp transport caused those failures. The fresh CI modern run passed while
the retained local prerequisite remained blocked; neither result erases the
other. Lower-resource local Win11 profiles remain unvalidated.

Sources: [SandboxTimeouts.cs][pt-timeouts], [WinAppSandbox.cs][pt-adapter].

### Requested improvement

Query application filters in bulk and match their rule associations, or use
another bounded, efficient strategy for finding rules for the exact agent
executable. Preserve the existing security scope; do not disable the firewall.

Expose preparation phases separately from transfer: provider startup, interactive
login, firewall preparation, agent launch/authentication, and file transfer.
Benchmark fresh guests with large firewall inventories and report phase timings
and warm/cold behavior. Consumers should not see a small transfer appear to hang
for ten minutes when most time is spent preparing the guest.

## 2. Expose a first-class owned Sandbox session

### Observed friction

We need an isolated test session with explicit configuration and predictable
ownership. The interactive default of reusing an existing Sandbox is not enough
for that contract.

In v0.7.0 there is no public attach-only/expected-instance option. We precreate a
provider GUID through `wsb`, then verify that winapp adopted that exact instance.
Our pre/post checks detect replacement, but they are not an atomic acquisition
fence.

### What we implemented

```csharp
WinAppSandboxProtocol.RequireEmptyInventory(
    Inventory(Budget(InventoryTimeout, bootstrap: true)));

// Journal ownership before calling a provider that might create the VM and time out.
creationAttempted = true;
saveJournal();

var result = Run(
    wsbPath,
    ["start", "--id", instanceId.ToString("D"),
     "--config", configuration.ToString(SaveOptions.DisableFormatting), "--raw"],
    Budget(SandboxTimeouts.ModernStart, bootstrap: true));

WinAppSandboxProtocol.RequireStartResult(result, instanceId);
WinAppSandboxProtocol.RequireExclusiveInstance(
    Inventory(Budget(InventoryTimeout, bootstrap: true)), instanceId);
```

We also inspect private target-state files to check the adopted instance and
generation:

```csharp
var stateFiles = WinAppSandboxPayload.PlainFiles(targetStateRoot)
    .Where(path => Path.GetFileName(path) == "target-state.json")
    .Take(2)
    .ToArray();

epoch = WinAppSandboxProtocol.RequireAdoptedState(
    stateFiles.Select(ReadPrivateState).ToArray(), instanceId, epoch);
```

Those files stay in private control storage and are never attached as test
artifacts. Requiring consumers to understand this state format is still an
integration burden.

An additional case matters for automation: `wsb start` can time out after
creation was attempted but before the caller receives confirmation. We journal
the trusted run-derived GUID **before** the call, stop only that exact instance,
and verify its absence. Even when owned provider cleanup completes, our
conservative recovery keeps interrupted creation `Incomplete` and requires a
clean baseline reset. It does not invent settings/clipboard restoration or
adopt whatever Sandbox happens to exist afterward.

Sources: [WinAppSandbox.cs][pt-adapter], [WinAppSandboxProtocol.cs][pt-protocol].

### Requested improvement

Expose explicit modes for creating a new owned session and attaching to an
expected instance without creating, adopting, or repairing another one.
Bind subsequent commands to that instance and generation, failing on mismatch.

Support caller-supplied clipboard/network restrictions and folder mappings through
that session API. Close only resources owned by the session and return a cleanup
receipt. Keep the existing adoption behavior as an interactive default, not an
implicit automation policy.

Publish a durable, non-secret session/operation identity before creation can
have side effects. Let a fresh controller query whether creation never began,
is still in progress, completed, or has unknown ownership, and request
idempotent exact-owner cancellation/cleanup after the original CLI exits.
A receipt should distinguish provider absence, client/process cleanup and
private-state cleanup. It must not imply that the consumer application's
settings or in-memory clipboard snapshot were restored.

## 3. Make record-and-interact cooperation explicit

### Observed friction

A recording without the same workflow ID as the UI actions held the desktop
turn and blocked mutations. Read-only searches still worked, making this look
like a hung navigation action rather than workflow contention.

The cooperation mechanism already exists and is
[documented][upstream-workflows]. The problem is discoverability and propagation
when a harness launches independent CLI processes and an attached guest worker.

### What we implemented

Every adapter command receives private state isolation and the same run-scoped
workflow ID in its child environment:

```csharp
start.Environment["WINAPP_TARGET_STATE_ROOT"] = targetStateRoot;
start.Environment["WINAPP_CLI_TELEMETRY_OPTOUT"] = "1";
start.Environment["WINAPP_CLI_UPDATE_CHECK"] = "0";

if (workflowId is { } id)
{
    start.Environment["WINAPP_UI_WORKFLOW_ID"] = id.ToString("D");
}
```

Both one-shot commands and attached worker/recorder commands pass the same
`instanceId` into that launcher:

```csharp
using var command = WinAppSandboxCommand.Start(
    executable, arguments, targetStateRoot, controlRoot, instanceId);
```

The official target implementation forwards a hashed, generation-specific
workflow identity into guest execution. We rely on that supported mechanism;
we do not edit the global desktop lock or expose the raw coordination state.

Sources: [WinAppSandboxCommand.cs][pt-command], [WinAppSandbox.cs][pt-adapter],
[upstream recording contract][upstream-record].

### Requested improvement

Make workflow identity a first-class CLI option or session handle. Provide
helpers that propagate it through recording, `target exec`, and guest UI actions.

A blocked action should report the owning operation, wait duration, and
cooperation instructions, without exposing private coordination material.
Keep independent workflows isolated; do not simply remove the desktop lock.

## 4. Provide a CI-friendly prerequisite/setup contract

### Observed friction

The optional Windows Sandbox feature was enabled on CI, but the modern client
package was absent for the actual interactive test account. Feature enablement,
package installation, per-user registration, and a functioning `wsb.exe` alias
are separate conditions.

winapp already provides useful [setup states and manual remediation
instructions][upstream-setup]. We needed an unattended, per-account setup flow
and durable before/after evidence.

### What we implemented

An elevated controller stages trusted setup scripts, then dispatches the worker
to the exact logged-on account as an Interactive, Limited task:

```powershell
$principal = New-ScheduledTaskPrincipal `
    -UserId $interactiveUser -LogonType Interactive -RunLevel Limited

$settings = New-ScheduledTaskSettingsSet `
    -Priority 4 -ExecutionTimeLimit (New-TimeSpan -Seconds 630) `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $taskName -Action $action `
    -Principal $principal -Settings $settings | Out-Null
```

The worker rejects elevation, SYSTEM, session zero, the wrong SID, and a missing
Explorer desktop. An already ready client is a no-op. Otherwise we register an
existing trusted package or trigger the signed inbox first-launch updater.

The controller verifies WindowsApps ownership/ACLs and the package manifest,
then supplies a private, administrator-owned, read-only proof. The Limited worker
cannot reliably read WindowsApps ACLs itself. We do not download packages from
third-party mirrors, bypass trust checks, enable features, reboot, or change
Store policy.

Sources: [Install-MwbSandboxClient.ps1][pt-setup],
[MwbSandboxClientSetup.Common.ps1][pt-setup-common].

### Requested improvement

Provide a standalone, read-only doctor report covering machine and current-user
readiness, with explicit failed versus unchecked stages. Privileged inventories
should say "not checked" when permission is unavailable.

Offer an explicit opt-in setup helper or maintained CI recipe for supported
client installation/registration. Never silently elevate or reboot. Some parts
need a supported contract from the Windows Sandbox team rather than a CLI-only
workaround.

## 5. Simplify readiness and owned teardown

### Observed friction

Provider creation, a connected client, an interactive desktop, and a ready
authenticated agent are different milestones. We added bounded probes between
them. First-launch installation could also leave a client after guest shutdown.

Application readiness is a further, **consumer-owned** milestone. In the later
Release pilot, the MWB Settings RPC pipe was available before the module's main
form and TCP listeners finished initializing. Settings UI readiness plus a live
MWB PID therefore admitted pairing too early. The harness now requires both
native listeners (15100/15101) on the exact owned MWB PID and two stable
Settings/MWB identity samples before submitting real New key/Connect, within
the existing startup ceiling. The snapshot-save fix alone still failed Win11
transport; the subsequent readiness guard passed both full CI suites. This
does not prove every prior invalid packet had one cause, and it is not a
winappcli agent-readiness or networking defect.

### What we implemented

After one owned connection attempt, we poll a safe no-op in the existing guest
login context:

```csharp
var result = Run(
    wsbPath,
    ["exec", "--id", instanceId.ToString("D"),
     "--command", "cmd.exe /c exit 0",
     "--run-as", "ExistingLogin", "--raw"],
    Budget(timeout, bootstrap: true));

WinAppSandboxProtocol.RequireGuestCommandSuccess(result);
ValidateOwnership(bootstrap: true, requireState: false);
```

Only documented transient readiness failures are retried; we do not blindly
repeat `wsb connect`.

For setup cleanup, we retain verified descendant process handles. Only a correct
guest GUID acknowledgement and an empty provider inventory authorize closing
those owned clients:

```powershell
if ($guestObserved -and $ProcessTracker -and
    @($ProcessTracker.Records | Where-Object { -not $_.Process.HasExited }).Count) {
    Assert-MwbClientEmptyProvider -ProcessTracker $ProcessTracker
    Stop-MwbClientOwnedProcesses $ProcessTracker
}
```

Parent PID alone is insufficient. We verify parent lifetime, child birth time,
session, and exact trusted image path. We then require global desktop absence
and an empty provider inventory. A broker, system host, vmmem, or an uncorrelated
process is never adopted or killed by name.

Sources: [WinAppSandbox.cs][pt-adapter],
[MwbSandboxClientSetup.Common.ps1][pt-setup-common],
[EndpointSupport.ps1][pt-endpoint].

### Requested improvement

Expose readiness milestones and one bounded "ready for input and recording"
operation. Track created client resources and distinguish guest stopped, owned
client closed, and cleanup incomplete.

Document that target/agent readiness does not establish readiness of an
application launched inside the guest. A maintained example should demonstrate
a separate caller-supplied application readiness predicate without automatically
retrying a mutation, reconnecting, restarting the product or extending its
deadline.

Extend the existing passive `target snapshot` model. Inspection should not
automatically mean permission to reconnect, repair, or stop unrelated work.

## 6. Strengthen the subprocess and recording control protocol

### Observed friction

Our wrapper must drain both streams, recognize recording readiness on stderr,
stop open-ended recording through stdin newline/EOF, and close stdin for one-shot
commands. These semantics are supported but cumbersome to compose.

### What we implemented

```csharp
// Start draining both streams immediately; neither may block the other.
stdoutReader = DrainAsync(process.StandardOutput, stdout);
stderrReader = DrainAsync(process.StandardError, stderr);
```

The recording readiness loop consumes stderr rather than waiting for final
stdout:

```csharp
if (WinAppSandboxProtocol.RecordingStarted(StandardError))
{
    return;
}
```

Graceful stop and one-shot completion use different stdin behavior:

```csharp
// Stop an open-ended recording, then wait for bounded finalization.
process.StandardInput.WriteLine();
process.StandardInput.Flush();
process.StandardInput.Close();

// For a one-shot command with no input producer, close stdin before waiting.
process.StandardInput.Close();
result = Complete(timeout);
```

The surrounding wrapper bounds output size, waits, and stream closure. It treats
incomplete output as failure, rather than accepting a truncated success result.

Sources: [WinAppSandboxCommand.cs][pt-command],
[official exec stream behavior][upstream-exec].

We also corrected our own search error parser: an error envelope is not
guaranteed to contain `matchCount`. An explicit no-match result and an
infrastructure error are not interchangeable.

### Requested improvement

Expose structured progress events with operation IDs, phases, and elapsed time.
Provide an operation handle for graceful recording stop/finalization, including
in the SDK. Keep child stdout separate from infrastructure status and make
attached/detached lifetime and cancellation semantics explicit.

A versioned result discriminator such as success, no-match, busy, or error would
make typed consumers safer. Do not reinterpret an application's exit code as a
generic winapp infrastructure failure.

Offer an allowlisted, versioned diagnostic projection with operation/session
identity, phase, elapsed time, readiness and cleanup outcomes. Consumers should
not need raw private target-state files, complete UI trees or process command
lines to correlate host/guest evidence. Window titles and future provider
fields can contain user data; Windows Sandbox client command lines can contain
ephemeral credentials. Keep detailed raw diagnostics private and make the
safe-to-publish projection explicit.

## 7. Ship a maintained automation integration example

The existing `WINAPP_TARGET_STATE_ROOT` override already supports private
isolation. Make it part of a maintained session recipe rather than expecting
consumers to discover implementation details.

The example should cover creation, readiness, payload transfer, a long-running
worker, concurrent recording/UI actions, interruption, and owned cleanup.
Include standard-user CI and independently launched CLI processes.

The following illustrates the abstraction we would like. It is **proposed
pseudocode, not an existing winappcli API**:

```text
session = create_owned_sandbox(configuration, deadline, private_state_root)
session.wait_ready(exec=true, ui_input=true, recording=true)
session.push(payload)

recording = session.start_recording(output)
worker = session.start_exec(worker_command, lifetime="attached")
session.invoke(selector)       # Automatically shares this session's workflow.

recording.stop_and_finalize(deadline)
worker.cancel_and_wait(deadline)
receipt = session.close_owned(deadline)
assert receipt.guest_absent && receipt.owned_clients_closed
```

A small .NET wrapper or equivalent sample would be useful for Windows test
harnesses. This does not require duplicating the existing npm helpers.

## Attribution: do not file the wrong bug

| Observation | Attribution |
| --- | --- |
| Slow repeated firewall application-filter queries during cold preparation | Actionable winappcli implementation/performance feedback |
| No atomic expected-instance acquisition option | winappcli automation API gap |
| Recording blocked mutations with different workflow IDs | Documented behavior; integration wiring mistake plus API/discoverability feedback |
| Optional feature enabled but modern client missing | Windows image/account setup gap; needs supported setup integration |
| Leftover first-launch client process | Windows provider lifecycle behavior; owned cleanup support would simplify consumers |
| Raw `wsb start` timed out before target push or product startup | Windows provider/environment failure; root cause unproven, separate from winapp guest-agent preparation |
| MWB Connect request lost before applying peer/key configuration | PowerToys Settings RPC lifetime bug, not winapp networking |
| Queued settings save lost its requested snapshot after reload or later mutation | PowerToys persistence bug; fixed by immutable capture, not a winapp transport fix |
| Live MWB PID/Settings pipe accepted before owned TCP listeners initialized | PowerToys application-readiness/harness gap; fixed by native readiness before pairing |
| Start-menu interference with receiver foreground | Windows/harness focus problem, not a demonstrated Sandbox transport defect |
| Error formatter assumed `matchCount` existed | PowerToys parser bug; stronger result schemas would prevent similar mistakes |
| Long run overlapped an 8h54 host suspension | Environment interruption; excluded from eligible stability proof, not product/CLI execution latency |
| Slow Settings startup also occurred without UIA or responsiveness probes | PowerToys/nested-environment investigation; no demonstrated winapp UIA or recording root cause |
| Windows 10 lacks the modern Sandbox CLI backend | Documented platform boundary, not a regression |

Our earlier connection fix gave each disposable Settings RPC helper a fresh
verified pipe and awaited acknowledged New key/Connect/Reconnect requests before
closing its owned transport. The later persistence fix captures immutable
settings JSON before dispatching a save. Initial endpoint readiness now waits
for the actual owned module listeners; pairing separately requires the guest's
matching persisted key and exact peer configuration, followed by real TCP and
input/clipboard assertions. These consumer responsibilities are not something
winappcli should implement on our behalf.

## Evidence and recommended order

The latest full Release evidence is [CI build 160517346][ci-full], completed
October 14, 2026 UTC, at implementation source
`b7be098373ebb0b41a2de352f46abf00f05e719f`:

| Fresh CI job | Azure Test run | Executed/passed | Eight smoke phases | Persisted key/peer acknowledgement |
| --- | --- | --- | --- | --- |
| Win10 Legacy | `1714540311` | 11/11, no skipped cases | All passed | Both true, 271 ms |
| Win11 modern | `1714541931` | 11/11, no skipped cases | All passed | Both true, 2261 ms |

Normal fixture cleanup recorded zero errors; all four endpoint journals
confirmed settings/clipboard restoration. Owned modern creation and stop,
original-user recovery, exact firewall/task/public-trust cleanup and recording
finalization succeeded. All four nonempty smoke videos were downloaded,
container-probed and frame-decoded privately. Both jobs used matching managed/
runtime product inventories and valid same-cohort endpoint signatures.

Earlier [CI build 159456667][ci-one] and [CI build 159461630][ci-two] were
green ordered Debug smokes, not the expanded eleven-case Release suite.
CI links require authorized internal access. Recordings and private journals
remain in access-controlled evidence; no raw media, secrets or profile backups
are included in this report.

The [maintained suite README][pt-readme] describes the current operating and
recovery contract. The suite and its `Scripts`/`Payload` support files are now
consolidated under `MouseWithoutBorders.UITests`; that directory cleanup is
separate from the exact-source CI result above. All cited adapter excerpts use
the validated implementation revision, not a claim of a new run after cleanup.
Successful runs use the official v0.7.0 binary, not the earlier privately
patched preview. One fresh green CI matrix establishes the stated pilot
coverage, not repeated reliability across every image, resource profile or
physical-device configuration.

Recommended order: optimize cold preparation; expose owned session identity and
lifecycle; make cooperative recording, readiness, and operation control easy to
consume. Those changes remove more integration work than simply increasing
default timeouts.

[release]: https://github.com/microsoft/winappCli/releases/tag/v0.7.0
[upstream-firewall]: https://github.com/microsoft/winappCli/blob/v0.7.0/src/winapp-CLI/WinApp.Cli/ExecutionTargets/WindowsSandbox/WindowsSandboxBackend.cs#L1149-L1202
[upstream-workflows]: https://github.com/microsoft/winappCli/blob/v0.7.0/docs/sandbox-execution.md#L160-L199
[upstream-record]: https://github.com/microsoft/winappCli/blob/v0.7.0/src/winapp-CLI/WinApp.Cli/Commands/UiRecordCommand.cs#L66-L77
[upstream-setup]: https://github.com/microsoft/winappCli/blob/v0.7.0/src/winapp-CLI/WinApp.Cli/ExecutionTargets/WindowsSandbox/WindowsSandboxSetup.cs
[upstream-exec]: https://github.com/microsoft/winappCli/blob/v0.7.0/src/winapp-CLI/WinApp.Cli/Commands/TargetCommand.cs#L145-L237
[pt-adapter]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/WinAppSandbox.cs
[pt-protocol]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/WinAppSandboxProtocol.cs
[pt-command]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/WinAppSandboxCommand.cs
[pt-timeouts]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/SandboxTimeouts.cs
[pt-setup]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/.pipelines/Install-MwbSandboxClient.ps1
[pt-setup-common]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/.pipelines/MwbSandboxClientSetup.Common.ps1
[pt-endpoint]: https://github.com/microsoft/PowerToys/blob/b7be098373ebb0b41a2de352f46abf00f05e719f/src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/Payload/EndpointSupport.ps1
[pt-readme]: ..\..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\README.md
[ci-full]: https://dev.azure.com/microsoft/Dart/_build/results?buildId=160517346
[ci-one]: https://dev.azure.com/microsoft/Dart/_build/results?buildId=159456667
[ci-two]: https://dev.azure.com/microsoft/Dart/_build/results?buildId=159461630
