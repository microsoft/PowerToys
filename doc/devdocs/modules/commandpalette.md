# Command Palette

Command Palette is an extensible launcher for applications, commands, files, and Windows utilities.
See the [module README](../../../src/modules/cmdpal/README.md) for building the application and
creating extensions.

## UI tests

`src\modules\cmdpal\Tests\Microsoft.CmdPal.UITests.Next` drives Command Palette through
`Microsoft.PowerToys.UITest.Next` and `winappcli`. See the
[shared UI-test framework guide](../development/ui-tests.md) for general build, local-VM, and CI
workflows.

### Setup

- Build Command Palette and the test project as **Release**. A Debug Command Palette build is not a
  supported target for these tests. Set `CommandPaletteBranding=Release` for both
  `Microsoft.CmdPal.UI` and `CmdPalModuleInterface`; Release configuration alone does not select
  stable branding. The runner lifecycle case needs the corresponding signed
  `Microsoft.CommandPalette` MSIX in the runtime's `WinUI3Apps\CmdPal` directory or its `AppPackages`
  subtree. A loose-only or development-branded app is not a substitute for testing the runner's
  package activation.
- The assembly fixture installs that package for the test user only when it is absent and removes
  only the installation it created. A pre-existing package is preserved only when its version
  matches the runtime MSIX; a mismatch fails explicitly rather than replacing the user's package.
  `ModuleLifecycleTests` drives the real PowerToys Settings toggle and verifies process shutdown,
  startup, and activation-shortcut behavior; it does not seed settings and restart the runner to
  simulate a live toggle.
- The test project builds `SamplePagesExtension` and packages its layout as an unsigned
  `SamplePagesExtension.msix` next to the test executable. Sign this package and trust its signing
  certificate on the test machine before running the suite. `SamplePagesTests` installs it for the
  current user and removes it after the class; it preserves an extension installed beforehand.
  **Developer Mode is not required for this extension deployment.** Windows policy must permit
  sideloading trusted app packages; certificate trust does not override a policy that blocks it.
- CI uses `.pipelines\signSparsePackages.ps1` to sign and trust packages during elevated setup,
  with `.pipelines\removeTestSigningCertificates.ps1` in the always-running cleanup step. Missing or
  untrusted sample packages fail setup for CmdPal jobs on both Windows 10 and Windows 11. The
  selected project also opts into the shared authenticated-Settings-IPC setup, which signs both
  `PowerToys.exe` and `PowerToys.Settings.exe` so Release lifecycle assertions exercise the production
  authentication path. The suite uses the existing `runUiTestAsUser.ps1` dispatch so packaged COM
  activation runs on the same non-elevated interactive desktop as local validation, not the elevated
  agent process. Installed-layout fallback resolves Command Palette from its registered MSIX package
  even when `useInstallerForTest` is unset and uses registered app activation instead of directly
  executing its WindowsApps file. Build-artifact runs still prefer the loose output under test.
- For a local host-desktop run, execute the same signing helper from an elevated terminal before
  running the tests as the normal test user (replace `<test-output>` with the executable's directory):

  ```powershell
  .\.pipelines\signSparsePackages.ps1 -PackageRoot "<test-output>", "<runtime-root>" `
    -RequiredPackage SamplePagesExtension.msix `
    -RequiredAuthenticodeFile PowerToys.exe, PowerToys.Settings.exe `
    -CertificateMarkerPath "$env:TEMP\CmdPalUiTestSigning.txt"
  ```

  Afterwards, run `.pipelines\removeTestSigningCertificates.ps1` elevated with the same marker path.
  For VM runs, sign on the host with `-SkipLocalTrust -ExportCertificatePath "<exchange>\cmdpal-test.cer"`,
  using a host-side marker and omitting `-RequiredPackage` (it verifies local trust, which is
  intentionally absent on the host). Verify the signature is `Valid` in the guest after importing
  the certificate. Transfer only the signed MSIX and public `.cer` (never the private key).
  Import the public certificate into `Cert:\LocalMachine\Root` and
  `Cert:\LocalMachine\TrustedPeople` during elevated guest setup. Record its exact thumbprint in a
  guest-side marker and use the cleanup helper in both environments after the run, including on
  failure. Keep guest-side markers outside `C:\PowerToysUiTestRun`, which the VM runner replaces
  when refreshing the payload. This is per-run test trust, not a change to the saved VM baseline.
- The extension depends on the `Microsoft.WindowsAppRuntime.2` framework package. The test project
  copies its MSIX from the `Microsoft.WindowsAppSDK.Runtime` NuGet package. `SamplePagesTests`
  deploys the bundled framework for the current user and preserves a compatible newer installation.
- Indexer tests create temporary files and uniquely named folders under `Documents`, an indexed
  location in Windows Search's Classic configuration, and need indexing enabled. Folder actions
  use test-owned indexed folders; they do not depend on the hidden `AppData` directory being indexed.

### Checklist coverage

The `.Next` project lives beside the unchanged legacy project. It ports its basic-provider,
Indexer, Dock, Settings-search, and sample-extension keyboard cases. The expanded workflows use
test-owned files, shortcuts, windows, and bookmarks; live configuration tests preserve the same
Command Palette process and restore changed settings.

Coverage below refers to [the Command Palette checklist, #40685](https://github.com/microsoft/PowerToys/issues/40685).
Command discovery alone is not treated as successful execution, and manual cases are not represented
by ignored or inconclusive automated tests.

| Checklist items | Automated evidence / remaining manual scope |
| --- | --- |
| 1-2: PowerToys installation/uninstallation | Manual installer validation. The package deployment fixture is not a PowerToys installer test. |
| 3, 31: unelevated startup, enable/disable, activation shortcut | `ModuleLifecycleTests.SettingsToggleControlsProcessAndActivationShortcut` verifies real Settings IPC, process lifetime, activation, disabled-hotkey behavior, and re-enablement. |
| 4-5, 7, 15, 19: elevated startup/restart/application actions | Manual administrator/UAC scenarios, outside the standard-user desktop suite. |
| 6, 8: Win32 and packaged application launch | `CommandWorkflowTests.InstalledAppsLaunchesWin32Application` and `InstalledAppsLaunchesPackagedSettings` verify launched windows. |
| 9: Calculator result and Enter-to-copy | `BasicTests.BasicCalculatorTest` checks exact results for addition and arithmetic precedence, then exact clipboard contents. |
| 10-12: file open, copy path, show containing folder | `IndexerTests` checks primary and double-click actions, the complete copied path, and exact Shell selection/focus after Show in folder. |
| 13: Run commands | `CommandWorkflowTests.RunCommandsExecutesLocalCommand` executes a local command and verifies its uniquely named output file; no network dependency. |
| 14: Window Walker | `CommandWorkflowTests.WindowWalkerActivatesSelectedWindow` verifies the exact owned target HWND becomes foreground. |
| 16-17: WinGet installation and web search | Manual external-service/browser workflows; this suite does not install third-party applications or depend on web responses. |
| 18: Windows Terminal profile launch | Manual. `BasicWindowsTerminalTest` covers command discovery only, not profile execution. |
| 20: Windows Settings | `BasicTests.BasicWindowsSettingsTest` opens the selected extension result and verifies the Settings application appears. |
| 21-22: Registry navigation/editor/copy | `BasicTests.BasicRegistryTest` browses the current-user Software key and copies its exact path. Registry Editor launch remains manual. |
| 23: service start/stop/restart | Manual privileged mutation. `BasicWindowsServicesTest` verifies service discovery only. |
| 24: now/year/week | `BasicTests` verifies the current local time, year, and calendar week; year/week actions also verify clipboard contents. |
| 25-28: lock, empty Recycle Bin, shutdown, network-adapter copy | Manual. Destructive/session-disrupting actions are not executed by the suite; system-command discovery is additional smoke coverage, not execution coverage. |
| 29-30: bookmarks | `CommandWorkflowTests.BookmarkCanBeAddedOpenedAndDeleted` adds a local-folder bookmark through its form, opens the folder, and confirms deletion during cleanup. |
| 32-34: extension enablement, command hotkey, alias | `SettingsSearchTests` verifies Calculator command visibility, command-specific global-shortcut activation, and alias activation without restarting Command Palette. |
| 35: all-extensions-disabled warning | Not automated: the checklist explicitly marks this behavior unsupported. |
