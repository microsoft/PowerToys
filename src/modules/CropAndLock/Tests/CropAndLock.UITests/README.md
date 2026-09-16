# Crop And Lock UI tests

Greenfield coverage for [#40679](https://github.com/microsoft/PowerToys/issues/40679),
using `Microsoft.PowerToys.UITest.Next` and the real Runner hotkeys.

| Checklist | Test |
| --- | --- |
| Thumbnail, Win32 app | `ThumbnailWin32ShowsSelectedRegionAndLiveUpdates` |
| Thumbnail, packaged app | `ThumbnailPackagedAppShowsSelectedRegionAndLiveUpdates` |
| Reparent, Win32 app | `ReparentWin32SupportsInputAndRestoresSource` |
| Reparent, packaged app | `ReparentPackagedAppSupportsInputAndRestoresSource` |
| Additional lifecycle coverage | `SettingsToggleStopsAndStartsModule` |
| Additional cancellation coverage | `EscapeCancelsSelectionWithoutChangingSource` |

## Local Test Explorer

Run Visual Studio/Test Explorer non-elevated on an English interactive desktop.
A normal developer build produces an **unsigned** fixture, and unsigned Settings
is rejected by Release Runner's authenticated IPC. These are setup requirements,
not failures of the crop scenarios.

For ordinary local runs, the two packaged scenarios and the authenticated Settings
lifecycle scenario now check signing before installing the fixture or changing the
toggle. If a trusted signature is unavailable, they report **Inconclusive/Skipped**
with the missing file and setup instructions. The remaining scenarios and helper
checks still run. The lifecycle scenario deliberately requires the authenticated
signing setup rather than inferring it from the test project's Debug/Release
configuration, since tests can target a different product build.

To run those cases too, prepare the signed fixture and matching trusted
Runner/Settings environment described below, then rerun them. The tests never
install certificates or alter host trust automatically; isolated VM validation is
recommended for disposable test signing.

CI and pipeline-like VM runs (`TF_BUILD`/`platform`, as defined by the shared
harness) remain **strict**: missing signing fails instead of skipping. Corrupt
signatures and actual installation, lifecycle, pixel, or input failures are not
converted into skips.

## Fixtures and prerequisites

- Run in an unlocked, English-language standard-user desktop with PowerToys,
  winappcli and the architecture-matched .NET 10 Desktop Runtime. No Store,
  elevated test process or additional third-party dependencies are required.
- The Win32 fixture is an owned WinForms window on a dedicated STA thread. Its
  editable text and colored panels provide deterministic, visibly changing content.
  The test verifies that its process has **no** package identity.
- The packaged fixture is `CropAndLock.TestApp`, a separate self-contained
  WinForms executable in a **full MSIX**, not a sparse identity or an unpackaged
  substitute. The same `CropSourceForm.cs` supplies both fixtures' editable text,
  colored panels and physical-pixel geometry.
- The fixture includes its architecture-matched .NET 10 Windows Desktop runtime.
  Windows activates the app through its registered app entry, so the test
  process's private `DOTNET_ROOT` is not an inherited runtime contract.
  Neither a machine-wide .NET installation nor a VM/pipeline environment change
  is needed for the packaged source. The UITest executable itself still uses the
  existing framework-dependent test-host runtime setup.
- Stage **signed** `CropAndLock.TestApp.msix` beside `CropAndLock.UITests.exe`.
  Each packaged case installs it for the current user using
  `PackageManager.AddPackageAsync`, activates its registered app entry, and checks
  the source HWND's process against the installed package's exact
  `GetPackageFullName`, architecture and non-elevated token. Cleanup closes the
  owned process before removing the current-user registration and verifies removal.
  A previous interrupted run is reclaimed only after checking the fixture's exact
  package name and publisher; process cleanup additionally checks its full package
  identity. Unrelated registrations and executable namesakes are refused.
- Use the existing UI-test package signing/trust setup, not test-side certificate
  creation or an unsigned development-registration fallback:
  - Package filename: `CropAndLock.TestApp.msix`
  - Identity: `Microsoft.PowerToys.CropAndLock.TestApp`
  - Publisher: `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US`
- Release builds exercising the Settings lifecycle test need the existing UI-test
  pipeline's authenticated Settings IPC companion-signing setup for Runner and
  Settings. A rejected Settings command must fail the runtime assertion; the suite
  never repairs it by editing the enabled map or restarting Runner.
- CI dispatches this suite through `.pipelines\runUiTestAsUser.ps1` in the logged-on
  user's limited interactive token. Package activation always starts the fixture
  non-elevated; an elevated Runner can reparent its HWND but cannot reliably route
  keyboard focus across that integrity boundary. Both the test host and module
  must be non-elevated, matching the local standard-user runs. The shared pipeline
  keeps its administrative setup/signing steps and other suites unchanged.

## What is asserted

Every feature case starts through the Settings/Runner scope with only Crop And
Lock enabled. It reads the selected mode's live shortcut card, establishes source
input, sends the real chord and performs a physical crop drag. Overlay/output
readiness uses module-owned native HWNDs/classes; no UIA inspection occurs while
selecting or displaying a crop.

Before input, the test waits for the source to own the target screen point. A
Windows Shell `Shell_LightDismissOverlay` is dismissed with one outside click,
then ownership is checked again; unrelated occlusion is never accepted as ready.

The tests compare the selected source pixels with the composed crop client area,
including exact selected dimensions. Runtime reference captures avoid dependence
on OS-specific window rendering. In addition to the overall comparison, a
foreground-pixel comparison prevents an empty input/background from passing.
Thumbnail cases edit the original app and require the **same** thumbnail to update.
Reparent cases verify the actual HWND parent chain, `WS_CHILD`, preserved source
size, keyboard input through the clipped window, and original parent, styles,
geometry, content and input after closing it.

Failure media is captured before custom cleanup closes any diagnostic windows.
Four additional pixel-comparison tests cover dimension diagnostics, repeated
comparisons, blank-image rejection, and changed foreground content.
Four signing-policy checks cover local skips, strict CI failures, trusted signing,
and rejection of corrupt signatures.

### Why not Windows Settings?

The initial Windows Settings probe on Windows 10 Pro 19045 showed two independent
fixture limitations: its search field changes appearance on focus loss, and its
`ApplicationFrameWindow` does not support the required reparent operation
(`SetParent` left the parent unchanged and the crop blank). The issue explicitly
acknowledges that some apps do not support reparenting.

The suite therefore uses a deterministic **packaged desktop app**, preserving all
pixel, live-update, interaction and parent/style/restoration assertions. It does
not skip prepared/CI packaged coverage, lower comparison thresholds, or claim that UWP
Windows Settings supports reparenting. UWP-specific compatibility remains outside
this fixture's coverage.

## Build and run

```powershell
tools\build\build.cmd -Path src\modules\CropAndLock\Tests\CropAndLock.UITests -Platform x64 -Configuration Release
```

Use `tools\build\build-essentials.cmd` or a targeted `dotnet restore` if the first
build reports missing assets. Build with the repository script, not `dotnet build`
(the shared harness uses COM references).

The project reference builds `..\CropAndLock.TestApp` for the same x64/ARM64
architecture. Its build target uses Windows SDK `makeappx.exe` to package the
fixture executable, its complete self-contained runtime (including native and
satellite assemblies), runtime configuration and existing PowerToys logo assets.
Packaging checks that CoreCLR/Windows Forms are present and that the runtime
configuration does not request an ambient framework. It stages an unsigned
`CropAndLock.TestApp.msix` beside the
test executable; the existing local/CI signing setup must sign that staged file
before deployment. The fixture app is not a test-runner executable and adds no
entrypoint to `CropAndLock.UITests`.

### Sign before local VM deployment

After building the product runtime and tests, run the existing signer on the
**host**, without adding host trust. These x64 paths assume the normal Release
output layout; use the corresponding ARM64 paths for an ARM64 guest.

```powershell
$tests = (Resolve-Path .\x64\Release\tests\CropAndLock.UITests\net10.0-windows10.0.26100.0).Path
$runtime = (Resolve-Path .\x64\Release).Path
$certificate = 'C:\PowerToysUiTestVm\shared\PowerToysUiTests\CropAndLock\test-signing.cer'

.\.pipelines\signSparsePackages.ps1 `
    -PackageRoot $tests, $runtime `
    -Include CropAndLock.TestApp.msix `
    -RequiredAuthenticodeFile PowerToys.exe, PowerToys.Settings.exe `
    -SkipLocalTrust -ExportCertificatePath $certificate
```

Import the exported **public** certificate only into the running, isolated test
guest, using the configured administrator control channel:

```powershell
$vm = 'PowerToysUiTest-Win10'
Copy-VMFile -VMName $vm -SourcePath $certificate `
    -DestinationPath C:\PowerToysUiTestExchange\CropAndLock\test-signing.cer `
    -FileSource Host -CreateFullPath -Force

.\.github\skills\ui-tests-local-vm\scripts\Invoke-GuestScript.ps1 -VmName $vm -ScriptBlock {
    $certificate = 'C:\PowerToysUiTestExchange\CropAndLock\test-signing.cer'
    Import-Certificate -FilePath $certificate -CertStoreLocation Cert:\LocalMachine\Root
    Import-Certificate -FilePath $certificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople
}
```

Package/deploy the signed files with the local-VM controller after this step.
Repeat signing after every rebuild or restaging that replaces the package or
companions. `-SkipLocalTrust` can report an untrusted signature on the host; the
guest must trust it. Missing this setup skips only the signing-dependent scenarios
in local Test Explorer and fails the prerequisite in CI; unsigned Release
companions cannot exercise authenticated Settings IPC.
After validation, remove the session-added certificate thumbprint from the guest
Root/TrustedPeople stores; do not remove pre-existing certificates.

Run the staged executable **inside the UI-test VM**, not on a working desktop:

```powershell
.\CropAndLock.UITests.exe --report-trx --report-trx-filename CropAndLock.trx --results-directory .\TestResults
```

For a focused iteration, add
`--filter "FullyQualifiedName~ThumbnailWin32ShowsSelectedRegionAndLiveUpdates"`.
Validate the same complete fourteen-test executable (six UI scenarios, four
pixel-comparison checks and four signing-policy checks) on Windows 10 and Windows 11, recording the actual
Windows edition/build. A build or a focused run alone is not end-to-end sign-off.
