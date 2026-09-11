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

### Elevated-launch signature warning (V1)

Before each application launch explicitly marked **Run as administrator**, the
native launcher verifies the actual launch target. Verified signatures continue
to the normal Windows elevation flow. Other results use one launcher-UI dialog
with a specific reason and **Skip this app** (the default) / **Run anyway** buttons.
Enter initially selects Skip; Escape and closing the warning also skip that
application, without trying another launch fallback. Other applications continue.
The launcher's separate **Cancel launch** action cancels the entire workspace.
An override applies only to that pending target and arguments; there is no saved
approval, trust allowlist, or signature-verdict cache.

`WorkspacesLib\SignatureVerification` uses Authenticode verification, including
embedded secondary signatures and installed SHA-256/SHA-1 catalog membership.
The signer and any timestamp countersigners must also chain to machine-trusted
roots; no Microsoft-only publisher restriction is applied. Revocation retrieval
is **cache-only**. Windows may reuse locally cached certificate intermediates and
CRL/OCSP revocation information; this check does not request online retrieval.
Missing or unusable cached revocation information is
reported as unavailable, never silently accepted. The publisher is taken only
from a successfully verified signature.

The verifier keeps the executable open without write/delete sharing through
`ShellExecuteEx`. This reduces file-replacement races, but does not constitute
handle-based execution or eliminate races involving mutable parent directories.
Protocols, shortcuts, application aliases, and other unresolved targets require
confirmation rather than inheriting a verdict from the workspace's saved path.
Implicit elevation caused by a manifest or shell association on an entry not
marked **Run as administrator** is outside this signature gate.

The launcher remains non-elevated. Its UI approval messages are advisory, bound
to a single pending request, and are not an authorization mechanism for an
elevated broker. Windows UAC and execution policies remain in control. Signatures
do not establish that arguments, scripts, loaded dependencies, or the requested
operation are safe. The internal Window Arranger still starts before application
launches and retains its existing UAC flow; its launch timeout receives a bounded
heartbeat while application verification or consent is pending.

### Confirmation channel and failure behavior

The native Launcher creates a unique local duplex pipe before starting its UI.
It authenticates the exact spawned UI process, executable directory, basename,
and matching file version, with the existing Microsoft-signature requirement in
Release. Debug exempts only that Microsoft-signature requirement for local builds.
The reused peer authenticator also disables online revocation retrieval, not just
online intermediate/root retrieval. No shared IPC API or ABI is changed.
The UI independently checks the OS-reported pipe server PID, its actual parent,
the sibling Launcher image and matching version, and the parent's lifetime.
No application details are sent before the authenticated UI reports readiness.

The versioned protocol uses strict UTF-8 JSON in bounded length-prefixed frames
(4 MiB maximum). Each one-time warning has a new request ID. A response is accepted
only after the UI acknowledges displaying that request; early, stale, duplicate,
or replayed responses cannot approve a launch. Connection/readiness and warning
display have 10-second deadlines. A UI-thread heartbeat must arrive at least
every 10 seconds while the warning is displayed. Human decision time is otherwise
unlimited. Partial frames and writes have bounded waits.

Skipping an application is distinct from canceling the workspace. Missing,
disconnected, malformed, or unresponsive confirmation UI, display timeout, and
changed package identity fail that launch rather than masquerading as a user
decision. None permit an alternate launch fallback. Windows UAC rejection also
stops fallback. Workers and pipe readers are owned and stopped before launcher
state is destroyed; child processes are tracked by retained process handles.

This channel is defense in depth for an advisory warning, not a new security
boundary against same-user process injection or a compromised local installation.
It does not change the Window Arranger's separate existing IPC protocol.

### Installed MSIX/AppX targets

Valid `shell:AppsFolder\<AUMID>` targets use a separate package-verification path.
The launcher resolves the AUMID against the current user's registered packages
and their application entries, not the workspace's saved package name or path.
The original AUMID activation string is preserved.

Automatic approval currently requires a fully available Store- or System-signed
package, no development-mode registration, no external/mutable payload, a healthy
package status, and a successful `VerifyContentIntegrityAsync` result. Application
enumeration and content verification have bounded, cancelable waits. Registration
of each asynchronous completion handler happens only once; subsequent waits use
the same event so operations taking longer than a polling interval do not trigger
`E_ILLEGAL_DELEGATE_ASSIGNMENT`. The handler retains its event through any late
completion after cancellation or timeout. Registration
identity (including package version and installed/effective/external/mutable paths) is checked again
after verification and before execution; changes fail that launch without a
fallback.

`SignatureKind.Developer` does not mean development mode or malicious code.
Developer/Enterprise-signed packages are reported as unverified under the current
launch policy until a machine-level package-signing policy is implemented. This
deliberately does not silently replace the EXE verifier's machine-root policy with
current-user package deployment trust. Sparse/external-content packages, stubs,
unhealthy deployments, unavailable packages, and failed integrity checks also retain
reason-specific confirmation.

## Debugging

Build `WorkspacesLib.UnitTests`, `WorkspacesLauncherUI.UnitTests`,
`WorkspacesLauncher`, `WorkspacesLauncherUI`, and `WorkspacesWindowArranger`
using `tools\build\build.ps1` from each project folder.
Use the same configuration/platform for all projects, and pass the repository root
as `SolutionDir` so native resource generation resolves its scripts correctly:

```powershell
& "$repo\tools\build\build.ps1" -Platform x64 -Configuration Debug -ExtraArgs "/p:SolutionDir=$repo\"
```

Use Debug for interactive unsigned local Runner/Settings builds; Release Settings
IPC requires the official Microsoft signature. App-signature and package checks
remain active in Debug.

From the resulting **Debug** output directory, inspect a file without launching it:

```powershell
.\PowerToys.WorkspacesLauncher.exe --verify-signature 'C:\Windows\System32\cmd.exe' | Out-String
.\PowerToys.WorkspacesLauncher.exe --verify-signature 'shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App' | Out-String
```

Diagnostic and standalone warning-preview switches are compiled out of Release;
Release rejects those invocations. The JSON result includes `status`, `reason`, `windowsStatus`, `publisher`, `source`,
and the resolved `path`. Package results use `source: "msix"` and include
`packageFullName` and `applicationUserModelId` when resolution succeeds.
An unavailable or rejected result is a completed
diagnostic operation, not a request to launch the file.

Preview the actual warning dialog without using workspace files, live IPC, or
elevation:

```powershell
.\PowerToys.WorkspacesLauncherUI.exe --preview-signature-warning unsigned
```

To exercise the real authenticated Launcher/UI channel and warning lifecycle,
use the native Debug preview with an unverified fixture:

```powershell
.\PowerToys.WorkspacesLauncher.exe --preview-signature-warning 'C:\SignatureFixtures\Unsigned.exe' | Out-String
```

This verifies the target and shows the warning, but never calls the application
launch path, starts the Window Arranger, or edits workspace files. Both choices
only emit a `previewOnly: true` result and close the preview; even **Run anyway**
does not execute the target. This switch is also unavailable in Release.

Other standalone UI preview reasons are `invalid-signature`, `certificate-untrusted`, `revoked`,
`explicit-distrust`, `expired`, `revocation-unavailable`, `unresolved-target`, and
`verification-unavailable`. Package reasons include `package-not-found`,
`package-development`, `package-external-content`, `package-integrity-failed`,
`package-unavailable`, `package-unsigned`, `package-signing-policy`,
`package-verification-unavailable`, and `package-changed`.
Both preview buttons only close the preview; when
standard output is redirected, the preview emits the same one-time response JSON
as the real dialog. It never sends live IPC or launches the displayed target.

Native tests cover failure classification, confirmed unsigned PE files,
malformed/missing targets, file sharing, one-time approval/skip/cancellation,
display and heartbeat deadlines, terminal failure/no-fallback behavior, AUMID
parsing, package policy combinations, registration identity changes, and framed
pipe transport. Reproducible harmless unsigned/self-signed/tampered test applications
are generated by `WorkspacesLib.UnitTests\SignatureFixtures`; no private keys or
binaries belong in source control, and the fixtures do not install trusted roots.

### Localization and release acceptance

Warning strings and translator context live in the launcher's
`Properties\Resources.resx`. Keep resource keys stable and regenerate
`Properties\Resources.Designer.cs` when adding or removing keys; the warning and
launch-state models use these strongly typed properties instead of string-key
lookups. Keep paths, arguments, package identities, and Windows status codes
verbatim. They are data, not translatable messages.

Both launch windows use the selected UI culture for language and text direction.
The warning's technical values remain left-to-right in RTL languages. Action
labels wrap within the available window width, while the warning body scrolls
independently, so expanded translations do not push consent buttons off-screen.
Application details are display text rather than input controls, with field
labels associated with their values for automation. **Copy details** in the
expanded Details section copies the displayed information, including raw paths,
arguments, and the Windows status code, without changing the launch decision.

The existing Touchdown workflow
(`.pipelines\loc\loc.yml`) exports these source resources; Microsoft's localization
team supplies translated resources through the normal localization process.
Release preparation downloads them and produces language-specific satellite
assemblies. `installer\PowerToysSetupVNext\Resources.wxs` explicitly includes
`PowerToys.WorkspacesLauncherUI.resources.dll` for each pipeline language.
English resources and packaging support do not mean translated strings have
already been delivered.

#### Translation handoff

No new `LocProject.json`, language list, or pipeline resource allowlist is needed:
`WorkspacesLauncherUI\Properties\Resources.resx` already matches the current
Touchdown globs. The older CDPX/XLoc instructions in the general localization
document describe a previous workflow, not the onboarding required here.

1. Merge the English resources, generated designer, translator comments, and
   installer component through the normal PR review. The scheduled localization
   pipeline watches `main`; an unmerged local branch is not automatically
   submitted for translation. A maintainer must arrange any pre-merge submission
   through the authorized localization workflow.
2. Have the localization team process the new keys. In particular, preserve the
   distinction between **Skip this app** and **Cancel launch**, between
   inconclusive verification and explicit distrust, and between a one-time
   decision and permanently trusting an application. Comments beside each
   warning resource provide this context.
3. Use the release preparation step
   `.pipelines\v2\templates\steps-fetch-and-prepare-localizations.yml` to download
   the approved resources. `tools\build\move-and-rename-resx.ps1` moves
   `Properties\<culture>\Resources.resx` to `Properties\Resources.<culture>.resx`.
   The SDK-style launcher UI project includes these automatically and emits
   `<culture>\PowerToys.WorkspacesLauncherUI.resources.dll`.
4. Confirm the expected satellite files exist before the pipeline installer
   build. The component is under the existing `IsPipeline` localization loop
   and installs them next to the launcher in its culture subfolders, not under
   `WinUI3Apps`. The existing `*.resources.dll` entry in
   `.pipelines\ESRPSigning_core.json` covers their signing.
5. Verify the installed UI in the delivered languages. A missing translation
   falls back to the neutral English resource; fallback prevents missing text
   but must not be reported as a completed translation.

`WorkspacesLauncherUI.UnitTests` covers resource-key access, reason mappings,
satellite loading and English fallback, RTL technical values, expanded action
labels, and safe default buttons. Its synthetic language resources are test
fixtures, not translated product resources or inputs to Touchdown.
After building that project, run
`x64\<Configuration>\tests\WorkspacesLauncherUI\PowerToys.WorkspacesLauncherUI.UnitTests.dll`
with `vstest.console.exe`. These in-process tests do not display windows, connect
to the launcher, or read or modify the user's settings.

Relevant implementation precedents:

- [#45194](https://github.com/microsoft/PowerToys/pull/45194) adds an omitted
  module's satellite assemblies to `Resources.wxs`.
- [#41152](https://github.com/microsoft/PowerToys/pull/41152) fixes satellite
  installation paths to match the application's actual output location.
- [#48649](https://github.com/microsoft/PowerToys/pull/48649) adds translator
  guidance directly to resource comments.
- [#15054](https://github.com/microsoft/PowerToys/pull/15054) introduces the
  Touchdown localization pipeline. Current authentication/configuration should
  be taken from the checked-in pipeline, not copied from that historical diff.

Before shipping, the authorized release/localization pipelines must still verify:

- Microsoft-signed Release Launcher/UI authentication end to end, EXE and MSIX
  launch paths, UAC rejection, cancellation, UI exit/hang, and no launch fallback.
- Installed satellite resources, upgrade/uninstall behavior, translated and
  pseudo-localized layouts, RTL, keyboard focus, Narrator, high contrast, and DPI.
- Known-good embedded/catalog/timestamped signatures and offline revocation
  outcomes against the supported Windows versions and managed-machine policies.

Local Debug tests and unsigned Release builds do not substitute for these
official signing, installer, and localization acceptance gates.

## Settings

TODO: Add settings documentation

## Future Improvements

TODO: Add potential future improvements
