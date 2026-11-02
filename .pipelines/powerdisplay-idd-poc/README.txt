Power Display IDD feasibility POC (issue #51800)
==============================================

This is a test-only feasibility spike, not the complete virtual-monitor fixture
or a shipping PowerToys component. It does not modify product code, installers,
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
restores the original 40 and 60. Six protocol test groups pass on each OS.
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

Always run elevated owned cleanup, including after a failed test:

  pwsh C:\IddPoc\Manage-Poc.ps1 -Action Cleanup -IsolatedGuest `
    -StatePath C:\IddPoc\state.json -ResultsDirectory C:\IddPoc\provision

Require cleanup.json Removed=true, Errors=[] and an absent ownership journal.
Export evidence before restoring the owned checkpoint. Restore it for exact
topology/trust recovery, then stop the guest and remove only that checkpoint.
An interrupted run retains its journal for cleanup with the original bundle.
Do not delete the journal or remove unrelated packages/certificates.

Remaining issue gates
---------------------

No Power Display UI signoff, per-monitor hot-plug control, identify-overlay
coverage, persistence across driver restart, failure-mode injection, constrained
resource matrix or remote CI is claimed. Arrival occurs at adapter startup and
departure at owned device removal. No ARM64 driver-load result is claimed.
The full UI matrix/publication gate is not reached.

This POC establishes native transport and production discovery feasibility.
The remaining fixture/test-suite work is separate from this spike. No injectable
monitor backend, production filter bypass or shipping behavior change is used.
