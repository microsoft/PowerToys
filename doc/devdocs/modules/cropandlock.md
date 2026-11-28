# Crop and Lock

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/crop-and-lock)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-CropAndLock)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3AProduct-CropAndLock)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-CropAndLock)

## Overview

The Crop and Lock module in PowerToys allows users to crop a current application into a smaller window or create a thumbnail. This utility enhances productivity by enabling users to focus on specific parts of an application window.

## Features

### Thumbnail Mode
Creates a window showing the selected area of the original window. Changes in the original window are reflected in the thumbnail.

### Reparent Mode
Creates a window that replaces the original window, showing only the selected area. The application is controlled through the cropped window.

### Screenshot Mode
Creates a window showing a freezed snapshot of the original window. 

## Code Structure

### Project Layout
The Crop and Lock module is part of the PowerToys solution. All the logic-related settings are in the main.cpp. The main implementations are in ThumbnailCropAndLockWindow and ReparentCropAndLockWindow. ChildWindow and OverlayWindow distinguish the two different modes of windows implementations.

### Key Files
- **ThumbnailCropAndLockWindow.cpp**: Defines the UI for the thumbnail mode.
- **OverlayWindow.cpp**: Thumbnail module type's window concrete implementation.
- **ReparentCropAndLockWindow.cpp**: Defines the UI for the reparent mode.
- **ChildWindow.cpp**: Reparent module type's window concrete implementation.
- **ScreenshotCropAndLockWindow.cpp**: Defines the UI for the screenshot mode.

## Known Issues

- Cropping maximized or full-screen windows in "Reparent" mode might not work properly.
- Some UWP apps may not respond well to being cropped in "Reparent" mode.
- Applications with sub-windows or tabs can have compatibility issues in "Reparent" mode.

## Debug
1. build the entire project
2. launch the built Powertoys
3. select CropAndLock as the startup project in VS
4. In the debug button, choose "Attach to process". ![image](https://github.com/user-attachments/assets/a7624ec2-63f1-4720-9540-a916b0ada282)
5. Attach to CropAndLock.![image](https://github.com/user-attachments/assets/08aa0465-596c-4494-9daa-e96b234f9997)

## UI tests: prepared run

A **prepared run** executes all 14 tests, including the two packaged-app scenarios
and the authenticated Settings lifecycle scenario. Building alone is not enough:
`CropAndLock.TestApp.msix` must be signed and trusted, and the Settings client must
have a trusted Microsoft-publisher signature for authenticated IPC. Without that
setup, those three cases are skipped/inconclusive in local Test Explorer; CI and
pipeline-like VM runs fail the prerequisite instead.

> **Use a disposable test VM or a dedicated test machine.** The test signer creates
> a temporary certificate with a Microsoft publisher identity and adds it to
> trusted certificate stores. This is test-only trust, not a production signature.
> Do not add this trust to an ordinary working machine or share the test signer
> with unrelated work. Signing/setup needs elevation; the tests themselves must
> run **non-elevated**.

### Build the matching runtime and tests

Use an unlocked English-language desktop with .NET 10 Desktop Runtime, winappcli,
and, for Test Explorer, Visual Studio. Building/signing also requires the Windows
SDK tools (`makeappx.exe` and `signtool.exe`). Start with a complete PowerToys
Release runtime built for the target architecture, not just the test project.

Run these examples from the repository root. They use x64; substitute ARM64 for
an ARM64 runtime/test build.

```powershell
tools\build\build.cmd -Path src\modules\CropAndLock\Tests\CropAndLock.UITests -Platform x64 -Configuration Release
```

The test project builds the self-contained fixture and stages its initially
unsigned MSIX beside `CropAndLock.UITests.exe`. See the
[UI-test README](../../../src/modules/CropAndLock/Tests/CropAndLock.UITests/README.md)
for restore prerequisites and fixture details.

### Prepare signing on the execution machine

For Test Explorer on a **disposable machine containing the build**, close
PowerToys and run the following from an **elevated PowerShell 7** terminal at the
repository root:

```powershell
$runtime = (Resolve-Path .\x64\Release).Path
$tests = (Resolve-Path .\x64\Release\tests\CropAndLock.UITests\net10.0-windows10.0.26100.0).Path
$marker = Join-Path $env:TEMP 'CropAndLock-UITestSigning.txt'

.\.pipelines\signSparsePackages.ps1 `
    -PackageRoot $runtime `
    -Include CropAndLock.TestApp.msix `
    -RequiredPackage CropAndLock.TestApp.msix `
    -RequiredAuthenticodeFile PowerToys.exe, PowerToys.Settings.exe `
    -CertificateMarkerPath $marker

Get-AuthenticodeSignature -LiteralPath `
    (Join-Path $tests 'CropAndLock.TestApp.msix'), `
    (Join-Path $runtime 'PowerToys.exe'), `
    (Join-Path $runtime 'WinUI3Apps\PowerToys.Settings.exe') |
    Select-Object Path, Status, SignerCertificate
```

All three files must report `Valid`. The script searches the runtime tree, so it
also signs the fixture project's MSIX output that may later be copied into the
test output. **Repeat signing after a rebuild or restaging replaces any of these
files.** Merely rebuilding in Debug does not establish the suite's authenticated
signing prerequisite.

### Run without elevation

Close the elevated terminal. Open Visual Studio **normally, not as administrator**,
select `CropAndLock.UITests` in Test Explorer, and run the tests. If Test Explorer
rebuilds/restages the outputs, repeat the signing step before rerunning.

Alternatively, run from a **non-elevated** PowerShell terminal at the repository
root:

```powershell
$env:POWERTOYS_INSTALL_DIR = (Resolve-Path .\x64\Release).Path
$tests = (Resolve-Path .\x64\Release\tests\CropAndLock.UITests\net10.0-windows10.0.26100.0).Path

& (Join-Path $tests 'CropAndLock.UITests.exe') `
    --report-trx --report-trx-filename CropAndLock.trx `
    --results-directory (Join-Path $env:TEMP 'CropAndLockTestResults')
```

Expect **14 executed, 14 passed, zero skipped**: six UI scenarios, four
pixel-comparison checks, and four signing-policy checks. A signing prerequisite
message means the run is not prepared; an input, pixel, installation, or lifecycle
assertion remains a real failure and must not be bypassed.

### Build on the host and execute in a local VM

The preferred isolation workflow keeps build tools on the host and runs only the
runtime/tests in the guest. In that case, **do not use the trust-installing signing
command above on the host**. Use `signSparsePackages.ps1` with `-SkipLocalTrust`
and `-ExportCertificatePath`, then import only the exported public certificate in
the guest. The
[host-signing and guest-trust recipe](../../../src/modules/CropAndLock/Tests/CropAndLock.UITests/README.md#sign-before-local-vm-deployment)
contains the exact commands.

Create or refresh `ui-tests.zip` and `powertoys-runtime.zip` **after signing**,
alongside architecture-matched `winappcli.zip` and `dotnet-runtime.zip`. Follow the
[local-VM execution guide](../../../.github/skills/ui-tests-local-vm/references/agentic-loop.md)
for the payload layout, `-PlanOnly`, and `Invoke-LocalVmUiTest.ps1` invocation.
Use `-TestExecutable CropAndLock.UITests.exe` and the correct `-Platform`:
`x64Win10`, `x64Win11`, or `ARM64`. VM runs remain strict about prerequisites.

### Remove temporary trust

After the same-machine prepared run, close PowerToys/tests and return to an
**elevated PowerShell 7** terminal at the repository root:

```powershell
.\.pipelines\removeTestSigningCertificates.ps1 `
    -CertificateMarkerPath (Join-Path $env:TEMP 'CropAndLock-UITestSigning.txt')
```

This removes the recorded test certificates from trust stores and removes their
private keys; use it only for the disposable signer used by this run. For the
separate-host/guest workflow, remove only the trust entries added to the guest,
without deleting a pre-existing host signing key. Restoring the disposable VM's
baseline checkpoint is another way to return to a clean test environment.
