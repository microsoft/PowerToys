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

### Elevated EXE signature warning

For entries explicitly marked **Run as administrator**, Workspaces checks the
actual EXE file selected by the existing launch routing. An EXE with a verified
signature continues to the normal Windows elevation flow. An unsigned,
untrusted, invalid, or inconclusive result shows **Skip this app** (the default)
and **Run anyway** in the Workspaces launch UI. Enter initially selects Skip;
Escape and closing the warning also skip that application. Other applications
continue. **Cancel launch**
still cancels the workspace. Choices are one-time, not a saved trust allowlist.

**Only EXE file targets are checked.** MSIX/AppX activation, `shell:AppsFolder`,
URI protocols, shortcuts, and other non-EXE targets keep their existing launch
behavior without this warning. Bypassing the check does not mean that a target
has been verified or is trusted. If normal routing falls back to an EXE, that
EXE is checked before it is launched. Implicit elevation of entries not marked
**Run as administrator** is also outside this feature.

`WorkspacesLib\SignatureVerification` uses Windows Authenticode verification
for embedded signatures (including secondary signatures) and installed
SHA-256/SHA-1 catalogs. Signer and timestamp chains must terminate at
machine-trusted roots; the publisher does not have to be Microsoft.
Certificate and revocation retrieval are **cache-only**: missing or unusable
cached revocation data produces a warning, not automatic approval.

The checked executable is held open without write/delete sharing through
`ShellExecuteEx`. This reduces file replacement races, but execution remains
path-based: mutable parent directories, arguments, scripts, dependencies, and
runtime behavior are not covered. A trusted signature establishes signing
identity and signed-content integrity, not that an application is safe.

### Warning lifecycle

The warning uses the existing `IPCHelper` / `TwoWayPipeMessageIPCManaged` pipe
pair. The launch-status payload and workspace cancellation message are
unchanged. Additional application messages carry a warning, its display
acknowledgement, its one-time decision, and dismissal, matched by request ID.
This PR does not replace or authenticate that transport. The dialog is an
advisory check, not an authorization boundary against other same-user processes;
Windows UAC and execution policies remain in control.

The launcher waits up to 10 seconds for the warning to be displayed. Once it
is displayed, there is no human decision deadline while the UI process remains
alive. No response, an exited UI, invalid messages, or a display timeout can
approve the EXE. Skip and confirmation failure stop fallback for that
application. Pending decisions are canceled before the launch worker is joined
at shutdown. A keepalive on the existing Window Arranger channel prevents its
normal launch timeout from expiring while elevation verification or consent is
pending; it is not a UI transport heartbeat.

### Localization

Warning strings and translator comments are in the launcher's
`Properties\Resources.resx`, with strongly typed access through
`Resources.Designer.cs`. The existing Touchdown workflow collects these
resources, and the installer includes Launcher UI satellite assemblies.
Translated strings still require delivery through the normal localization
process.

The warning respects the selected language and right-to-left layout, with
left-to-right technical values and wrapping action labels. Displayed application
details escape control characters and bidi formatting controls to avoid
misleading line breaks and direction overrides. **Copy details** preserves the
raw path and arguments. These display escapes do not alter the launched values.

## Debugging

Build `WorkspacesLib.UnitTests`, `WorkspacesLauncherUI.UnitTests`,
`WorkspacesLauncher`, `WorkspacesLauncherUI`, and `WorkspacesWindowArranger`
with the repository build wrapper, using the same configuration and platform:

```powershell
# Run from the project directory; $repo is the repository root.
& "$repo\tools\build\build.ps1" -Platform x64 -Configuration Debug -ExtraArgs "/p:SolutionDir=$repo\"
```

After a successful build, use Visual Studio Test Explorer or
`vstest.console.exe` for the unit-test assemblies. Native tests cover EXE target
classification, signature outcomes, held-file behavior, and one-time
approval/skip/cancel/failure. Managed tests cover the warning's actual lifecycle,
display, default actions, and localization. There are no product command-line
verification or warning-preview switches.

Trust regressions compile the production verifier with test-only Windows API
seams for signature/provider results, catalog discovery, and final-path forms.
They exercise embedded/secondary signatures, catalog matches, chain failures,
and timestamps. Windows chain and Authenticode policy evaluation use a private
memory-only root store; no machine trust, installed catalogs, or network state
is modified. These controlled tests are not end-to-end signed-binary acceptance.

For end-to-end acceptance, use a harmless unsigned EXE in a saved workspace,
mark it **Run as administrator**, and exercise Skip, Run anyway, Escape/close,
and workspace cancellation. Also confirm that ordinary launches and MSIX/URI
activation retain their original behavior. Use matching signed product binaries
for Release Runner/Settings; this feature does not bypass their authentication.
Installed Release, accessibility, and delivered-language acceptance remain
separate from unit-test coverage.

## Settings

TODO: Add settings documentation

## Future Improvements

TODO: Add potential future improvements
