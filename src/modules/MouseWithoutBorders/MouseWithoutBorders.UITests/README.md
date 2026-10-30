# Mouse Without Borders UI tests

This is the **maintained twelve-case UI-test suite**. It uses
`Microsoft.PowerToys.UITest.Next` and unmodified official winappcli v0.7.1.
There is no separate manual Debug experiment to build or run.

## Project layout

| Location | Purpose |
|---|---|
| `MouseWithoutBorders.UITests.csproj` and root `.cs` files | Test executable, ordered two-endpoint smoke, infrastructure/recovery cases and Sandbox adapters |
| `Payload` | Packaged endpoint workers, receiver/native helpers, recovery scripts and isolated settings template |
| `Scripts\Invoke-AutonomousLocalVm.ps1` | Dispatch the suite into an existing dedicated VM using PowerShell Direct |
| `Scripts\Initialize-AutonomousHost.ps1`, `Remove-AutonomousHost.ps1` | Protected, noninteractive prerequisite setup and exact cleanup |
| `Scripts\New-MwbRuntimeArchive.ps1`, `MwbRunnerModules.ps1` | Package the coherent product/native dependency closure |
| `Scripts\MwbReadyToRun.ps1`, `MwbReadyToRunMetadata.cs` | Optional private ReadyToRun preparation and IL/metadata verification; not test-host code |
| `Scripts\MwbTestSigning.ps1` | Shared public-certificate/signature checks; also packaged into `Payload` |
| `Scripts\Test-SuitePreparation.ps1` | Non-launch parser and isolated endpoint/recovery contract checks |

Product and harness unit tests remain in the sibling
`MouseWithoutBorders.UnitTests` project. Pipeline contracts live in
`.pipelines\tests\mwb*.Tests.ps1`. The former manual launchers and historical
continuation plan are retired; repository history retains them.

## Supported operating contract

This is a **default-enabled, dedicated-VM, x64 user-desktop pilot using one
same-source Release test artifact**, not complete MWB module or physical-device sign-off.
The outer Windows test VM is the host peer; its nested Windows Sandbox is the
guest peer. The physical development machine is never a peer.

| Boundary | Required contract |
|---|---|
| Windows 10 x64 | Legacy Sandbox backend; locally demonstrated on build 19045.6456 |
| Windows 11 x64 | 24H2+ modern backend; locally demonstrated on build 26200.9457 |
| Local default resources | Eight vCPUs / 16 GiB static RAM; nested virtualization enabled |
| Modern client | Healthy `MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy` registration for the actual standard test user; demonstrated version 0.8.107.0 |
| UI/Sandbox CLI | Official winappcli v0.7.1; release/native-closure hashes checked against `.pipelines\winapp-cli\release.json` |
| Execution identity | Unlocked English standard-user Default desktop on the outer VM, no existing PowerToys or Sandbox; run-owned guest desktop |
| Payload | Same coherent self-contained Release runtime archive on both endpoints |
| Privileged boundary | Separate protected setup/cleanup; no test-side elevation, feature enablement, reboot or GPO edit |

The disposable baseline explicitly sets the hidden JSON property
`properties.AllowNonConsoleSessions.value=true` before Runner startup. The
product default and missing-value behavior remain **false**. There is no
Settings UI/configure-CLI control; restart MWB after changing the property.
Shipping Release ignores both JSON and environment opt-in. Only the dedicated
Release UI-test variant honors this JSON property; its assembly metadata records
the test-build marker and checkout revision. Service/System, logon/screensaver,
locked/secure-desktop, active/unlocked input and authentication restrictions
remain intact. See the [module documentation](..\..\..\..\doc\devdocs\modules\mousewithoutborders.md).

Release Runner eagerly loads all module interfaces before applying its enabled
map. Runtime packaging derives that native DLL list from current Runner source
and includes its import closure even though only MWB is enabled. Missing
dependencies fail packaging/provenance validation, not a dismissible native
dialog or a Debug fallback.

Release retains Runner/Settings and MWB/Settings peer authentication. The
existing disposable companion-signing mechanism signs private Runner, Settings,
MWB, helper and Quick Access executable copies as one cohort. Only executable
signatures may change; managed files and published build outputs remain
unchanged after the isolated MWB test variant is packaged. Both endpoints use the same signed archive. Public certificate trust
is installed before product startup and removed by exact ownership; private
keys never enter a payload.

## Validated behavioral baseline

[CI build 160517346](https://dev.azure.com/microsoft/Dart/_build/results?buildId=160517346)
succeeded at implementation source
`b7be098373ebb0b41a2de352f46abf00f05e719f`, before the directory consolidation.
Win10 Legacy run `1714540311` and Win11 modern run `1714541931` each executed
and passed **11/11 cases and all eight smoke phases**. Matching persisted
key/peer acknowledgements took 271/2261 ms. Both endpoints restored settings
and clipboard, normal cleanup had zero errors, and original-user recovery,
firewall/trust cleanup and recording finalization succeeded. Four nonempty
smoke videos were downloaded and decoded privately; same-source runtime
manifests and valid matching endpoint signatures were verified.

Eligible cold local Win10 `localvm-20261014-142300-3e611e1c` passed 11/11
without host suspension. Local Win11 again failed native Sandbox creation
before product startup; an explicit fresh-CI infrastructure exception was used,
not a local Win11 pass. A local evidence-capture timeout remains failed, and an
otherwise passing repeat overlapping host suspension is excluded from eligible
cold-stability proof. Historical diagnostics remain in private evidence and git
history rather than a second runnable experiment.

Win10 pressure at two vCPUs / 8 GiB passed. Win11 pressure at two and four
vCPUs / 8 GiB failed native cold Sandbox creation before PowerToys; it remains
blocked. The generic one-vCPU / 4-GiB profile is below Windows Sandbox's
two-core minimum. Do not silently reduce resources or substitute a warm retry
for cold proof.

ARM64 live MWB, physical two-PC networking/hardware, installed per-user/machine
paths, installer/upgrade/uninstall, service/System, secure/locked desktop and
the remaining manual module checklist are **outside this pilot's sign-off**.
The hidden property does not make older official binaries support this flow.

## Test coverage and readiness

Run the **entire executable without a filter** for sign-off. The CI result gate
rejects stale, empty, filtered, failed or skipped TRX evidence.
`AutonomousSandboxTests.AutonomousSandboxSmoke` is one ordered, nonparallel
scenario using the Next framework directly, not the generic single-peer
`UITestBase` lifecycle.

The smoke requires protected provisioning identity, correlated Sandbox desktop
readiness, matching configured endpoints, real Settings **New key/Connect**,
bidirectional owned transport, local keyboard isolation, remote keyboard and
one physical mouse click, and clipboard-off isolation plus both transfer
directions. Six infrastructure cases and these four recovery cases run in the
same unfiltered suite:

| Recovery case | Required observation |
|---|---|
| `ControllerExitBeforePairingRestoresOriginalSettings` | Real endpoint startup, retained controller exit, exact 45-second lease expiry and original settings restoration |
| `KilledClipboardOwnerRequiresBaselineReset` | Real synthetic clipboard publication, exact worker/controller termination and only the expected lost-snapshot/reset-required failure |
| `AbortedSandboxStartupRefusesUnownedInstance` | Owned creation aborted before acknowledgement; another run refuses the existing identity without adoption, replacement or unowned stop |
| `StaleRunDirectoryRefusalPreservesRecoveryJournals` | A nonempty prior run root is refused without changing bytes, publication markers, filenames or prior-owner attachments |

Initial startup requires two stable samples of the initialized Settings window
and both native MWB listeners (15100/15101) on the same exact owned MWB PID.
SettingsSync is published after pairing-key initialization, but the pipe alone
does not prove the main form or native listeners are ready. `mwb-readiness.json`
records this boundary without UIA or secret data. The pilot keeps service mode
off; it does not sign off encrypted service-mode pairing or administrator-only
key disclosure.

Settings starts through Runner's supported `--open-settings=MouseWithoutBorders`
deep link. Native maximized geometry is checked before targeted UIA waits for
`MouseWithoutBordersShowConnectFieldsButton` and both enabled peer/key fields.
The actual Connect command is submitted once, then must persist the matching
key and peer. No Connect retry, post-pairing settings repair, signing/encryption
bypass, overlay suppression or weaker input/clipboard assertion is permitted.
This tests pairing, not Home-to-module navigation or startup performance.

Both owned MWB PIDs must have actual Established peer connections and the
expected F1-F4 routing slots. A verified settings mapping alone is not transport
proof. Input targets only the owned receiver, never the Sandbox viewer.
Start-menu recovery requires a visible, DWM-uncloaked window with the verified
same-session OS launcher identity. A closed shell window can retain Win32
visibility and regain foreground after a receiver closes; a cloaked window is
not sent Escape. The single-Escape rule and three-second dismissal deadline
remain unchanged.
The mouse probe requires two unobstructed native hits, correct foreground/no
capture, one checked physical click and exact guest-only effects. If a cursor
overlay obstructs a hit, the real mouse moves within safe receiver positions.
Clipboard sources publish distinct synthetic tokens and acknowledge local
observation before negative/transfer assertions; expected destination payloads
are never injected through the control channel.
Publication retries the same token when Windows temporarily holds the clipboard;
the independent 15-second transfer assertions are unchanged.
Observations report `CLIPBRD_E_CANT_OPEN` as explicitly unavailable, with its
HRESULT and no digest, rather than as empty clipboard data. Transfer polling
keeps its original 15-second deadline and records busy observations. The
sharing-off negative control requires readable data before each assertion;
other clipboard errors still fail the endpoint request.
Text observations hold one native clipboard lock while reading Unicode data;
OLE reads can otherwise return empty data when another thread owns the clipboard.

## Backends and deadlines

Windows 10 uses `WindowsSandbox.exe`, a hardened `.wsb`, mapped endpoint
channels and native viewer ownership. It never invokes unsupported
`--on sandbox` targeting. Windows 11 precreates an exclusive provider GUID,
verifies official winapp target adoption, and uses target push/attached exec
and guest-native capture. No silent backend or local fallback is allowed.

The modern client must already be registered for the standard test user.
Provider ID, epoch, CLI path/hash and private target state are checked before
and after operations; v0.7.1 has no atomic attach-only acquisition fence, so
use a dedicated single-Sandbox VM. Its authenticated agent transport is
separate from MWB's ports. Only child processes receive run-derived
`WINAPP_TARGET_STATE_ROOT` / `WINAPP_UI_WORKFLOW_ID` values; default-profile
state and parent/user/machine environments are untouched.

The guest clicks the visible Connect row opener once after its readiness wait,
then waits for both input fields before typing. This avoids a synchronous
InvokePattern call for the row expansion; the actual Connect submission and
persisted pairing acknowledgement remain unchanged.

| Deadline | Legacy | Modern |
|---|---|---|
| Guest bootstrap | 20 minutes | 35 minutes, including at most 15 minutes for provider creation |
| Endpoint native Settings/MWB readiness | 15 minutes | 15 minutes |
| Enclosing endpoint Start request | 20 minutes | 20 minutes |
| Read-only evidence request, per endpoint | 3 minutes | 3 minutes |
| Fixture/worker hard run deadline | 65 minutes | 90 minutes |
| Local suite/controller | 70/85 minutes | 95/110 minutes |
| CI interactive suite / outer Run step | 70/103 minutes | 100/103 minutes |
| Lease freshness | 45 seconds, no grace | 45 seconds, no grace |

Guest/host StopPeers allow 300/90 seconds. Recovery startup fits the same
20-minute Start request inside a 23-minute envelope; the actual recovery
script remains bounded to three minutes. Legacy abort-test viewer readiness
allows three minutes. UIA ceilings remain 90/180 seconds, clipboard transfer
15 seconds and mouse settling 20 seconds. These are readiness ceilings, not
fixed sleeps or claims that startup slowness has been solved.
Evidence collection has its own bounded request budget because it captures a
desktop image, reads Application events and copies redacted logs; it does not
use the 40-second default for ordinary endpoint commands.

## Build and local execution

Build from the repository root:

```powershell
.\tools\build\build.cmd -Path .\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests -Platform x64 -Configuration Release
```

Build the sibling unit project separately before running its executable.
After building the ordinary Release product, build the isolated test variant:

```powershell
$revision = git rev-parse HEAD
.\tools\build\build.cmd -Path .\src\modules\MouseWithoutBorders\App -Platform x64 -Configuration Release /p:BuildProjectReferences=false /p:MwbUiTestBuild=true /p:MwbUiTestSourceRevision=$revision
```

This writes only `artifacts\mwb-ui-test\x64\Release\bin` and its isolated
intermediates. Pass that directory's `PowerToys.MouseWithoutBorders.dll` to
`New-MwbRuntimeArchive.ps1 -MwbUiTestBinaryPath <path> -SourceRevision $revision`
(or the CI bundle script). The packager rejects unmarked or mismatched source
binaries and fingerprints the replacement before optional ReadyToRun compilation.
Record any uncommitted source diff alongside local payload hashes.
Use the repository build tools; do not use `dotnet test`. The UI executable is
under `x64\Release\tests\MouseWithoutBorders.UITests\net10.0-windows10.0.26100.0`.
Preserve its `Payload` subdirectory. The following non-launch check works in
Windows PowerShell 5.1 and never starts PowerToys/Sandbox or changes live settings:

```powershell
powershell.exe -NoProfile -File .\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Scripts\Test-SuitePreparation.ps1
```

Build/package on the development host; execute product/UI only inside the exact
owned VMs. Use the `ui-tests-local-vm` skill for prerequisites, DPAPI credentials,
standard-user dispatch and independent Win10/Win11 baselines.
`Scripts\New-MwbRuntimeArchive.ps1` prepares the managed/native/WinUI resource
closure. Optional `-ReadyToRun -Crossgen2Path <matching-native-compiler>` modifies
only private staging and verifies configuration, IL/MVID and duplicate copies.
Supply the same prepared product archive to both endpoints.

The local wrapper performs protected provisioning, standard-user test dispatch,
original-user recovery and exact privileged cleanup. It does not enable nesting,
restore checkpoints implicitly or change credentials.

```powershell
.\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Scripts\Invoke-AutonomousLocalVm.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -ConfigurationPath C:\PowerToysUiTestVm\vm.config.psd1 `
  -CredentialPath "$env:LOCALAPPDATA\PowerToysUiTestVm\admin.win11.credential.xml" `
  -ExchangeRoot C:\PowerToysUiTestVm\shared\PowerToysUiTests\MouseWithoutBorders `
  -Configuration Release -Platform x64Win11 `
  -SandboxWinAppArchive winappcli-official-0.7.1-x64-runtime.zip `
  -ProductArchive powertoys-runtime.zip -GuestRuntimeArchive powertoys-runtime.zip `
  -TestsArchive ui-tests.zip -ReuseStagedPayload -PlanOnly
```

Inspect the exact source/hash, empty filter, platform, configuration and resource
profile in the plan/request, then repeat without `-PlanOnly`.
For Win10, select its independent VM/config/credential and `-Platform x64Win10`;
omit the modern Sandbox archive. The wrapper's `Auto` backend selects correctly.
Use the official archive's three verified runtime files (`winapp.exe`,
`libSkiaSharp.dll`, `libHarfBuzzSharp.dll`), not PDBs or a patched CLI. A derived
portable ZIP has its own hash, distinct from the official release asset hash.

For an already-provisioned standard-user VM desktop, the executable can run
directly with `POWERTOYS_INSTALL_DIR`, `WINAPP_CLI_PATH` and optional
`POWERTOYS_MWB_PROVISIONING` pointing at its protected marker. A specified
`POWERTOYS_MWB_RUN_ROOT` must be new/empty; otherwise run evidence is placed
beside the MTP run directory, outside its disposable deployment tree.
Never use a focused filter as full-suite evidence.

## CI wiring

MWB preparation and execution are derived from the selected modules, without a
separate enable/disable switch. Current-source `buildNow`/`buildNowSlim` supports
all, explicit, mixed and affected module selections: ordinary suites retain their
dispatcher/installer path, MWB runs once through its two-endpoint flow. There is
one shared Release build, not a second Debug configuration.
Only MWB is skipped on ARM64 with `Arm64SandboxImageUnavailable`; other selected
suites remain normal. To omit MWB, select the other desired modules explicitly.
Explicit MWB requests against official/installed sources are provenance blockers.
Implicit all-modules official runs skip MWB with a warning without blocking the
other suites. The automation pipeline defaults to `buildNow`; its build-source
label identifies `latestMainOfficialBuild` and `specificBuildId` as unsupported
for MWB.
Remove the former `mwbSandboxExperiment` parameter from saved queue/REST requests;
the selected modules now determine whether MWB runs.

`.pipelines\mwb\New-MwbSandboxCiArtifact.ps1` produces the verified runtime/tool
bundle with the isolated MWB UI-test variant, outside shipping outputs. Affected-PR
builds prepare it only when the resolver selects MWB.
`.pipelines\mwb\mwbSandboxExperiment.ps1` dispatches protected preparation,
the complete suite, recovery and cleanup using this project's `Scripts` and
`Payload`. The build artifact and installer are not modified by test signing.
The shared **Run UI Tests** step executes MWB once, after the other selected
suites, in its own log group. Setup and execution use isolated child processes
with the original phase deadlines, preserving the ordinary suites' environment
and the Limited-user desktop boundary. A failed suite does not suppress MWB.
The always-running **Restore UI-test prerequisites** step independently attempts
MWB recovery, privileged cleanup, allowlisted diagnostic collection and Light
Switch restoration, including after cancellation. Cleanup failures remain failures.

MWB TRX and test attachments use the existing **Publish UI Test Results** step.
Selection and ARM64 skip reasons remain in the job log, not separate artifacts.
Successful jobs do not upload prerequisite artifacts. **Publish UI-test
diagnostics** uploads the allowlisted prerequisite report only when selected MWB
execution or cleanup is incomplete, or the job fails or is canceled, including
setup failures before TRX exists. Raw protected logs and authentication state
remain excluded.
Use the `ui-tests-pipeline-ci` skill for exact pushed-source validation;
monitor long CI runs at 1800-second intervals.

### PowerShell infrastructure checks

MWB pipeline helpers live under `.pipelines\mwb`; the shared winapp CLI release
helper remains at `.pipelines\WinAppCli.Common.ps1`. Full builds with test
compilation enabled and UI-tests-only builds run the MWB and shared CLI Pester
suites before compilation, publish NUnit results, and fail on empty, failed or
skipped selected tests. The gate uses the existing Pester 3.4.0 framework under
PowerShell 7 and reports a missing module as a prerequisite failure.

The fast gate excludes the `NativeCompiler` integration group, which requires
explicit Crossgen2 and runtime-reference paths. Those tests remain available for
opt-in compiler validation; they are not counted as passing gate tests.

```powershell
$tests = @(Get-ChildItem .pipelines\tests\mwb*.Tests.ps1) +
    (Get-Item .pipelines\tests\winappCliRelease.Tests.ps1)
Invoke-Pester -Script $tests.FullName -ExcludeTag NativeCompiler -PassThru -Quiet
```

## Ownership, evidence and recovery

Protected setup never requests UAC, changes Windows features/groups/policy or
exposes an arbitrary-command service. It fingerprints the executable/library,
archive, test-user SID, run and rule identities in an administrator-protected
marker. That marker may be `PreparingTrust`, then `WaitingForSandbox` until the
actual inner Default Switch appears; MWB starts only after `Ready`. The guest
address must belong to that subnet and its gateway must exactly match the
provisioned host address. Network changes fail before product startup.

The trusted provisioning launcher has 90 seconds to acknowledge startup in its
protected run directory. Payload validation, fingerprinting and trust preparation
then have a separate, non-renewable 300-second deadline. The startup acknowledgement
is not an ownership or readiness marker: CI and local VM dispatch still require the
complete verified provisioning marker before starting the suite. All payload hashes,
signature checks and exact cleanup remain required. The worker's 22-minute execution
limit accommodates this preparation plus the unchanged 15-minute inner-network
deadline; UI-test deadlines are unchanged.

Guest bootstrap preserves its normal elevated `WDAGUtilityAccount` user desktop,
not service mode. Its firewall rule is exact-program/ports/host-address scoped.
Sandbox clipboard/audio/video/printer redirection is disabled. Only read-only
product/worker/tools/request and separate writable output folders are mapped;
host profile backups are never mapped.

Immutable committed leases are published by two parent-bound, identity-recorded
processes. Workers consume the latest generation, never replay old snapshots or
re-date a lease. A stale observation gets one newest-generation read before
enforcing the unchanged 45-second threshold. Publishers cannot outlive the
test process or hard run deadline. Publisher status in the controller-local results
directory is atomically replaced, so concurrent readiness readers cannot observe
a truncated update; mapped lease generations remain immutable. Recovery validates PID, parent, session,
image and exact start time before stopping anything; never kill by name or
stop shared virtualization services or idle modern brokers.

TRX evidence includes phases, topology, transport, receiver observations,
readiness, signatures, private-access recordings and restoration journals.
Run-scoped logs redact key/clipboard diagnostics. `ui-command-timings.json`
retains command kinds/timing/owned process readiness, not arguments, UI trees,
workflow/authentication tokens or raw sensitive stderr. Clipboard diagnostics
contain only typed flags and bounded event classifications. Native failure
context and composed desktop images are captured before teardown without
attaching another UIA client.

Recording manifests explicitly distinguish Started, Completed, Available and
errors. Keep nonempty clips even after a finalization warning, but never treat
missing/failing capture as a silent pass. Failures before MSTest starts have
protected provisioning logs, not a fixture recording. Raw provisioning output,
profile backups and bootstrap control state are not public attachments.
Sandbox command lines must never be exported: Windows 10 embeds an ephemeral
password in them. Videos can show disposable pairing keys and must remain in
the existing access-controlled Azure Test/pipeline store or private local
archive; never post them publicly. Apply normal build retention.

Control state lives under
`%LOCALAPPDATA%\Microsoft\PowerToysUiTestControl\<RunId>`; original settings
backups live under `PowerToysUiTestRecovery\<RunId>`, outside mappings/results.
Peers stop before restoring exact original global/OOBE/module bytes or absence
and the in-memory clipboard snapshot. Hash-check backups before/after restore.
No original clipboard contents are written to disk.

On interruption, allow original leases to expire, preserve ownership journals,
then dispatch `Payload\Recover-Host.ps1 -JournalPath <run-root>\cleanup-journal.json`
as the original **Limited interactive user**, never administrator/SYSTEM.
Its default marker is
`C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json`.
Use a five-minute controller envelope around the three-minute recovery script.
It revalidates protected ownership, original session/machine, parent chains,
owned provider GUID/epoch and exact private target paths. It does not touch
official default-profile state or remove privileged firewall rules.

`recovery-result.json` returns zero only for complete recovery. A killed
clipboard owner can lose the only original snapshot: nonzero `Incomplete`,
false restoration flags and `RequiresBaselineReset=true` must remain failed.
Do not reconstruct clipboard data, invent restored settings or retry warm.
Preserve/reconcile private creation state, then reset the exact verified clean
nested-Sandbox baseline or use a fresh dedicated VM. Do not blindly restore an
obsolete generic checkpoint.

Always run `Scripts\Remove-AutonomousHost.ps1` through the protected controller
to remove only recorded rules/tasks/new public trust, including on failure.
`Cleaned` means cleanup completed, not that the test passed. Require all eleven
TRX cases, both eight-phase smokes, exact source/payload identities, media and
clean restoration for pilot success. Expected injected recovery failures have
their own asserted verdicts; unrelated failures cannot satisfy them.
