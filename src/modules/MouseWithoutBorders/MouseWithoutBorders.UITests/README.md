# Autonomous MWB nested-Sandbox Debug pilot

**Status: the full unfiltered Win10 experimental suite has passed (2/2).**
Run `localvm-20260915-170002-e841cb1a` completed the smoke in 25m59s, including
remote keyboard/mouse, clipboard both ways, negative controls and cleanup, without
operator actions. Repeat reliability is not established: the later persistent-
evidence run `localvm-20260915-173353-3804112e` passed the receiver probe but stalled
in guest Settings initialization. This is feasibility evidence, not clean-baseline,
Win11 or full module sign-off. The first CI diagnostic was blocked in installer
packaging before either OS test stage; the Debug pilot now omits that unused path.

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

Topology: **L1 Windows 10 x64 is the host peer; its L2 Windows Sandbox is the guest
peer. The physical development machine is never a peer.** This is experimental
user-desktop coverage, not service, secure-desktop, physical-machine, Win11, or
full module sign-off.

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
   own read-only mapping. The full L1 and lean L2 runtimes are separate archives.

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
  --filter 'FullyQualifiedName~AutonomousSandboxSmoke' `
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
  There is no dependency on modern `wsb.exe`.
- Process-local legacy `PSModulePath` normalization, both at host-worker creation
  and in the shared worker/recovery support script. A PS7 controller's module path
  must not hide Windows PowerShell's hashing, networking, or firewall modules.
  Only the native System32 and Program Files WindowsPowerShell module roots are
  used; no user/machine environment or execution-policy setting is changed.
- Immutable, committed lease generations with latest-snapshot consumption:
  liveness does not require replaying hundreds of obsolete redirected JSON files.
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
`HostName`, `TestProcess`,
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

CI should bound the MTP suite at **45 minutes**, publish TRX and the public run
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
