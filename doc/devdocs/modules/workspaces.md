# Workspaces

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/workspaces)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-Workspaces)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3AProduct-Workspaces)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-Workspaces)

## Overview

Workspaces is a PowerToys module that allows users to save and restore window layouts for different projects or workflows.

## Links

- [Source code folder](https://github.com/microsoft/PowerToys/tree/main/src/modules/Workspaces)
- [Issue tracker](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+label%3AWorkspaces)

## UI tests

`src\modules\Workspaces\Tests\Workspaces.UITests.Next` uses the winappcli-based
`UITestAutomation.Next` harness. It runs through Visual Studio Test Explorer or
the built Microsoft.Testing.Platform executable. Build the product runtime from
the same branch as the tests, use an English-language, connected and unlocked
desktop, and run Visual Studio/testhost **without elevation**. Keep an RDP
connection open and avoid mouse/keyboard input while the UI cases execute.

```powershell
# Run from the repository root; use ARM64 instead of x64 when appropriate.
tools\build\build.cmd -Path src\modules\Workspaces\Tests\Workspaces.UITests.Next -Platform x64 -Configuration Debug
```

The test-project reference builds `Workspaces.TestApp` and stages both an
unpackaged fixture and `Workspaces.TestApp.msix`. The package is **unsigned by
default**, so ordinary local builds do not require certificate installation.

These two cases need the packaged fixture:

- `CaptureIncludesPackagedUnpackagedAndMinimizedWindows`
- `CommandLineArgumentsReachUnpackagedAndPackagedApps (True)`

If the MSIX has no signature, or Windows rejects its certificate chain with
`CERT_E_UNTRUSTEDROOT`, these cases report **Inconclusive/Skipped locally**, with
the missing prerequisite and a pointer to this section. Package preparation is
attempted before opening the capture overlay. Other cases, including the
unpackaged command-line variant, remain enabled.

CI and pipeline-like VM runs (`TF_BUILD=true` or a nonempty `platform` environment
variable) are strict: missing signing is a **failure**, not a skip. Missing build
outputs, malformed archives, hash/signature corruption, access errors, and other
deployment/product failures also remain failures. The tests never sign packages,
install trust, or change security policy automatically.

### Optional packaged-fixture setup

Running the packaged cases locally is straightforward, but it requires an
explicit test-certificate trust setup. The Windows SDK's `signtool.exe` is needed;
the repository signer locates it or uses the existing pinned SDK-tools mechanism.
A disposable UI-test VM is preferable if you do not want test trust on a working
machine.

1. Build the test project **before** signing.
2. From an **elevated PowerShell terminal using the same Windows account**, run
   the existing signer below. This signs only `Workspaces.TestApp.msix` copies in
   the selected test output tree and records the certificate for cleanup. It adds
   a test trust anchor to the machine Root/TrustedPeople stores; do not use that
   certificate for production signing.
3. Run the two cases again from **non-elevated** Test Explorer.

```powershell
# Elevated PowerShell, repository root. Adjust platform/configuration as needed.
$testRoot = (Resolve-Path .\x64\Debug\tests).Path
$marker = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestSigning\Workspaces-certificates.txt'

.\.pipelines\signSparsePackages.ps1 `
    -PackageRoot $testRoot `
    -Include Workspaces.TestApp.msix `
    -RequiredPackage Workspaces.TestApp.msix `
    -CertificateMarkerPath $marker
```

Both the fixture project's output and the copy beside the UI-test runner are
signed, so ordinary copy staging does not immediately replace the signed fixture
with an unsigned one. A rebuild that regenerates the MSIX still requires signing
again. A Windows error of `0x800B0100` means the package is unsigned;
`0x800B0109` means the signing root is not trusted on the execution machine.

When finished, remove the recorded test trust/private key from the same elevated
account:

```powershell
$marker = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestSigning\Workspaces-certificates.txt'
.\.pipelines\removeTestSigningCertificates.ps1 -CertificateMarkerPath $marker
```

For VM execution, use the `ui-tests-local-vm` skill: sign on the build host with
`-SkipLocalTrust`, export only the public certificate, and trust it in the guest
instead. Release Runner/Settings/Quick Access authentication is a separate
prerequisite: use matching signed product binaries or the existing CI companion
signing setup. The package-only command above does not bypass product IPC
authentication.

The suite README documents the full checklist mapping, fixture ownership, and
evidence/cleanup behavior:
`src\modules\Workspaces\Tests\Workspaces.UITests.Next\README.md`.

## Implementation Details

TODO: Add implementation details

## Debugging

TODO: Add debugging information

## Settings

TODO: Add settings documentation

## Future Improvements

TODO: Add potential future improvements
