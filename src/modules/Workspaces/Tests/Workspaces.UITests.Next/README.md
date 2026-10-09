# Workspaces UI tests

End-to-end coverage for [#40682](https://github.com/microsoft/PowerToys/issues/40682),
using `Microsoft.PowerToys.UITest.Next` and the repository-pinned winappcli.
The legacy `WorkspacesEditorUITest` project is retained unchanged.

## Test design

The suite uses the real Settings/Runner scope with only Workspaces enabled.
Runner and Settings are reused within the class; every case closes the previous
editor/launcher/fixture processes and starts with independent workspace data.
Settings, workspace storage, temporary snapshots, desktop shortcuts, and generated
shortcut icons are journaled and restored after the owning processes stop.
Failure screenshots, recordings, product logs, editor UIA trees, and workspace
JSON are collected before cleanup removes the diagnostic windows.

Settings lifecycle tests drive the real switch and observe Runner persistence and
shortcut behavior. They never repair rejected IPC by writing the enabled map or
restarting Runner. Shortcut tests read the live control, and the customization
case captures a new chord through the real shortcut dialog. The Quick Access
case opens the Runner-owned flyout through its show event, then invokes its
Workspaces tile; it does not substitute direct editor activation for the tile.

Saved-file assertions complement UI assertions rather than replacing them.
Capture reads the native snapshot and compares fixture identity, package identity,
window geometry, and minimized state. Launch cases observe actual fixture windows,
their Workspaces placement stamp, geometry/state, received arguments, and process
identity. Search checks exact inclusion and exclusion; sorting checks order and a
real editor close/reopen. Preview tests compare composed pixels from the visible
preview before/after editing and require removal/add-back to restore the original
image. They do not use OS-specific golden screenshots or assert geometry on
off-screen application rows.

The product changes are automation identifiers only: the workspace-card container
identity (its name, so a card stays addressable after the list re-projects its
items), sort and window-position selectors, the preview image, and the launch
progress/state indicators.

## Fixtures and prerequisites

`..\Workspaces.TestApp` builds one deterministic, resizable Windows Forms app as
both a self-contained unpackaged payload and a **full MSIX**. The test package is
`Microsoft.PowerToys.Workspaces.TestApp`, with application ID `App`. It is installed
for the current user, and its HWND's actual package identity is checked. Cleanup
stops only fixture processes matching the owned executable paths and removes the
owned package registration.

The fixture supports `--title`, `--payload`, `--minimized`, and existing local
`--started-event`, `--wait-event`, and `--ready-event` handles. Startup gates hold
the first window until the test releases it; no sleeps stand in for app readiness.
Launch-progress/cancellation cases use ordered application IDs and same-executable
pending instances because the native launcher iterates an ID-ordered map and
serializes outstanding instances. Cancellation proves that a final pending app
never started while an already-open HWND survives. Dismissal releases the gate
after the UI closes and requires every configured app to appear and be positioned.

Run in an unlocked, English-language **non-elevated** desktop. No Visual Studio,
Store access, third-party applications, or elevated fixture process is needed on
the machine under test. The test fixture is representative packaged/unpackaged
desktop coverage, not a claim of compatibility testing every application named as
an example in the issue.

Placement cases seed each application against the display Workspaces actually
enumerates at launch: `WorkspacesDisplay` resolves the primary monitor to the
product's GDI monitor **number**, effective DPI, and physical/logical work area
(the same contract as `src\common\Display\DisplayUtils.cpp`), and the seed uses
those real values plus a default rectangle kept inside the target work area. This
replaces the previous fixed `monitor = 1` / `(240, 220, 720, 460)` assumption,
which made the native launcher's live-monitor lookup miss and minimize the
window when display 1 was absent. The fixed geometry/DPI assumptions could also
produce incorrect placement on other display configurations.
The class preflight fails fast with actionable guidance when the host cannot support real
placement — for example a disconnected RDP session that reports only
the numberless `WinDisc` pseudo-display — instead of a ~96s minimized-placement
assertion timeout, and before any window is opened or state is mutated.

### Running on the host via Visual Studio Test Explorer

These tests can run directly on a developer machine, not only inside the local VM.
Requirements for a host run:

- A **connected, unlocked, interactive** desktop in an **English** display
  language. A disconnected/locked RDP session (`WinDisc` primary) is rejected up
  front by the placement preflight.
- **Non-elevated** Test Explorer / `testhost`. The suite asserts it is not
  elevated and drives the real standard-user Settings/Runner scope; do not launch
  Visual Studio "as administrator".
- Build the project (and its `Workspaces.TestApp` fixture) with the repository
  script for the platform under test, then let Test Explorer discover the
  `Microsoft.Testing.Platform` executable. Make sure the build selected in Test
  Explorer matches the bits you intend to exercise.
- Build the **product runtime from the same branch** too. Building the UI-test
  project does not rebuild Runner, Settings, or Workspaces Editor. Stale editor
  binaries can lack the automation identifiers used by the suite. When staging
  binaries elsewhere, keep the complete runtime/dependency set, including
  `PowerToys.WorkspacesEditor.dll.config` and the launcher UI configuration;
  copying a lone editor DLL is not a supported deployment.
- **Do not touch the mouse or keyboard** while a case runs — the tests move the
  real cursor and rely on foreground ownership.
- Settings, workspace storage, temporary snapshots, desktop shortcuts, and
  generated icons are journaled before each run and restored afterwards, so a run
  does not lose your existing Workspaces data. Cleanup stops only the fixture and
  Workspaces module processes it owns, never unrelated apps.

The **unpackaged** placement/launch/cancel cases can use matching local Debug
product builds without signing the fixture. The two **packaged** cases report
Inconclusive/Skipped locally when the fixture is unsigned or its root certificate
is untrusted. CI and pipeline-like VM runs remain strict, and malformed packages,
corrupt signatures, missing files, and unrelated deployment failures are never
converted into skips. Package preparation happens before opening the capture
overlay. The tests never add host certificate trust automatically.

For the optional host signing/trust command and cleanup instructions, see
`doc\devdocs\modules\workspaces.md`, **Optional packaged-fixture setup**. Do not
claim full packaged coverage on an unsigned host. Authenticated **Release**
Runner/Settings IPC is a separate prerequisite and remains unchanged.

The full suite needs a signed/trusted `Workspaces.TestApp.msix`. Release
Runner/Settings must have matching versions and valid Microsoft-named signatures.
Quick Access has its own authenticated pipe, so `PowerToys.QuickAccess.exe` must
also satisfy that contract. The existing CI companion/package-signing step covers
all three executables and the fixture. In CI the suite is dispatched through the
shared limited-token UI-test runner, not through an elevated test host.

## Checklist coverage

| Item | Automated scenario or explicit boundary |
| --- | --- |
| 1 | `SettingsLaunchButtonOpensEditor` |
| 2 | `QuickAccessLaunchesEditor` |
| 3 | `ActivationShortcutOpensEditor`; `CustomizedShortcutReachesRunnerAndReplacesTheOldChord` |
| 4 | `DisabledModuleRejectsShortcutUntilReenabled` |
| 5 | `CaptureIncludesPackagedUnpackagedAndMinimizedWindows`, including an app opened after Create Workspace; elevated capture remains manual |
| 6 | `CaptureIncludesPackagedUnpackagedAndMinimizedWindows` compares actual window bounds and minimized-monitor bounds |
| 7 | Manual: capture a genuinely elevated app with elevated PowerToys |
| 8 | `CaptureIncludesPackagedUnpackagedAndMinimizedWindows` saves and verifies the new list entry and stored application IDs |
| 9 | `CancelCaptureLeavesWorkspaceListAndStorageUnchanged`; `CancellingCapturedWorkspaceDiscardsIt` |
| 10 | `SearchFiltersByWorkspaceAndApplicationNames` |
| 11 | `SortOrderPersistsAcrossEditorRestart`, all three sort orders |
| 12 | `SortOrderPersistsAcrossEditorRestart` |
| 13 | `DeleteRemovesOnlyTheSelectedWorkspace` |
| 14 | `WorkspaceCardAndEditMenuOpenEditingPage`, menu case |
| 15 | `WorkspaceCardAndEditMenuOpenEditingPage`, card case |
| 16 | `RemoveAndAddBackUpdatePreviewAndSavedApplicationList` |
| 17 | `RemoveAndAddBackUpdatePreviewAndSavedApplicationList` |
| 18 | `WindowStateChangesUpdatePreviewAndPersist`, minimized case |
| 19 | `WindowStateChangesUpdatePreviewAndPersist`, maximized case |
| 20 | `NameArgumentsAdminAndGeometryEditsPersist` checks the available administrator preference; actual elevated activation is manual |
| 21 | `NameArgumentsAdminAndGeometryEditsPersist`; `CommandLineArgumentsReachUnpackagedAndPackagedApps` |
| 22 | `NameArgumentsAdminAndGeometryEditsPersist` compares preview changes after changing all four position fields |
| 23 | `NameArgumentsAdminAndGeometryEditsPersist` |
| 24 | Save assertions throughout; `CancelAndBreadcrumbDiscardUnsavedEdits`; `CancellingCapturedWorkspaceDiscardsIt` |
| 25 | `CancelAndBreadcrumbDiscardUnsavedEdits`, breadcrumb case |
| 26 | `DesktopShortcutTracksFilePresenceAndLaunchesWorkspace` |
| 27 | `DesktopShortcutTracksFilePresenceAndLaunchesWorkspace`, including externally deleting the shortcut |
| 28 | `LaunchAndEditCaptureAddsWindowsToTheExistingWorkspace` |
| 29 | `LaunchFromEditorRestoresWindowPlacement`, normal/minimized/maximized cases |
| 30 | `DesktopShortcutTracksFilePresenceAndLaunchesWorkspace` activates the actual `.lnk` through the Windows Shell |
| 31 | `LauncherDisplaysLaunchingLaunchedAndFailedStates` |
| 32 | `CancelLaunchStopsPendingAppsAndPreservesOpenedWindows` |
| 33 | `DismissClosesProgressWhileApplicationsContinueLaunching` |
| 34 | Capture and launch cases use the real unpackaged fixture |
| 35 | Manual: UAC and genuinely elevated unpackaged launch |
| 36 | `CommandLineArgumentsReachUnpackagedAndPackagedApps`, unpackaged case |
| 37 | Capture and launch cases use the registered full-MSIX fixture |
| 38 | Manual: UAC and genuinely elevated packaged launch |
| 39 | `CommandLineArgumentsReachUnpackagedAndPackagedApps`, packaged case |
| 40 | Manual: physically connect a second monitor after capture, including mixed DPI |
| 41 | Manual hot-plug matrix; `MissingSavedMonitorUsesTheRemainingDisplay` separately covers replay of a saved, unavailable monitor |

`MoveExistingWindowsReusesTheOriginalProcessAndWindow` additionally checks the
current editor's move-existing-windows option without accepting duplicate launches.
Manual boundaries are documented here, not represented by ignored/inconclusive
tests that would make a full CI run appear complete.

## Build and run

```powershell
tools\build\build.cmd -Path src\modules\Workspaces\Tests\Workspaces.UITests.Next -Platform x64 -Configuration Debug
```

Use a targeted `dotnet restore` when introducing the project or when assets are
missing; build with the repository script, not `dotnet build`, because the shared
harness has COM references. Build `ARM64` as well before CI. The project reference
builds the matching fixture and stages its complete self-contained payload under
`Fixture`, plus `Workspaces.TestApp.msix` beside the MTP test executable.

On a connected, unlocked, non-elevated host you can run the unpackaged cases from
Visual Studio Test Explorer, or directly against the built executable:

```powershell
$tests = (Resolve-Path .\x64\Debug\tests\Workspaces.UITests.Next\net10.0-windows10.0.26100.0).Path
& "$tests\Workspaces.UITests.Next.exe" `
    --filter "FullyQualifiedName~CancelLaunchStopsPendingAppsAndPreservesOpenedWindows" `
    --report-trx --results-directory .\TestResults
```

The placement preflight rejects a disconnected/locked (`WinDisc`) desktop with
actionable guidance before touching any state. Packaged-fixture and Release-IPC
cases still need the signed artifacts below; run those in the VM or CI.

Sign the staged payload before packaging it for a VM. Use the existing signer
without adding trust to the build host:

```powershell
$tests = (Resolve-Path .\x64\Debug\tests\Workspaces.UITests.Next\net10.0-windows10.0.26100.0).Path
$runtime = 'C:\PowerToysUiTestPayload\Product'
$certificate = 'C:\PowerToysUiTestPayload\workspaces-test-signing.cer'

.\.pipelines\signSparsePackages.ps1 `
    -PackageRoot $tests, $runtime `
    -Include Workspaces.TestApp.msix `
    -RequiredAuthenticodeFile PowerToys.exe, PowerToys.Settings.exe, PowerToys.QuickAccess.exe `
    -SkipLocalTrust -ExportCertificatePath $certificate
```

Import only the exported public certificate into the isolated guest's machine
Root/TrustedPeople stores using the existing administrator control channel. Never
create trust from the test executable. Repeat signing after a build/restage
replaces signed files, and remove session-added guest trust after validation.

Inside the prepared VM, the executable can run directly:

```powershell
.\Workspaces.UITests.Next.exe --report-trx --results-directory .\TestResults
```

For the first focused iteration, use
`--filter "FullyQualifiedName~SettingsLaunchButtonOpensEditor"`.
Follow the `ui-tests-local-vm` skill for hash-addressed payload staging, desktop
preflight, durable evidence, and complete Windows 10/Windows 11 runs under both
Default and Constrained profiles. Narrow filters are not full-suite sign-off.
The `ui-tests-pipeline-ci` skill then queues the pushed revision with
`uiTestModules=[Workspaces.UITests.Next]` and verifies the terminal results for
every requested platform.
