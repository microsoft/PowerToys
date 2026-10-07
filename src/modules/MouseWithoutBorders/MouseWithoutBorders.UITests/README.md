# Autonomous MWB nested-Sandbox Debug pilot

## Supported operating contract

This is a **default-off, dedicated-VM, x64 Debug pilot**, not complete MWB module
or physical-device sign-off. CI selects exactly
`mwbSandboxExperiment=true`, `buildSource=buildNow`,
`buildPlatforms=[x64]`, `uiTestModules=[MouseWithoutBorders.UITests]`,
and `useLatestWebView2=false`. The selection expands to fresh Win10 and Win11
jobs; invalid or unvalidated selections fail before host mutation.
Run the **entire test executable without a filter** for sign-off. The pipeline
requires the current job's TRX to contain all required cases, with every test
executed and passed. A green ordered smoke alone, an empty report, or a skipped
infrastructure case is not full-suite evidence.

| Boundary | Required contract |
|---|---|
| Windows 10 x64 | Legacy Sandbox backend; locally demonstrated on build 19045.6456 |
| Windows 11 x64 | 24H2+ modern backend; locally demonstrated on build 26200.9457 |
| Retained local resource profiles | Win10: four vCPUs/24 GB static RAM; Win11: four vCPUs/8 GB static RAM; nested virtualization enabled |
| Modern client | `MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy`, healthy and registered to the actual interactive test user; demonstrated version 0.8.107.0 |
| UI/Sandbox CLI | Unmodified official winappcli v0.7.0; release and native-closure hashes checked using the shared repository pin |
| Execution identity | Unlocked English standard-user Default desktop on L1, no pre-existing PowerToys or Sandbox; run-owned guest desktop, not service mode |
| Payload | Same coherent self-contained Debug runtime for both endpoints; private ReadyToRun compilation does not change IL/MVID or bypass product checks |
| Privileged boundary | Separate protected setup/cleanup; no feature enablement, reboot, GPO edit, or test-side elevation |

Debug native binaries using the hybrid CRT import **`ucrtbased.dll`**, which is
not an inbox dependency on a fresh Sandbox. CI takes this sidecar from the
installed Windows SDK version pinned by `Cpp.Build.props`, verifies its native
architecture and Microsoft signature, and fingerprints/copies it only into the
private runtime stage. The original build outputs are not modified. Manual
packaging can pass `-DebugUcrtPath` explicitly; a missing required debug UCRT is
a packaging error, not a reason to wait longer for an already-exited Runner.
This SDK debug dependency is internal test payload, not a Release/installer
redistribution.

The one-vCPU/4-GB generic `Constrained` profile is **not signed off for nested
Sandbox**. The resource profiles above are the demonstrated pilot baselines,
not measurements establishing the smallest possible configuration. Do not
silently reduce resources or claim a smaller profile passed. Fresh CI images
provide the clean-profile evidence; retained local VMs provide iteration and
failure-injection evidence.

The retained Win11 four-vCPU/8-GB profile has an observed **first-creation-after-
boot limitation**: the modern client's initial `wsb start` can exhaust the
unchanged ten-minute provider deadline before guest acknowledgement. A cold VM
reset reproduced it, while later real owned creation/abort/stop cycles completed
within a minute. Preserve those failed runs and their `Incomplete` recovery
verdict; neither a later readiness probe nor a warm full-suite pass retroactively
signs off that cold start. This local profile is not a cold-start guarantee.
Fresh CI sign-off must independently pass after the pipeline's client setup,
without reducing assertions, extending deadlines or silently priming a failed
smoke.

| Deadline | Legacy | Modern |
|---|---|---|
| Guest bootstrap, including provider setup/transfer | 15 minutes | 35 minutes, including at most 10 minutes of provider start |
| Fixture/worker hard run deadline | 40 minutes | 70 minutes |
| Local suite/controller envelope | 45/60 minutes | 75/90 minutes |
| CI interactive suite/outer Run step | 45/83 minutes | 80/83 minutes |
| Lease freshness | 45 seconds, no grace | 45 seconds, no grace |

The modern-client setup step precedes protected runtime preparation and is
bounded to 12 minutes. Owned external recovery and privileged cleanup always
run, even when setup or the suite fails. Their success cannot turn a failed
test into a pass. Do not retry a whole suite in place or extend an individual
operation past the enclosing hard deadline.

### Recovery, evidence and reset runbook

1. Record the exact source SHA, product/test/tool hashes, run/job GUID and OS
   before starting. Use the local wrapper's `-PlanOnly` first; never launch the
   fixture on the physical development host.
2. Run once under the protected setup marker as the original Limited user.
   Preserve full-suite TRX, phase/transport/pairing booleans, prerequisite and
   recording manifests, sanitized logs, and `recovery-result.json`.
3. After a failure or controller interruption, let the original parent-bound
   leases expire and run `Payload\Recover-Host.ps1` through the original user's
   Limited interactive task. Revalidate recorded PID/start/image/parent/session
   and the exact provider GUID/epoch; never kill by name or stop a shared
   virtualization service. Run the elevated exact-rule/task cleanup separately.
4. Require zero cleanup/export errors and finalized nonempty videos where
   recording started. Preserve the original failure even when recovery passes.
   If recovery reports `RequiresBaselineReset`, stop: clipboard data was held
   only in memory and cannot be reconstructed. Do not rerun on that profile.
5. Retire the failed CI agent. Locally, use a freshly provisioned dedicated VM
   or an explicitly verified clean nested-Sandbox checkpoint with current
   credentials, feature/client registration, resource profile and no owned
   leftovers. Do not restore an old generic `provisioned-baseline` that predates
   nested setup or credential servicing.

**Artifact access/retention:** full recordings are internal, access-controlled
diagnostics and may display disposable pairing keys. Keep them in the existing
Azure Test/pipeline artifact store under its build-retention policy, or the
private local run archive; do not attach them to a public PR/issue or mirror them
into a public file service. There is no additional indefinite repository copy.
Delete only explicitly identified run-owned local artifacts when no longer
needed. Never publish private control channels, bootstrap authentication, profile
backups, original clipboard content, raw process dumps or Sandbox command lines.
Public summaries may contain verdicts, timings, source SHAs and internal build
links, not raw internal evidence.

Release/installed builds, ARM64 live execution, service/secure-desktop behavior,
physical two-PC networking and the remainder of the manual module checklist are
**outside this pilot's sign-off**. ARM64 payload compilation is retained for
future image work; it is not evidence of a working ARM64 Sandbox.

### Required recovery cases

The unfiltered suite contains **eleven cases**, including the ordered smoke and
these four `MwbRecovery` cases. None is skipped or hidden behind a smoke-only
filter.

| Case | Required observation |
|---|---|
| `ControllerExitBeforePairingRestoresOriginalSettings` | Real Runner/MWB/Settings startup, retained controller exit, unchanged 45-second lease expiry, exact original settings restored and only recorded processes stopped |
| `KilledClipboardOwnerRequiresBaselineReset` | Real synthetic clipboard publication, exact worker/controller termination, recovery exits nonzero with only the expected lost-snapshot error and `RequiresBaselineReset=true` |
| `AbortedSandboxStartupRefusesUnownedInstance` | Actual owned creation aborted before guest acknowledgement; a different run refuses the existing GUID/PID without adoption, replacement or unowned stop |
| `StaleRunDirectoryRefusalPreservesRecoveryJournals` | Fixture preflight/finally/cleanup refuses a nonempty prior run root without changing any original bytes, publication markers or file names |

Expected injected failures are asserted explicitly; an unrelated startup or
cleanup error cannot satisfy a recovery case. They do not change a normal
smoke failure into a pass.

The killed-worker case has a separate **test-only in-memory clipboard guard**,
using the receiver's supported `Host` snapshotting role, to isolate its deliberate
loss from subsequent cases. A known synthetic baseline must be restored after
fixture disposal; an outer guard then restores the actual original clipboard in
`finally`. No original clipboard payload or digest is published. This isolation
is not available to an interrupted real run: `Recover-Host.ps1` still reports
`Incomplete`, returns nonzero and requires baseline reset when the original
worker snapshot is lost.

## Recorded evidence

**Final x64 Debug pilot: the complete eleven-case suite passed on both fresh CI
jobs in [build 159677767](https://dev.azure.com/microsoft/Dart/_build/results?buildId=159677767).**
The build completed successfully on October 7, 2026 UTC at source
`078299d71e685f445332bf44057cf52565405916`.

| Fresh CI job | Executed/passed | Smoke duration | Persisted key/peer acknowledgement |
|---|---|---|---|
| Win10 Legacy, test run 1706270707 | 11/11, no skipped cases | 13m 46s | Both true, 640 ms |
| Win11 modern, test run 1706269035 | 11/11, no skipped cases | 7m 16s | Both true, 685 ms |

Both jobs passed all eight ordered smoke phases, including physical input and
clipboard negatives/both transfer directions. Each asserted real 45-second
controller-loss recovery, the exact incomplete/lost-clipboard reset verdict,
owned creation-abort/unowned-instance refusal, and byte-for-byte stale-journal
preservation with zero prior-owner attachments. Normal fixture and external
recovery were clean; client setup, exact-rule cleanup and artifact publication
succeeded. Recording manifests report capture/finalization available and complete.

The first expanded fresh-CI attempt,
[159647941](https://dev.azure.com/microsoft/Dart/_build/results?buildId=159647941),
executed all eleven cases on each OS but passed ten: only guest smoke startup
failed. Inspection of the actual native Runner imports and failed runtime bundle
identified the missing SDK `ucrtbased.dll` dependency, not a Settings timeout.
Adding only that signed SDK file to the actual CI runtime made the full Win10
suite pass locally; the private-staging fix then passed the fresh CI matrix above
without changing deadlines or assertions. Four of the six authorized attempts
were used, including the two earlier green ordered smokes.

The final local eleven-case backing runs were Win10
`localvm-20261006-165358-e25fe5be` and Win11
`localvm-20261006-183216-1bf7c9f2`, with the documented provider-ready retained
Win11 profile. The attachment-observer follow-up passed on both OSes; the actual
corrected CI runtime passed locally in `localvm-20261006-215628-5275908c`.
These local results do not replace the independent fresh CI evidence or erase
the retained-profile cold-start limitation above.

**Historical seven-test baseline: official winappcli v0.7.0 passed the full seven-test pilot on both
Windows 10 and Windows 11 on October 2, 2026.**
The Start-menu focus fix adds three infrastructure regressions. Windows 11 run
`localvm-20261002-221219-4822b84c` passed 7/7 tests and all eight
ordered smoke phases in 34 minutes 38 seconds, including real pairing, physical
remote input, clipboard-off isolation and both clipboard transfer directions.
Both recordings finalized, settings and clipboard were restored, exact-GUID
cleanup and external recovery succeeded, and evidence export had no errors.
The retained VM used four vCPUs and 8 GB RAM; this is not a fresh-profile or
physical-machine sign-off. See the bounded startup/cooperative recording section
below for the measured delays and lifecycle changes.

**The ordered CI smoke is green on both OSes.** After the Settings IPC lifetime
fix, [build 159456667](https://dev.azure.com/microsoft/Dart/_build/results?buildId=159456667)
and a fresh-agent confirmation
[build 159461630](https://dev.azure.com/microsoft/Dart/_build/results?buildId=159461630)
both succeeded at source `90f5187c47af851fa6727fb763f5b80ba28bb187`.
Each executed one Win10 and one Win11 smoke, with all eight phases passing,
matching persisted pairing key/peer acknowledgement, no Settings
`ConnectionLostException`, finalized recordings and clean recovery.
The same refined local payload passed both unfiltered seven-test suites:
Win10 `localvm-20261005-024048-1f7d3651` and
Win11 `localvm-20261005-030720-3e958cce`.
Two of the six newly authorized CI attempts were needed.

The preceding six-run October 2 stabilization cycle ended
with [build 159338297](https://dev.azure.com/microsoft/Dart/_build/results?buildId=159338297),
source `07ef8f8bf88bb9c6b6380c313c2fb83b8b2358d5`. The build and Win10 smoke
passed. The new Win11 setup step installed the modern client from an absent
package under the actual Limited test user, verified version 0.8.107.0, and
completed owned cleanup with the controller's package proof verified.
Modern provider start took 10.9 seconds and target push 76.5 seconds in that run.
The Win11 smoke passed startup and the Settings pairing actions, then exhausted
the 90-second transport-readiness wait: both owned MWB processes were listening,
but neither had an established peer connection or the required routing slots.
Cleanup and external recovery passed. This remaining pairing/transport failure
is distinct from client installation, the resolved recording workflow contention,
and the locally reproduced/fixed Start-menu focus blocker.

The October 5 investigation identified the controlling failure in that run's
guest Settings log: `MouseWithoutBordersPage.Connect` lost its JSON-RPC
connection before the request completed, followed by repeated failures in status
polling. The guest retained its previous key and own-only machine matrix; its
MWB process never attempted an outbound peer connection. A deterministic
poll-then-Connect unit reproduction failed with the same exception.
Settings now uses one freshly verified pipe per disposable RPC helper and closes
the helper's pipe explicitly rather than reusing a transport that an earlier
RPC reader is still closing. State-changing calls remain acknowledged and are
never automatically replayed.

The pairing phase also now requires the guest's persisted key to match the
requested host key and its machine matrix to contain the exact peer before
transport checks begin. `pairing-before.json` and `pairing-acknowledgement.json`
publish only boolean comparisons, stage and timing, never keys or their hashes.
An invoked button is no longer treated as proof of accepted pairing. The
90-second owned-transport assertion remains unchanged.

The unfiltered Windows 10 regression with the same test payload
(`localvm-20261002-214643-9264f3c0`) passed 7/7 tests and all eight ordered phases
in 22 minutes 36 seconds on its retained four-vCPU/24-GB VM:
real New key/Connect, bidirectional owned TCP transport, local-input isolation,
remote keyboard/mouse, clipboard-off isolation, clipboard transfer both ways,
and cleanup. Both endpoints restored settings and clipboard; both recordings
finalized, and evidence export had no errors. The product and guest use the same
R2R runtime
(`mwb-guest-runtime-win10-r2r.zip`, SHA-256
`330575082E61412C790CEB0E0CD6CBC4A2120F5E54DA052A35410B7E0F973259`) on both
endpoints. This is the selected ordered smoke scenario, not full module,
Release, service, secure-desktop, or physical-machine sign-off.

The earlier official Windows 11 run (`localvm-20260926-132913-04f4638e`) passed the three
infrastructure tests, but the smoke timed out in the initial target push at the
unchanged 15-minute bootstrap deadline (778 seconds remained after provider
startup). The released binary wrote schema-1 state with the exact adopted run
GUID under the private override; no default-profile target state was created.
It never reached the authenticated bootstrap epoch or MWB worker. The live
owned provider child was still executing the release's per-rule
`Get-NetFirewallApplicationFilter` loop after more than ten minutes. Exact-GUID
stop and private-state cleanup completed, with clean external recovery and
no export errors.

A cold Windows 11 retry (`localvm-20260926-135157-b16d1cb7`) instead timed out
in `wsb start` after its existing 300-second limit, before target bootstrap.
The harness stopped that exact GUID and marked cleanup complete. External
recovery conservatively reports interrupted creation because creation was never
confirmed; that guard is not weakened. The earlier private-preview Windows 11
4/4 baseline below does **not** establish an official v0.7.0 pass.

The stability fixes retain real Settings actions and the existing safety gates:
state-changing Settings RPCs await acknowledgement before their channel closes;
host and guest leases have independent owned publishers; and guest bootstrap
finishes before the host worker starts. Discovery and transport observation use
native APIs rather than blocking Sandbox UIA or cold NetTCPIP/CIM queries.
Watchdogs, scoped firewall rules and peer verification remain intact.

Desktop and separate Sandbox-viewer recordings, phase evidence, transport
snapshots and sanitized endpoint logs survive successful runs. Windows 11
Sandbox enablement **and current-test-user modern client registration** remain
separate image prerequisites; updating winappcli does not install either.

The gated CI pilot's elevated `Prepare` step writes
`prerequisite-admin.json` (published in the `mwb-prerequisite-report-<job>`
pipeline artifact by `steps-mwb-sandbox-experiment.yml`) before any prerequisite
can throw and before the protected run root exists, so a registration gap is
evidenced even when the pilot never reaches the build artifact, feature, or
backend checks. It records: the OS architecture/build and
`Containers-DisposableClientVM` feature state; an all-users
`MicrosoftWindows.WindowsSandbox` package inventory and the matching
provisioned-package inventory (elevated queries, since `Prepare` already
requires an elevated agent); and inventory for the intended interactive test
user. This administrator inventory is not proof that the provider executes
successfully as that user: it does not run another account's execution alias.
Every query
distinguishes a confirmed-absent result (the call succeeded with zero matches)
from a query failure (the call itself threw); a stage that could not be
evaluated is listed under `StagesNotChecked` rather than omitted or reported as
passing. Explicit `NotChecked` placeholders are still published if
`Prepare` or the interactive probe never ran. The always-run collector publishes
`prerequisite-admin.json`, allowlisted `prerequisite-user.json`, and `summary.txt`,
even without TRX. This diagnostic report never installs, elevates,
enables features, or reboots; it only reads what CI already runs as.

The test separately creates and attaches `winapp-prerequisites.json`
under persistent `TestResults\mwb-preflight-<invocationGuid>` before constructing
the fixture, so even a failure before the protected RunId or normal run directory
exists leaves evidence. It records the actual account/SID/session, elevation and
process/OS architectures. Each completed or failed check is persisted immediately.
When the launcher supplies `POWERTOYS_MWB_RUN_ROOT`, the same allowlisted JSON
is also written to `prerequisite-user.json` in that run root's existing parent
directory, before the run root is created. This fixed mirror lets the privileged
collector publish user evidence independently of TRX; it does not run user probes.
Existing `AssertPrerequisites` results include package query status/presence/count,
full name/family/version/architecture/status, execution-alias existence/reparse
state, package executable presence/PE machine, private tool/state validation,
CLI schema and the single bounded (15-second) `wsb --version` probe.
Unvisited checks remain `NotChecked`; a failed package query has a null count,
not a false zero. The provider probe records its actual exit code (null when
unavailable), timeout, fixed error codes/HRESULTs and only a recognized
`wsb major.minor.patch[.revision]` output line. Unrecognized banners are withheld,
not treated as a new prerequisite failure. No raw output, exception messages,
command lines, private paths or bootstrap state are attached. This report does
not run extra probes or establish a Windows 11 v0.7.0 pass.
Every stage also exposes `FailureCode` and `QueryErrorHResult` (null unless
applicable); confirmed package absence never invents a query-error HRESULT.

The September 28 unfiltered Windows 11 run
(`localvm-20260928-174310-a7f7f400`) exported this report and referenced it in TRX:
the actual standard user had one healthy Sandbox 0.8.107.0 package, a reparse
execution alias, an AMD64 provider and a successful `wsb --version` exit 0.
The smoke still timed out in official target bootstrap after 807 seconds
remaining in its unchanged 15-minute budget (3/4 tests; clean recovery).
A separate expected early-preflight failure
(`localvm-20260928-175405-bdbefcd1`, without starting provisioning) also exported
and attached the report, with all unvisited provider checks explicitly
`NotChecked`. This distinguishes a diagnostic-export pass from a scenario pass.

The bootstrap and Settings startup failures have been diagnosed and repaired. The lean payload
omitted dynamically activated Windows App SDK components and localized MUI
sidecars, including `WinUIEdit.dll` (the server for `Microsoft.UI.Text.FontWeights`
used by `FontIconExtension`). A private startup dump identified the actual XAML
exception; no raw process-memory dump is retained or published.

The input failures were separate harness defects: keyboard-only routing did not
leave MWB's simulated-input guard, and the guest acknowledged focus while Settings
still owned foreground. Pointer activity, routing-slot checks and an input-queue
foreground handoff with stable HWND/edit-focus assertions repaired those boundaries.
See the implementation checkpoint in `Tests\SandboxExperiment\CONTINUATION-PLAN.md`
for the evidence and remaining gates.

`AutonomousSandboxTests.AutonomousSandboxSmoke` is one ordered, nonparallel
MSTest/Microsoft.Testing.Platform scenario. It uses `UITestAutomation.Next` directly,
not `UITestBase` (whose generic startup/hygiene would interfere with two peers).

Topology: **L1 Windows x64 is the host peer; its L2 Windows Sandbox is the guest
peer. The physical development machine is never a peer.** This is experimental
user-desktop coverage, not service, secure-desktop, physical-machine, or
full module sign-off.

## Sandbox backends

The Windows 10 path remains `Legacy`: `WindowsSandbox.exe`, hardened `.wsb`
configuration, mapped endpoint channels and the existing viewer recording.
The official CLI's local UI commands work on Win10, but `--on sandbox` returns
`sandbox_unsupported`; the harness never attempts that target as a Win10 fallback.

The experimental `WinApp` backend requires Windows 11 24H2+, the modern
`MicrosoftWindows.WindowsSandbox` client registered for the standard test user,
and the official winappcli **v0.7.0** runtime. Enabling the Windows
optional feature alone is insufficient if the client/`wsb.exe` alias has not
finished installation. The harness does not install Windows features or elevate
its standard-user process.

The hybrid precreates an exclusive, run-owned provider GUID with the same
clipboard/device isolation as Legacy, then checks that winapp adopted that ID.
It uses target push for payloads, attached target exec for the existing stateful
guest worker, and guest-native screenshot/recording. The mapped request/result
channels, receivers, parent-bound leases, MWB firewall rules, real input/clipboard
assertions and restoration remain unchanged. The CLI also provisions its own
authenticated guest-agent transport; that is separate from MWB's ports.

Winapp's target state and bootstrap material stay below the private control root,
never test attachments. Official v0.7.0 defaults to
`%USERPROFILE%\.winapp\state\targets`, but explicitly retains the documented
`WINAPP_TARGET_STATE_ROOT` override in `TargetStateDirectoryProvider.cs`.
The adapter sets that override only for its child processes. It does not redirect
`USERPROFILE`, read or delete default-profile state, or modify the released binary.
Recovery derives the provider ID from the protected provisioning RunId,
validates the CLI path/hash and exact private state path, and stops only
owned processes and that exact provider instance. Missing capabilities, stale
ownership or failed modern startup are errors, not permission to run locally or
silently switch backends.

The selected release is [v0.7.0](https://github.com/microsoft/winappCli/releases/tag/v0.7.0)
at commit `2fdd020c5b7db093e8b3e317b22bdd8135a47e89`. It has no attach-only/expected-instance
option: the pre/post-operation identity checks detect changes but are not an
atomic acquisition fence. Run this experimental backend only in a dedicated
single-Sandbox VM. An ownership mismatch preserves private recovery state and
never authorizes stopping a different instance.

The earlier private-preview Win11 two-peer scenario was signed off locally: an unfiltered full-suite
run (4/4 tests, all 8 ordered phases) completed cleanly, including a single
physical remote click validated by the native click-readiness predicate
(`ReceiverObservation.cs`, `TwoEndpointFixture.cs`), with settings and
clipboard restored on both endpoints and no export errors. Earlier attempts on
this same backend did hit real infrastructure limits worth keeping as history:
the first nested run hit a transfer-specific timeout in the preview's initial
target-push bootstrap before the MWB guest worker started; an isolated
push/pull then completed byte-identically in 11 minutes 45 seconds, mostly
spent in guest firewall preparation, which is why the first target operation
now shares the original 15-minute endpoint bootstrap budget rather than a
shorter transfer cap. A later run also observed a transient
`StreamJsonRpc.ConnectionLostException` during Settings `Connect` and, in a
separate run, a real native-click-readiness failure (an MWB overlay under the
cursor at click time despite correct bounds/foreground) — fixed by requiring
two consecutive native hits on the expected receiver panel/root plus
foreground and no capture before the single physical click. Neither recurred
in the next runs. Owned cleanup completed without errors on every attempt,
signed off or not.

Earlier private-preview runs investigated a slow guest-firewall query and Win10
LogonCommand startup timeout. Those experiments are historical, not dependencies
of the official release path; no private upstream patch is built or shipped.

`Initialize-AutonomousHost.ps1` defaults to Legacy for existing callers.
Selecting `-SandboxBackend WinApp` also requires `-SandboxWinAppPath`; its hash
is recorded in the protected marker. The local wrapper's `Auto` mode selects
Legacy for `-Platform x64Win10` and WinApp for `-Platform x64Win11`. The stable
host/guest UI CLI and modern Sandbox CLI use the same official release.
Verify the official archive hash before selecting its three portable runtime files:
`winapp.exe`, `libSkiaSharp.dll`, and `libHarfBuzzSharp.dll`. Do not ship the PDBs.
The derived runtime archive has its own hash; it is not the official ZIP hash.
Both the x64 and ARM64 release assets are native AOT, with matching native
companions; do not infer architecture from the archive filename alone.

Example after staging coherent product/test archives and the official runtime
archive containing all three runtime files directly at its root (also stage it
as `winappcli.zip` for local UI commands):

```powershell
.\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\Invoke-AutonomousLocalVm.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -ConfigurationPath C:\PowerToysUiTestVm\vm.config.psd1 `
  -CredentialPath "$env:LOCALAPPDATA\PowerToysUiTestVm\admin.win11.credential.xml" `
  -ExchangeRoot C:\PowerToysUiTestVm\shared\PowerToysUiTests\MouseWithoutBorders `
  -Platform x64Win11 -SandboxBackend WinApp `
  -SandboxWinAppArchive winappcli-official-0.7.0-x64-runtime.zip `
  -ProductArchive powertoys-runtime-hybrid.zip `
  -GuestRuntimeArchive mwb-guest-runtime-hybrid.zip `
  -TestsArchive ui-tests-hybrid.zip `
  -ReuseStagedPayload -PlanOnly
```

Inspect the plan, including `SandboxWinAppArchiveSha256`, then repeat without
`-PlanOnly`. Keep separate DPAPI credential files when the two VMs have different
administrator passwords.

Local privileged setup uses the same protected stdout/stderr launcher and
90-second initial-marker budget as CI. Raw logs remain under the administrator-
only `provisioning-logs` directory, not public test attachments.

### Bounded modern startup and cooperative recording

The released v0.7.0 cold guest-agent setup queries
`Get-NetFirewallApplicationFilter` separately for every active firewall rule.
Measured official target pushes completed in 650.6 and 744.3 seconds; the latter
left too little of the former 15-minute bootstrap allowance for the endpoint
worker. This is slow completion, not proof of a permanently hung query. The
adapter retains that released, program-and-port-scoped firewall preparation;
it does not disable the firewall, patch winapp, or bypass agent authentication.
The final green run's push took 402.3 seconds. These are fresh Sandbox instances
on a retained L1 VM, not evidence that increasing a timeout speeds up the query.

Modern guest bootstrap now gets **35 minutes total**, including the bounded
**10-minute provider start**, CLI preparation, transfer and worker readiness.
The first push consumes only the remaining bootstrap allowance; it does not
start another 35-minute clock. Modern runs have a **70-minute hard deadline**
shared by both workers and their parent-bound lease publishers. Host and Legacy
bootstrap remain **15 minutes**, capped by the same run hard deadline; Legacy
runs retain **40 minutes**. Lease freshness remains **45 seconds**, with no grace
period. Local suite/controller envelopes are **75/90 minutes** for WinApp and
**45/60 minutes** for Legacy.

All adapter CLI children receive the same run-derived `WINAPP_UI_WORKFLOW_ID`
in their process environment only. This is the official cooperation contract:
v0.7.0 recording pins its workflow's desktop turn, and unrelated anonymous UI
actions wait until recording stops. `target exec` and target recording forward
the same target/epoch-scoped workflow into the guest, allowing the recorded
worker's real Settings actions to proceed. Neither the parent environment nor
global coordination state is changed. Distinct runs remain distinct workflows,
and GUID/process ownership and authenticated transport checks are unchanged.

The two initial guest navigation invocations have a bounded **180-second**
allowance after repeated 90-second timeouts; all other guest UI commands retain
**90 seconds**, and the encompassing Connect request remains **600 seconds**.
The green run used 6.0/10.8 seconds for those navigation actions, so it did not
exercise the extra allowance. Its search calls still took 20–30 seconds each.
`ui-command-timings.json` retains at most 64 command summaries; a command still
waiting after 90 seconds includes CPU/readiness and coordination ownership
booleans, never arguments, workflow tokens, owner hashes, or authentication state.
This distinguishes future cold/UIA delays from actual desktop-turn contention
without publishing a raw command line.

Clipboard publication acknowledges the digest of the generated synthetic token,
then requires that exact digest to be observed on its source endpoint before any
negative or transfer assertion. A cold guest's immediate post-write OLE read
returned empty once, although the next observation contained its own token (not
the host's distinct digest). Accepting that transient empty read as the baseline
caused a false isolation failure. The bounded source-readiness check neither
retries the write nor substitutes a destination value; clipboard-off isolation
and both real transfer directions remain mandatory.

## Before the test

An external privileged controller must:

1. Prepare an active, unlocked **standard-user** L1 desktop with Sandbox enabled,
   nested virtualization, suitable Sandbox launch rights, and no existing
   PowerToys or Sandbox instance. The pilot uses English Settings labels.
2. Stage a coherent self-contained **Debug** Runner, Settings, MWB and companion
   helper. Root and `WinUI3Apps` settings-library hashes must match. The fixture
   checks managed `AssemblyConfigurationAttribute`, the experiment flag, and
   fingerprints the endpoint executables, libraries and runtimes. No installation,
   elevation, firewall prompt, feature enablement, or GPO change is attempted by
   the standard-user fixture.
3. Provision the exact staged `PowerToys.MouseWithoutBorders.exe` for inbound TCP
   15100/15101, restricted to the **inner virtual-network subnet**. Write the
   protected, test-user-readable `host-provisioning.json`:

   ```json
   {
     "RunId": "<guid>",
     "ProductRoot": "C:\\PowerToysUiTestRun\\PowerToys",
     "RuleName": "PowerToys.Mwb.UITest.<guid>",
     "InnerSubnet": "<discovered-inner-subnet/prefix>",
     "HostAddress": "<L1-inner-interface-address>",
     "TestUserSid": "<standard-test-user-SID>",
     "ExecutableSha256": "<staged-MWB-executable-SHA256>",
     "LibrarySha256": "<staged-MWB-assembly-SHA256>",
     "GuestArchivePath": "<local-prepared-guest-runtime.zip>",
     "GuestArchiveSha256": "<guest-runtime-archive-SHA256>",
     "Status": "Ready"
   }
   ```

   On a cold outer-VM boot, the actual Sandbox **Default Switch** does not exist
   until L2 starts. The privileged setup may initially publish
   `Status: "WaitingForSandbox"` with empty `HostAddress`/`InnerSubnet`, then wait
   independently for that Windows-created interface. It must be a bounded setup
   process, not a broker accepting commands from the test. The fixture accepts
   this marker, boots its owned Sandbox, and waits up to three additional minutes
   after guest readiness for setup to provision the exact rule and publish
   `Ready`. A failed setup or timeout fails as infrastructure; no human warmup is
   required and no MWB endpoint starts while setup is pending.

   RunId, user, product and rule identity cannot change during the wait. Hashes
   are checked whenever present and are mandatory in the final `Ready` marker.
   Provision the actual Sandbox **Default Switch**, not another vEthernet adapter.
   The guest's selected interface must belong to `InnerSubnet`, with its default
   gateway exactly equal to `HostAddress`. Each worker rechecks its local address
   before product startup; the guest also rechecks its gateway before creating
   its rule. A shifted network fails with `NETWORK_CHANGED`.
4. Stage the repository-pinned standalone winappcli folder with its dependencies.
   Preserve the test application's `Payload` subdirectory.
5. Use `New-MwbRuntimeArchive.ps1` during preparation to package managed dependencies,
   native imports, dynamically loaded SDK servers, loose XAML/XBF and localized
   PRI/MUI resources. `.deps.json` and PE imports alone are insufficient.
   The guest copies the archive with a 4 MiB buffer, verifies its protected hash,
   then extracts locally before launching WinUI. Keep the source archive in its
   own read-only mapping. The maintained pilot uses that same coherent lean runtime
   archive for L1 and L2, with both copies verified against the protected bundle.

## Build and invoke

```powershell
dotnet restore .\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\MouseWithoutBorders.UITests.csproj -p:Platform=x64
.\tools\build\build.cmd -Path .\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests -Platform x64 -Configuration Debug
```

Run only inside the prepared L1 desktop:

```powershell
$env:POWERTOYS_INSTALL_DIR = 'C:\PowerToysUiTestRun\PowerToys'
$env:WINAPP_CLI_PATH = 'C:\PowerToysUiTestRun\winappcli\winapp.exe'
$env:POWERTOYS_MWB_PROVISIONING = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
$env:POWERTOYS_MWB_RUN_ROOT = 'C:\PowerToysUiTestRun\Results\MwbRun'
& 'C:\PowerToysUiTestRun\Tests\MouseWithoutBorders.UITests.exe' `
  --report-trx --report-trx-filename mwb.trx `
  --results-directory 'C:\PowerToysUiTestRun\Results'
```

`POWERTOYS_MWB_RUN_ROOT` must be new/empty. If omitted, its `mwb-<RunId>` directory
is placed beside `TestContext.TestRunDirectory`, outside the deployment tree that
MSTest deletes after success (falling back to `<working-directory>\TestResults`
when no test-run directory exists). The provisioning marker
environment variable is optional; the path shown above is its default.
The built executable is under
`x64\Debug\tests\MouseWithoutBorders.UITests\net10.0-windows10.0.26100.0`.

## What the test owns

- Legacy `WindowsSandbox.exe <run.wsb>` startup, a packaged Windows PowerShell 5.1
  `LogonCommand`, bounded run-correlated request/result files, leases and desktop
  readiness. Each bootstrap gets fifteen minutes starting at its endpoint launch,
  rather than consuming the guest's allowance during host preparation.
  The guest is bootstrapped first; the host worker starts after guest readiness,
  avoiding an idle host watchdog during the expensive nested-VM creation.
  There is no dependency on modern `wsb.exe`. Discovery and close confirmation
  use native owned-window APIs, never the Sandbox client's UIA tree. Startup
  errors require positive failure text from the launcher's native dialog.
  Committed, run-correlated readiness takes priority over optional discovery;
  worker failure still takes priority over readiness.
- Process-local legacy `PSModulePath` normalization, both at host-worker creation
  and in the shared worker/recovery support script. A PS7 controller's module path
  must not hide Windows PowerShell's hashing, networking, or firewall modules.
  Only the native System32 and Program Files WindowsPowerShell module roots are
  used; no user/machine environment or execution-policy setting is changed.
- Immutable, committed lease generations with latest-snapshot consumption:
  liveness does not require replaying hundreds of obsolete redirected JSON files.
  Two owned, hidden Windows PowerShell processes keep publication outside the
  MTP/native-recording process: a dedicated managed thread still stalled in CI.
  Each publishes only one endpoint instead of serializing host and mapped
  guest-file writes. Each retains the exact test-process handle, exits when that
  process exits, and cannot outlive the run's hard deadline (40 minutes for
  Legacy; 70 for WinApp).
  Correlated shutdown and identity-checked recovery stop only these publishers.
  `Host-lease-publisher.json` and `Guest-lease-publisher.json` record stage,
  sequence, write timing, maximum cycle gap and bounded stall history, including
  the previous write duration. The 45-second worker watchdog
  remains enabled. Before declaring an aged observation expired, a worker
  re-reads the newest committed generation once: the reader itself may have
  paused while publication continued. The publication timestamp must still be
  within 45 seconds; no lease is re-dated and no grace period is added.
  Native .NET adapter enumeration provides bootstrap IPv4/prefix/interface/gateway
  data without cold-starting PowerShell's NetTCPIP/CIM cmdlets.
- Read-only product-archive, worker, tools and request mappings; a separate writable guest
  output mapping. Host profile backups are **never mapped**. Network is enabled;
  Sandbox clipboard, audio, video and printer redirection are disabled.
- The guest bootstrap's ordinary elevated `WDAGUtilityAccount` desktop, including
  its exact-program/ports/host-address firewall rule. This is explicitly **not**
  MWB service mode. Only launched Runner processes receive
  `POWERTOYS_MWB_ALLOW_NONCONSOLE=1`.
- Settings **New key** and guest **Connect** UI actions, displayed peer identity,
  and established connections owned by each MWB PID.
- Native `GetExtendedTcpTable` snapshots preserve process ownership, local/remote
  addresses, ports and states for both IPv4 and IPv6. Transport evidence does not
  require PowerShell's NetTCPIP/CIM provider to initialize. The command and
  connection-readiness deadlines are unchanged; `transport-probe.json` identifies
  the current probe stage and elapsed time.
- A single explicit peer name-to-IP mapping on each endpoint, checked after
  seeding and immediately before Connect. `peer-mapping-*.json` records the
  persisted expected/actual mapping without exporting security keys. The
  transport assertion still requires actual connections to the discovered inner
  addresses; a settings-file match alone is not network proof. Transport evidence
  includes all owned MWB socket states as well as established connections.
- Native Settings readiness before attaching UI Automation: two consecutive
  observations of a responsive process with a visible, unowned, fully titled
  English Settings window. The window owner can coexist with its WinUI launcher.
  A `WinUI Desktop` placeholder is not ready. This uses
  the existing five-minute host/eight-minute guest budgets, not longer timeouts.
- Local-only `local1357924680`, guest-only `remote2468135790`, real remote cursor
  motion and a guest-only physical mouse counter. The L1 receiver HWND must retain
  foreground; Sandbox's viewer is not the physical input target. Receiver commands
  can clear/focus, but cannot set expected text or increment the mouse counter.
  Routing slots and the configured F1-F4 switch range must match before input;
  pointer activity precedes the keyboard-only switch to leave MWB's simulated-input guard.
  `FocusInput` must observe the exact receiver HWND and focused edit control twice;
  a successful `SetForegroundWindow` request alone is not an acknowledgement.
  An open Start menu is dismissed before native activation only when its visible
  `Windows.UI.Core.CoreWindow` belongs to the same session's exact OS-owned
  StartMenuExperienceHost, Win11 SearchHost, or Win10 SearchApp path. The modern
  Start panel can give foreground to its search process rather than its visual
  Start process. The helper revalidates process/HWND identity and active Default
  desktop, refuses held modifiers, sends one Escape, and requires foreground to
  leave that shell window within three seconds. It does not send Escape to unknown
  windows, inject a rescue click, toggle the Windows key, or extend the existing
  ten-second focus deadline. `FocusInput` records `StartMenuDismissed` in its
  correlated response. The focused regression deliberately opens Start, requires
  one dismissal, then proves physical typing reaches the receiver with zero
  mouse messages; separate cases reject other windows, paths and sessions.
- Clipboard-sharing-off isolation and sharing-on transfer in both directions,
  with at least 1500 ms between copies. Each source creates its own random
  synthetic token. Only observed hashes cross the control channel; the destination
  never receives the expected clipboard payload.
- Exact host module/global/OOBE settings bytes (or original absence), and an
  in-memory copy of the original clipboard formats. Peers stop **before** private
  clipboard restoration. Diagnostic receiver images render only the test-owned
  controls, never other windows or generated keys; they are not desktop-pixel
  assertions. Log export filters key/clipboard diagnostics.

## Evidence and recovery contract

TRX attachments include phase outcomes, topology, peer mappings, transport, receiver observations,
screenshots, and journals. Screenshots/logs are captured before endpoint teardown.
The workers' `logs` subfolders contain only run-scoped, filtered/redacted excerpts;
these files are attached explicitly, without recursively publishing control
requests or private recovery data.

The custom fixture also records continuous, silent H.264 video at 720p/15 fps:

- `recordings\desktop\recording_*.mp4` covers the test machine's desktop from
  fixture preparation through cleanup, including Sandbox startup and pairing.
- `recordings\sandbox-*\recording_*.mp4` captures the owned Sandbox viewer window
  separately, so the guest remains visible in its video while the host receiver
  covers that window. This encoder starts only after correlated guest readiness
  and native viewer ownership are acknowledged, outside the readiness poll;
  the continuous desktop recording covers Sandbox startup.
- Nonempty MP4s and `recordings\recordings.json` are attached to the test result
  on success and failure, under Azure DevOps **Tests > test result > Attachments**.
  Files live outside MSTest's disposable deployment tree.

There are no pairing pauses or key-hiding gaps. These disposable-VM recordings
can contain the experiment's temporary pairing keys and should remain internal
test diagnostics. JSON/log redaction and private profile-backup handling are
unchanged. The recording manifest reports unavailable encoders or capture/finalize
errors explicitly; a missing MP4 is not silently treated as successful capture.
An existing clip is retained even after a finalization warning, since it can
still contain usable evidence; consult `Completed`, `Available` and `Error`.
Failures in privileged CI preparation before MSTest starts cannot produce a
fixture recording. The protected provisioning launcher captures initializer
stdout/stderr even before its first marker, preserves the exit code, and prints
bounded, filtered failure excerpts to the CI task log. Raw provisioning logs
remain administrator/SYSTEM-only and outside test attachments.
The receiver-only PNGs remain control renders, not desktop
screenshots or substitutes for these videos.

Control inputs live outside test results at
`%LOCALAPPDATA%\Microsoft\PowerToysUiTestControl\<RunId>`, so a forcibly terminated
test cannot publish its pending Connect request as an artifact. Generated Connect
keys are overwritten in request files after use. Private profile
backups are kept outside all mappings/results, at
`%LOCALAPPDATA%\Microsoft\PowerToysUiTestRecovery\<RunId>`. They are removed after
successful restoration, otherwise retained on L1 for recovery; **do not publish
them as CI artifacts**.
Original clipboard contents are never written to disk. A forced worker termination
can therefore prevent clipboard restoration; such a run cannot claim clean cleanup.

`cleanup-journal.json` has `FormatVersion: 1`, `RunId`, `ControlRoot`, `ProductRoot`,
`HostName`, `TestProcess`, `LeasePublishers`,
`ProvisioningMarker`, `RuleName`, `TestUserSid`, `HostWorker`, `SandboxProcesses`,
`ViewerHwnd`, `GuestAcknowledged`, `HostEndpointJournal`, `GuestEndpointJournal`,
`Phase`, `Status`, `TimestampUtc`, `CleanupErrors`, and `PrivilegedCleanupRequired`.
Process records contain `Id`, `ParentId`, `SessionId`, `Path`, `StartTimeUtc`.
Never collect or publish Sandbox client command lines: Windows 10 embeds an
ephemeral account password in them. Only the whitelisted identity fields belong
in evidence.
Before terminating any recorded PID, recovery must revalidate **all** those fields;
never kill by name.

Each endpoint journal contains `Processes` and `Settings`, where each settings
entry contains `Path`, `Existed`, `Backup`, `Sha256`. Restore only as the original
user after stopping recorded peers, checking backup hashes before and after.
`Cleaned` describes fixture cleanup; test pass/fail remains in TRX and `phases.json`.
Any failed cleanup action fails the test and sets `RecoveryRequired`.

The external administrator must always remove **only** the recorded L1 firewall
rule (including timeout/failure paths), using
`Tests\SandboxExperiment\Remove-AutonomousHost.ps1 -StateRoot <protected-state-root>`.
The standard-user fixture deliberately does not remove it. Sandbox close uses its
owned viewer HWND and deletion confirmation, then bounded exact-PID fallback;
shared virtualization services are never stopped.

### Aborted-MTP recovery

The published test application includes `Payload\Recover-Host.ps1`. After the
recorded MTP process exits, the controller can dispatch it through its existing
**Limited, interactive, original-user** task mechanism:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File C:\PowerToysUiTestRun\Tests\Payload\Recover-Host.ps1 `
  -JournalPath <run-root>\cleanup-journal.json
```

The optional `-ProvisioningMarker` parameter has the same default protected path
as the fixture. No environment extension is required. Never run this script as
SYSTEM/admin; it rejects elevation and never alters firewall rules. It checks the
protected run/product/user identity, original machine/session, and recorded parent
chains. It first allows expired endpoint leases to finish, then uses bounded
identity-checked process cleanup, owned Sandbox close/confirmation, and exact
settings restoration. Its three-minute deadline permits at most one in-flight
15-second process wait beyond the deadline; a **five-minute controller timeout**
leaves room for startup and result writing.

It writes `recovery-result.json` alongside the fixture journal and returns zero
only for complete recovery. Repeated recovery ignores exited/reused PIDs, verifies
already-restored settings without rewriting them, and persists successful settings
restoration in the endpoint journal. If those restored files subsequently changed,
it refuses to overwrite them. A prior loss of the original clipboard remains a
failure on subsequent attempts. If a terminated worker had potentially changed the
clipboard without restoring it, recovery cannot reconstruct the private in-memory
snapshot: it returns nonzero with `RequiresBaselineReset: true`. The original
clipboard is never persisted or guessed. Keep such runs failed and restore the
clean VM baseline; do not turn cleanup into a pass fallback.

CI bounds the test process with a **45-minute Legacy / 80-minute WinApp**
interactive launcher, enclosing the respective **40-/70-minute** run deadlines.
The pipeline Run step allows **83 minutes**; Legacy's inner limits remain unchanged.
CI publishes TRX and the public run
directory (including `recovery-result.json`), and separately run the administrator's
exact-rule cleanup on every outcome. Release compilation is supported; selecting
or executing this nested Debug pilot remains behind the pipeline's default-off
opt-in condition, not an implicit all-suite test run.

The MWB `buildNow` pilot sets the shared build job's `buildInstallers=false`:
it consumes the Debug product/test directory directly, so WiX/MSI/bootstrapper
builds, installer staging and installer hashes are not needed. Other callers keep
the default `true`, including Release `buildNow` and `buildNowSlim` runs.
After pushing a pipeline fix, queue a new run from the updated branch; retrying
an old job continues to use that run's original source revision.
