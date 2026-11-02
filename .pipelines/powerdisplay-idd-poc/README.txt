Power Display virtual monitor fixture (issue #51800)
===================================================

This is a test-only UMDF/IddCx fixture, not a shipping PowerToys component.
The initial feasibility milestone is commit 2d94623123. Follow-ups add controlled
hot-plug and fault modes plus the module's actual UI tests. It does not modify product code, installers,
boot policy, host trust, or ordinary CI images.

Result
------

The native transport works on both tested x64 guests:
  Windows 10 22H2, 19045.6456
  Windows 11 25H2, 26200.8037

The signed UMDF/IddCx package loads with Secure Boot enabled and TESTSIGNING
absent. It creates two distinct sample-EDID monitors with fixed container IDs.
Dxva2 capabilities and VCP 0x10 set/readback reach the real IddCx callbacks.
Independent brightness values 31 and 73 are observed by the driver; the probe
restores the original 40 and 60. The extended emulator has eight protocol groups.
x64 and ARM64 Release builds/catalog generation pass. ARM64 runtime is untested.

The production probe uses the unchanged Power Display MonitorManager:
  QueryDisplayConfig inventory contains both fixture identities.
  Production DDC/CI discovery retains both as brightness-capable monitors.
  Rediscovery retains their distinct stable IDs.
  Assembly hashes and production logs are included in the evidence.

The native-only probe can receive physical handle values 0 and 1. Native Dxva2
accepts BOTH, including handle 0. Although the production helper filters zero,
its actual enumeration/retry path discovers both fixture monitors. A zero value
in a different process is NOT proof of a product incompatibility. No product
workaround is needed or applied.

Native callback addressing is 0x6E for transmit and 0x6F for receive despite the
SevenBitI2CAddress field names. The driver records addresses, request bytes,
read/write counts, capabilities requests and independent brightness state.
The discarded experiment with connectors 1/2 did not change handle values.
No connector reservation, dummy display, DLL interception or fake backend is used.

Build on the host
-----------------

From the repository root in PowerShell 7:

  .\.pipelines\powerdisplay-idd-poc\Build-Poc.ps1 `
    -WorkRoot C:\IddPocBuild\work -OutputRoot C:\IddPocBuild\out -Platform x64

Use -Platform ARM64 for an ARM64 bundle. -PrepareOnly fetches/verifies build
inputs without building/signing. The script pins the Microsoft driver sample and
WDK/SDK packages, verifies NuGet signatures and uses tools\build\build.cmd.

The output is out\<architecture>\{driver,production,driver-hashes.json,
production-hashes.json,build.json}. The production directory is a console probe
referencing the actual PowerDisplay.Lib project, with its runtime dependencies.
It needs the matching .NET 10 runtime in the execution guest. If that runtime is
staged outside Program Files, set DOTNET_ROOT to its directory for the console
user (for example C:\PowerToysUiTestRun\dotnet).
A seven-day non-exportable signing key is removed in finally; only its public
certificate is exported. Do not rebuild/replace a bundle while it is installed.
Keep the complete artifact, including licenses and hashes.

Run only in an explicitly approved disposable Hyper-V guest
---------------------------------------------------------

Use the ui-tests-local-vm workflow for host preflight, architecture-matched
guests, public-only payload staging and a true standard-user console desktop.
Create an owned checkpoint BEFORE installation to preserve exact topology and
trust even if provisioning or execution is interrupted. Never change host or
ordinary guest boot policy to make this package load.

Stage the architecture artifact as C:\IddPoc\artifact and these two scripts as
C:\IddPoc\Manage-Poc.ps1 and C:\IddPoc\Test-Transport.ps1.

From the separate elevated guest provisioning channel:

  pwsh C:\IddPoc\Manage-Poc.ps1 -Action Install -IsolatedGuest `
    -ArtifactDirectory C:\IddPoc\artifact -StatePath C:\IddPoc\state.json `
    -ResultsDirectory C:\IddPoc\provision

Grant the standard user write access ONLY to a fresh results directory, never
the artifact, provisioning script, ownership journal or signing material.
From the standard-user console desktop (not session 0/RDP):

  pwsh C:\IddPoc\Test-Transport.ps1 `
    -ProbePath C:\IddPoc\artifact\driver\IddProbe.exe `
    -ResultsDirectory C:\IddPoc\results `
    -ProductionProbePath C:\IddPoc\artifact\production\PowerDisplay.IddPoc.ProductionProbe.exe

status.json requires NativeTransportPassed AND ProductionDiscoveryPassed.
NativeHandlesNonzero is diagnostic only; it is not an integration assertion.
Inspect protocol-tests.json, diagnostics.json, transport.json, before.json and
after.json, production-discovery.json and production-discovery-error.txt; do not
infer production success from native display enumeration or unit tests.
The product-free IddProbe also exposes --self-test, --snapshot, --diagnose and
--probe. --diagnose performs native read-only requests, including on handle 0;
--probe tests native capabilities, stateful writes/readback and restoration.
The production console invokes the real manager twice with an in-memory
known-good-observation cache; it does not inject a monitor/DDC backend. Production
Logger output is captured on stderr without COM-dependent app log-path setup.
These probes emit assertion-bearing JSON; they are not an MSTest/UI suite.

Monitor controls and state
--------------------------

Protocol version 2 exposes the following commands through the test-only device
interface. The package grants read/write access to the guest's standard users;
installation and owned cleanup still require the separate elevated channel.

  IddProbe.exe --connected 0 0     Disconnect connector 0
  IddProbe.exe --connected 0 1     Reconnect the same monitor
  IddProbe.exe --mode 1 0          Normal DDC/CI
  IddProbe.exe --mode 1 1          No advertised/supported brightness capability
  IddProbe.exe --mode 1 2          Fail I2C transmit/receive with a failure counter

Only connectors 0 and 1 are valid. Invalid commands fail explicitly. Arrival
and departure use IddCxMonitorArrival/IddCxMonitorDeparture, not disabled windows
or a substituted product backend. Brightness, mode and counters survive a
monitor's departure/arrival within the running driver; a new adapter/device
starts a new fixture lifetime. Snapshot JSON includes Connected, Mode, Failures,
Arrivals, Departures, successful Get/Set counts and the last I2C request.

Full module UI suite
--------------------

Build the existing PowerDisplay.UITests project with tools\build\build.cmd.
Run its executable on the standard-user console with the fixture installed:

  $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'C:\IddPoc\artifact\driver\IddProbe.exe'
  $env:POWERTOYS_INSTALL_DIR = 'C:\PowerToysUiTestRun\PowerToys'
  PowerDisplay.UITests.exe --report-trx --results-directory C:\IddPoc\ui-results

The complete suite contains 13 tests, including five cases tagged
PowerDisplayVirtualMonitors. Those cases FAIL if the fixture is missing; they
never silently skip or use a fake monitor controller. They verify independent
brightness with driver callbacks, stable IDs/readback after Refresh, per-monitor
hot-plug, the no-supported-monitors state, unsupported brightness and rejection
of a hardware write without persisting false success. The existing Identify
test checks one overlay on each connected Windows display, including the
fixture displays. Require all 13 executed and passed, with zero skipped.

Settings intentionally retains disconnected monitor records for 30 days.
The live inventory assertion reads actual flyout controls, not that retained
cache. Numeric slider reads use the Windows UIA RangeValuePattern because the
pinned winapp get-value returns a Slider's accessible text rather than its
range value. Actions remain real UIA slider writes; the test's WPF reference
provides UIAutomationClient only and does not change the product UI stack.

Use the ui-tests-local-vm workflow for full Windows 10/11 Default (4 vCPU/8 GB)
and Constrained (1 vCPU/4 GB) passes. Inspect the ACTUAL VM resource settings,
not just the request label, when reusing an already running guest. The internal
UI-test pipeline builds/provisions the fixture for PowerDisplay.UITests and
routes ARM64 to the existing isolated TESTSIGNING image in a separate
_UmdfDisplay job. It never changes boot policy and always exports owned cleanup
evidence. ARM64 build success alone is not an ARM64 load/routing verdict.

Always run elevated owned cleanup, including after a failed test:

  pwsh C:\IddPoc\Manage-Poc.ps1 -Action Cleanup -IsolatedGuest `
    -StatePath C:\IddPoc\state.json -ResultsDirectory C:\IddPoc\provision

Require cleanup.json Removed=true, Errors=[] and an absent ownership journal.
Export evidence before restoring the owned checkpoint. Restore it for exact
topology/trust recovery, then stop the guest and remove only that checkpoint.
An interrupted run retains its journal for cleanup with the original bundle.
Do not delete the journal or remove unrelated packages/certificates.

Coverage boundaries
--------------------

This fixture does not prove a physical panel's backlight, the WMI hardware path,
physical mixed-DPI signoff or persistence across driver/device restart. Runtime
and CI claims require their corresponding TRX, payload hashes, PnP/signing-policy
evidence and cleanup reports; compilation is not a substitute. No injectable
monitor backend, production filter bypass or shipping behavior change is used.
