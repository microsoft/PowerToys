# Grab and Move

## Overview

Grab and Move moves a window with modifier + left drag and resizes it with modifier + right drag.
Settings supports choosing Alt or Windows as the modifier, showing window geometry, and excluding
applications. Settings changes apply to the running module without a restart.

## Code structure

| Location | Responsibility |
|---|---|
| `src\modules\GrabAndMove\GrabAndMove\main.cpp` | Window selection, low-level input hooks, move/resize and geometry overlay |
| `src\modules\GrabAndMove\GrabAndMoveModuleInterface` | Runner module interface and module process lifecycle |
| `src\settings-ui\Settings.UI\SettingsXAML\Views\GrabAndMovePage.xaml` | Settings page |
| `src\modules\GrabAndMove\Tests\GrabAndMove.UITests` | Module UI tests and inert resizable test window |
| `src\common\UITestAutomation.Next\UmdfHidInput.cs` | Optional virtual-HID backend for the shared input helpers |
| `.pipelines\umdf-hid-smoke` | Test-only driver build, signing, provisioning, cleanup and transport probe |

## UI tests

The suite uses `Microsoft.PowerToys.UITest.Next` and winappcli, not WinAppDriver/Selenium. Its seven
cases cover Alt move/resize, geometry visibility for both gestures, live modifier changes,
application exclusions and disable/re-enable behavior through the real Settings UI.

### Why a driver is required

The shipping module intentionally rejects input marked `LLKHF_INJECTED` or `LLMHF_INJECTED`.
Ordinary `SendInput` gestures therefore do not exercise Grab and Move. Tests explicitly activate
`UmdfHidInput` and submit keyboard/mouse reports through the installed UMDF virtual HID device.
There is no injected-input fallback and no test-side automatic driver installation.

The Microsoft sample-derived driver is MS-PL licensed; the framework and source-built, fixed-device
setup helper are MIT licensed. The build preserves both licenses and attribution in the test
bundle. The driver is not included in PowerToys installers.

### Safety and desktop requirements

> **Use a disposable test VM or a dedicated test machine without real credentials.** While the
> driver is installed, local code can emit hardware-style keyboard/mouse input, including input
> to elevated desktops. Do not provision it on an everyday developer workstation. Always remove
> the device, driver package and journal-owned test trust after testing.

- Run the tests on an **active, unlocked console desktop**, as a **non-elevated user**.
  Driver provisioning/cleanup uses a separate elevated PowerShell 7 session.
- **Do not run the suite inside RDP**, including Hyper-V Enhanced Session. This HID backend does
  not target the RDP session's input desktop: opening the device can succeed while the RDP cursor
  never reaches the requested point. Use a physical console or Hyper-V **Basic Session**.
- The build host may remain connected over RDP when the tests execute in a guest's console
  desktop through PowerShell Direct and the local-VM controller. Do not RDP into that guest.
  Simply disconnecting RDP is not sufficient if it leaves the test session locked/disconnected.
- Use an English-language desktop, keep the pointer on the primary display, and avoid competing
  mouse/keyboard input. The current HID descriptor supports primary-monitor absolute coordinates,
  six non-modifier keys and whole 120-unit wheel detents.
- Exit PowerToys before starting. The harness stops/relaunches processes and temporarily changes
  settings. It restores saved settings during normal cleanup; retain a clean VM checkpoint in
  case the process or machine is interrupted.

To check the session from the terminal that will launch the tests:

```powershell
$sessionId = (Get-Process -Id $PID).SessionId
qwinsta.exe $sessionId
```

Look for an active `console` session, not an `rdp-*` session. `UserInteractive=true` alone does not
distinguish them.

### Architecture and signing prerequisites

The driver targets UMDF 2.31 (Windows 10 2004+ and Windows 11). The execution machine must also meet
the .NET 10 Desktop Runtime's OS/servicing requirements; an older unserviced Windows installation
is not a supported test baseline.

| Target | Test-driver policy for this self-signed bundle |
|---|---|
| Windows 10/11 x64 | Validated with ordinary boot policy and Secure Boot enabled, without enabling TESTSIGNING. A particular machine's additional policy can still reject the signer. |
| Windows 11 ARM64 | Use an isolated image already provisioned with TESTSIGNING enabled and rebooted by its owner. This is the validated configuration for this test bundle, not a claim about all ARM64 drivers. |

The native driver and setup helper must match the **target OS architecture**. x64 build tools or
provisioning PowerShell can run under emulation on ARM64; this does not make an x64 driver DLL an
ARM64 driver.

Do not disable Secure Boot, HVCI or Code Integrity on an ordinary working machine to get a pass.
Use the approved test image instead. Neither the tests nor the provisioning scripts change boot
policy or enable TESTSIGNING.

## Local testing quickstart

The recommended arrangement is **build on the host, execute in a disposable guest**. The steps
below also work entirely inside a disposable test VM containing the checkout and build tools.
They do not require a PowerToys installer.

### 1. Build a matching runtime and tests

On the build host, install the repository's [build prerequisites](../README.md#getting-started),
Visual Studio C++ tools for the target architecture, PowerShell 7 and .NET 10 SDK. The native
driver projects currently select Windows SDK `10.0.26100.0`; `Build-Smoke.ps1` acquires and verifies
its pinned Microsoft SDK/WDK NuGet packages separately. A first driver build requires network access.

Run from the repository root in a normal PowerShell 7 terminal. Use `ARM64` instead of `x64` for
an ARM64 execution machine:

```powershell
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$platform = 'x64'

git submodule update --init --recursive
if ($LASTEXITCODE) { throw 'Submodule initialization failed.' }

# Build the complete Debug product, not just the test executable.
.\tools\build\build.cmd -Platform $platform -Configuration Debug
if ($LASTEXITCODE) { throw 'PowerToys runtime build failed.' }

.\tools\build\build.cmd `
    -Path src\modules\GrabAndMove\Tests\GrabAndMove.UITests `
    -Platform $platform -Configuration Debug
if ($LASTEXITCODE) { throw 'Grab and Move test build failed.' }
```

See [build guidelines](../../../tools/build/BUILD-GUIDELINES.md) for first-time restore/setup and
error logs. The test executable is:

```text
<repo>\<platform>\Debug\tests\GrabAndMove.UITests\net10.0-windows10.0.26100.0\GrabAndMove.UITests.exe
```

**Runtime configuration and test configuration are separate.** A Release test executable can
drive a Debug runtime, but it does not turn an unsigned Release Runner into a Debug Runner.
The quickstart builds both in Debug to keep the runtime choice explicit.

### 2. Build and self-sign the native driver

In the same build-host terminal:

```powershell
.\.pipelines\umdf-hid-smoke\Build-Smoke.ps1 `
    -WorkRoot C:\PTGrabMoveBuild\work `
    -OutputRoot C:\PTGrabMoveBuild\artifacts `
    -Platforms $platform -DriverOnly
if ($LASTEXITCODE) { throw 'Native test-driver build/signing failed.' }
```

This script:

- Fetches the pinned Microsoft `vhidmini2` sample and applies the input-report patch.
- Builds the native driver DLL and the fixed-hardware-ID `CreateUmdfHidDevice.exe` helper.
- Creates a **seven-day**, non-exportable code-signing certificate named
  `CN=PowerToys Isolated UMDF CI Smoke` in the build user's `CurrentUser\My`.
- Authenticode-signs the DLL/helper, generates and signs the catalog, exports only the public
  `PtHidProbe.cer`, and writes a SHA-256 file manifest.
- Removes its temporary signing certificate/private key in `finally`. It does **not** import
  machine trust on the build host. An untrusted-root result when inspecting that bundle before
  provisioning is therefore not, by itself, a broken signature.

Preserve the complete `artifacts\<platform>` directory, including its `driver` subdirectory,
`driver-hashes.json`, public certificate and licenses. Do not copy just the DLL/INF.
Do not rebuild, re-sign or replace this bundle while its device is installed: every build creates
a new certificate, while cleanup still belongs to the original ownership journal.
If the certificate expires, clean up the old installation before rebuilding and provisioning again.

### 3. Stage the execution machine

Inside the target VM, install architecture-matched .NET 10 Desktop Runtime and the pinned winappcli
from the [shared UI-test guide](../development/ui-tests.md#before-running-tests). WinAppDriver is
not required for this suite. Make `winapp.exe` available to the non-elevated test user, not just
the administrator account. For a standalone CLI outside PATH, set `WINAPP_CLI_PATH` below to its
full executable path instead of using `Get-Command`.

Copy the **complete** product/test output and driver artifact into this layout:

```text
C:\PTGrabMoveTest\runtime\PowerToys.exe
C:\PTGrabMoveTest\runtime\PowerToys.GrabAndMove.exe
C:\PTGrabMoveTest\runtime\WinUI3Apps\PowerToys.Settings.exe
C:\PTGrabMoveTest\runtime\tests\GrabAndMove.UITests\net10.0-windows10.0.26100.0\...
C:\PTGrabMoveTest\driver-artifact\driver-hashes.json
C:\PTGrabMoveTest\driver-artifact\driver\...
C:\PTGrabMoveTest\Manage-SmokeDriver.ps1
```

`runtime` is the copied `<repo>\<platform>\Debug` tree, including dependencies and the newly built
tests. `driver-artifact` is the copied `C:\PTGrabMoveBuild\artifacts\<platform>` tree.
Copy `Manage-SmokeDriver.ps1` from `.pipelines\umdf-hid-smoke` at the same source revision.
Do not mix binaries or artifacts from different architectures/revisions.

For a machine that builds and executes inside the same disposable VM, staging the runtime is
optional: point `POWERTOYS_INSTALL_DIR` and the test path below at its actual build output instead.
For automated payload staging and execution, use the
[persistent local-VM workflow](../../../.github/skills/ui-tests-local-vm/references/agentic-loop.md)
with `-TestExecutable GrabAndMove.UITests.exe` and `-Platform x64Win10`, `x64Win11` or `ARM64`.

### 4. Provision the driver and arrange owned cleanup

In an **elevated PowerShell 7** terminal inside the disposable execution machine:

```powershell
$ErrorActionPreference = 'Stop'
$work = 'C:\PTGrabMoveTest'
$manager = Join-Path $work 'Manage-SmokeDriver.ps1'
$state = Join-Path $work 'driver-state.json'
$driverResults = Join-Path $work 'driver-results'

try {
    & $manager -Action Install `
        -ArtifactDirectory (Join-Path $work 'driver-artifact') `
        -StatePath $state -ResultsDirectory $driverResults

    Read-Host 'Run the suite from the NON-elevated console terminal, then press Enter here to clean up'
}
finally {
    & $manager -Action Cleanup -StatePath $state -ResultsDirectory $driverResults
}
```

This interactive example pauses only after successful installation. Follow Step 5 in a separate
normal terminal, then return and press Enter immediately. On an install error, `finally` still
attempts cleanup. Automated VM jobs use their separate administrator control channel instead of
this interactive pause.

Installation validates the file manifest and signer, imports the public certificate into
`LocalMachine\Root` and `LocalMachine\TrustedPublisher` only when absent, creates the fixed root
device, and installs the INF with inbox `pnputil`. Readiness requires install exit `0`,
status `OK`, problem code `0`, and **three HID collections** in `driver-install.json`.
An opened HID path alone is not proof that input reaches the test desktop.

The ownership journal records the exact certificate stores/thumbprint and published INF names.
Cleanup removes only the owned present device/package and journaled test trust. Pre-existing
certificate entries are not owned by this invocation.

### 5. Run the tests without elevation

Exit any existing PowerToys instance. In a **normal PowerShell 7** terminal on the execution
machine's unlocked **console** desktop:

```powershell
$ErrorActionPreference = 'Stop'
$env:POWERTOYS_INSTALL_DIR = 'C:\PTGrabMoveTest\runtime'
$env:WINAPP_CLI_PATH = (Get-Command winapp.exe -ErrorAction Stop).Source
$env:useInstallerForTest = 'false'
$exe = 'C:\PTGrabMoveTest\runtime\tests\GrabAndMove.UITests\net10.0-windows10.0.26100.0\GrabAndMove.UITests.exe'
$results = Join-Path $env:LOCALAPPDATA 'PowerToys\UITestResults\GrabAndMove'

& $exe --report-trx --report-trx-filename GrabAndMove.trx `
    --results-directory $results --timeout 20m
if ($LASTEXITCODE) { throw 'Grab and Move UI tests failed; preserve the TRX and complete driver cleanup.' }
```

Expect **7 executed, 7 passed, zero skipped/not-executed**. Check the TRX counters, not just the
exit code. Do not interact with the keyboard/mouse while gestures run.
Leave `platform` and `TF_BUILD` unset for ordinary local runs; pipeline mode also normalizes the
display and enables additional recording/diagnostics.

For a focused iteration, add an explicit filter such as
`--filter 'FullyQualifiedName~AltLeftDragMovesWindowToExpectedPosition'`; then expect one test,
not seven. Do not use `dotnet test`. Test Explorer is also supported after provisioning, but
Visual Studio must be **non-elevated** and use the matching runtime/test configuration.

### 6. Verify cleanup, including interrupted runs

Return to the elevated terminal in Step 4 and press Enter. Require `driver-cleanup.json` to show
`Removed=true`, `Errors=[]`, and confirm `driver-state.json` no longer exists.
Cleanup failure retains the journal so it can be retried.

If that terminal/process was closed, rerun cleanup in elevated PowerShell 7:

```powershell
& C:\PTGrabMoveTest\Manage-SmokeDriver.ps1 -Action Cleanup `
    -StatePath C:\PTGrabMoveTest\driver-state.json `
    -ResultsDirectory C:\PTGrabMoveTest\driver-results
```

Never delete the journal to bypass a pre-existing-device/installation error. Do not remove arbitrary
OEM INFs or certificate-store entries. A failure before a journal was created can have no cleanup
report; inspect the original install error rather than interpreting a missing report as success.
Restoring the disposable VM checkpoint is another way to return it to its original state.

## Driver signing is not Release Settings IPC signing

The bundle's certificate signs the virtual-HID binaries/catalog. It is not the Microsoft-publisher
identity required by the Release Runner's authenticated Settings IPC.

A locally unsigned Release runtime can open Settings while rejecting module enable/disable
commands with `not-microsoft-signed`. Debug relaxes only that signature check; matching versions,
filenames and the `WinUI3Apps` directory layout are still required.

For Release validation, use a matching officially signed runtime or the existing
[`signSparsePackages.ps1 -RequiredAuthenticodeFile` companion-signing workflow](../../../.github/skills/ui-tests-migration/references/ci-stability.md#principle-5a--keep-module-lifecycle-tests-on-real-release-settings-ipc)
for **both** `PowerToys.exe` and `PowerToys.Settings.exe` on the isolated execution machine.
Keep its certificate marker separate from `driver-state.json` and remove that test trust with
[removeTestSigningCertificates.ps1](../../../.pipelines/removeTestSigningCertificates.ps1).
When signing a payload on a separate build host, use `-SkipLocalTrust` and export only the public
certificate for the guest; do not install execution-machine trust on the build host.
Never replace the Settings lifecycle assertions with a settings-file/restart fallback.

## Optional transport-only probe

To isolate driver/session problems from PowerToys, build the product-free probe on the build host:

```powershell
dotnet restore .\.pipelines\umdf-hid-smoke\GrabAndMove.HidSmoke.Probe.csproj -p:Platform=$platform
if ($LASTEXITCODE) { throw 'Probe restore failed.' }
.\tools\build\build.cmd -Path .pipelines\umdf-hid-smoke -Platform $platform -Configuration Release
if ($LASTEXITCODE) { throw 'Probe build failed.' }
```

Copy the complete `<repo>\<platform>\Release\tests\GrabAndMove.HidSmoke.Probe\net10.0-windows10.0.26100.0`
directory to `C:\PTGrabMoveTest\probe`. While the driver is installed, run in the **non-elevated
console** terminal:

```powershell
& C:\PTGrabMoveTest\probe\GrabAndMove.HidSmoke.Probe.exe `
    --filter 'FullyQualifiedName~TransportSmokeTests.HidEventsAreNotMarkedInjected' `
    --report-trx --results-directory (Join-Path $env:LOCALAPPDATA 'PowerToys\UITestResults\HidProbe')
if ($LASTEXITCODE) { throw 'HID transport probe failed; preserve results and complete driver cleanup.' }
```

Expect **1 executed, 1 passed, zero skipped**. `umdf-input-flags.json` must contain non-injected HID
events and correctly detected injected positive controls after scope disposal. This proves the
transport, not the seven module cases. It still needs the console desktop and the same cleanup.

## Troubleshooting

| Symptom | Check/action |
|---|---|
| `The fixture cursor did not settle ...` while a real HID path was opened | Confirm console vs RDP/Enhanced Session first. Opening the device is not proof of session delivery. Also check primary-display coordinates and competing physical input. Do not hide this with longer waits, coordinate tolerances or `SendInput`. |
| `No PowerToys virtual HID test device was found` | Provision the matching architecture's bundle and inspect driver readiness/problem codes. There is no automatic install or fallback. |
| `0xE0000243` / signer not trusted with Code Integrity enforced | Authenticode certificate trust and driver-policy acceptance are different checks. Check the target image/policy, certificate presence and SetupAPI/Code Integrity diagnostics. For this self-signed ARM64 bundle use the preconfigured isolated test image, not weakened host policy. |
| Hash/signature mismatch or expired certificate | Clean up the old installation, rebuild/re-sign a fresh complete bundle, and provision it again. Do not replace files under an installed/journaled package. |
| Test initialization rejects elevation | Run the executable/Visual Studio normally. Only provisioning and cleanup are elevated. |
| Settings opens but enable/disable has no runtime effect | Check Runner IPC rejection logs, runtime path/layout/version and Release companion signing. Driver trust is not Settings IPC trust. |
| Exclusions textbox/other expected UI cannot be found | Use the Settings binary built from the same checkout; an older runtime may not expose the test's AutomationIds. |
| Existing journal/device prevents installation | Retry owned cleanup with the original journal. Preserve the journal and error evidence if it fails. |
| Exit code is zero but nothing ran | Inspect TRX total/executed/passed/not-executed counters and use a qualified filter. An unfiltered module run has seven cases. |

The provisioning results directory contains `driver-install.json`, `driver-cleanup.json` and
`trust-diagnostics.json`; failed installs also attempt `setupapi-tail.log` and `pnp-events.json`.
Build errors are under `<repo>\artifacts\logs\<project>\`. UI failures attach evidence to the TRX.
Additional Code Integrity Operational events and read-only `CiTool -lp -json` output can help
compare images. Inspect/redact usernames, paths, SIDs, enterprise policy names and unrelated
system details before posting logs publicly. Never upload private keys or credentials.

## CI

Select exactly `[GrabAndMove.UITests]` in UI Test Automation. x64 covers Windows 10 and Windows 11;
ARM64 selects an isolated `_UmdfHid` job with an explicit TESTSIGNING image override. The pipeline
provisions/cleans up the driver separately and dispatches tests to the standard-user desktop.
Public OSS PR CI keeps ARM64 HID opted out until its image availability is verified; other suites
remain selected. See the [shared ARM64 CI configuration](../development/ui-tests.md#dedicated-arm64-grab-and-move-ci-image)
and [UI-test development guide](../development/ui-tests.md) for current parameters and CI behavior.
