# UI tests framework

PowerToys provides UI-test frameworks for modules and Settings. New tests should use
`Microsoft.PowerToys.UITest.Next`, which drives Windows UI Automation through `winappcli` and runs as
a Microsoft.Testing.Platform executable. The legacy `Microsoft.PowerToys.UITest` framework uses
WinAppDriver/Selenium and remains documented for existing suites and migration baselines.

## Agent-assisted workflows

Two repository skills cover the complete implementation and validation loop:

- [UI-tests migration skill](../../../.github/skills/ui-tests-migration/SKILL.md): create new
  `.Next` test projects, port legacy WinAppDriver tests, design stable selectors/waits/lifecycle, and
  prepare tests for CI.
- [Local-VM UI-tests skill](../../../.github/skills/ui-tests-local-vm/SKILL.md): create persistent
  Windows 10 and Windows 11 Hyper-V guests, stage current build/test artifacts, execute tests in a
  standard-user interactive desktop, and collect durable TRX/log/screenshot/video evidence.

For new or migrated tests, use both skills. Build first, then use the local VMs as the default live
agentic loop: run one deterministic test, diagnose and fix it, and finally widen to the complete
module suite on both supported Windows versions.

Module-specific constraints are documented with the module; for example, see the
[PowerRename UI-test notes](../modules/powerrename.md#ui-tests) for command-line selection, Boost
engine lifetime, and signed shell-extension requirements.
Read the [Environment Variables safety notes](../modules/environmentvariables.md#ui-tests) before
running that suite: it temporarily replaces the real User PATH and must run only in a disposable VM,
not on a working machine.

## Before running tests

### `.Next` tests

- Build the PowerToys runtime and `.UITests.Next` test executable.
- Install the pinned `winappcli` runtime or set `WINAPP_CLI_PATH`. The pipeline helper is
  `.pipelines/InstallWinAppCli.ps1`.
- Use a live interactive desktop. UIA, foreground input, Explorer, hotkeys, and rendering do not work
  in session 0.
- Exit an existing PowerToys instance before a host-desktop run. The harness owns the runner and
  module lifecycle.

#### Command Palette (`Microsoft.CmdPal.UITests.Next`)

- Build Command Palette and the test project as **Release**. A Debug Command Palette build is not a
  supported target for these tests. The runner lifecycle case needs release branding
  (`CommandPaletteBranding=Release`) and the corresponding signed `Microsoft.CommandPalette` MSIX
  in the runtime's `WinUI3Apps\CmdPal` directory or its `AppPackages` subtree. A loose-only or
  development-branded app is not a substitute for testing the runner's package activation.
- `ModuleLifecycleTests` installs that package for the test user only when it is absent and removes
  only the installation it created. It drives the real PowerToys Settings toggle and verifies process
  shutdown, startup, and activation-shortcut behavior; it does not seed settings and restart the
  runner to simulate a live toggle.
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
  agent process. Installer-mode runs resolve Command Palette from its registered MSIX package;
  build-artifact runs continue to use the loose output under test.
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
  copies its MSIX from the `Microsoft.WindowsAppSDK.Runtime` NuGet package, and `SamplePagesTests`
  installs it for the current user when a compatible version is missing (for example on clean
  Windows 10 machines).
- Indexer tests create temporary files under `Documents`, an indexed location in Windows Search's
  Classic configuration, and need indexing enabled. Folder actions use `Downloads`; they do not
  depend on the hidden `AppData` directory being indexed.

##### Checklist coverage

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

### Legacy tests

- Install Windows Application Driver v1.2.1 from https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1 to the default directory (`C:\Program Files (x86)\Windows Application Driver`)

- Enable Developer Mode in Windows settings

## Running tests

### `.Next` tests

Build the focused project with the repository script, then run the produced Microsoft.Testing.Platform
executable directly:

```pwsh
tools\build\build.cmd `
  -Path src\modules\<Module>\Tests\<Module>.UITests.Next `
  -Platform x64 `
  -Configuration Debug

$exe = 'x64\Debug\tests\<Module>.UITests.Next\net10.0-windows10.0.26100.0\<Module>.UITests.Next.exe'
& $exe `
  --filter 'TestCategory=<Module>' `
  --report-trx `
  --report-trx-filename module.trx `
  --results-directory .\TestResults\<Module> `
  --timeout 7m
```

Use explicit filter properties such as `Name=`, `Name~`, `FullyQualifiedName~`, or `TestCategory=`.
A bare display name can select zero tests. The `7m` timeout above is a focused-filter example; choose
a larger value for a module or project-wide run.

### Legacy tests

- Exit PowerToys if it's running.

- Open `PowerToys.slnx` in Visual Studio and build the solution.

- Run tests in the Test Explorer (`Test > Test Explorer` or `Ctrl+E, T`).

## Running `.Next` tests in persistent local VMs

The supported local backend is a pair of persistent Hyper-V guests driven through PowerShell Direct:
Windows 10 and Windows 11, each with an already logged-on standard-user desktop. The VMs reveal
first-run, profile, Explorer, WebView2, foreground, and lifecycle assumptions without modifying the
host profile, while retaining staged payloads for a fast edit/build/rerun loop.

### One-time host setup

Scaffold a VM root outside the repository, then follow the generated next steps to create the
untracked configuration:

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVm.ps1 `
  -DestinationRoot C:\PowerToysUiTestVm

pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmHost.ps1 `
  -VmRoot C:\PowerToysUiTestVm `
  -CheckOnly
```

If `-CheckOnly` reports `IsReady=false`, a human must run the elevated setup command it prints.
Hyper-V group membership, the DPAPI-protected guest administrator credential, and guest creation
cannot be completed by an agent. See the [setup reference](../../../.github/skills/ui-tests-local-vm/references/setup.md)
for install media, `vm.config.psd1`, Windows 10/11 guest creation, and baseline checkpoints.

### Run the agentic loop

Create a module exchange containing `ui-tests.zip`, `powertoys-runtime.zip`, `winappcli.zip`, and
`dotnet-runtime.zip` as described in the
[agentic-loop reference](../../../.github/skills/ui-tests-local-vm/references/agentic-loop.md). Payloads
are extracted to guest-local storage; tests are never run directly from a host share.

```pwsh
$vmRoot = 'C:\PowerToysUiTestVm'
$exchange = "$vmRoot\shared\PowerToysUiTests\<Module>"

pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -ConfigurationPath "$vmRoot\vm.config.psd1" `
  -VmRoot $vmRoot `
  -ExchangeRoot $exchange `
  -TestExecutable '<Module>.UITests.Next.exe' `
  -Filter 'Name=<Module>.FocusedTest' `
  -Platform x64Win11 `
  -BuildLabel (git rev-parse HEAD) `
  -SuiteTimeout 15m `
  -TimeoutMinutes 25 `
  -ReuseStagedPayload
```

The controller starts the guest if needed, validates the standard-user token, Explorer session, and
desktop size, then runs the test through a limited interactive scheduled task. It streams progress
and returns `status.json`, TRX counters, per-test failures, logs, screenshots, and retained failure
recordings under `<ExchangeRoot>\LocalVmResults\<runId>`.

After each source change, rebuild and replace only the changed archive, then rerun the same focused
filter with `-ReuseStagedPayload`. Widen only after that behavior is understood. A module is complete
only after the full category filter passes with `executed == total` on both Windows 10 and Windows 11;
restore the baseline checkpoint for the final clean-profile confirmation. See the local-VM
[troubleshooting guide](../../../.github/skills/ui-tests-local-vm/references/troubleshooting.md) for
desktop, PowerShell Direct, payload, shell-extension signing, and evidence failures.

## Running tests in pipeline

The PowerToys UI test pipeline provides flexible options for building and testing:

Full builds with test compilation enabled and UI-tests-only builds first run the
MWB/shared winapp CLI PowerShell infrastructure tests using Pester 3.4.0 under
PowerShell 7. The build publishes NUnit results and fails if any selected test is
missing, skipped or failed. Optional `NativeCompiler` integration tests require
explicit tool/runtime paths and are excluded from this fast gate.

### Pipeline Options

- **buildSource**: Select the build type for testing:
  - `latestMainOfficialBuild`: Downloads and uses the latest official PowerToys build from main branch; does not support Mouse Without Borders Sandbox tests
  - `buildNow`: Builds PowerToys from current source code and uses it for testing
  - `buildNowSlim`: Builds the same current-source product, but downloads and installs its installer rather than the whole artifact
  - `specificBuildId`: Downloads a specific PowerToys build using the build ID specified in `specificBuildId` parameter; does not support Mouse Without Borders Sandbox tests

  **Default value**: `buildNow`

- **specificBuildId**: When `buildSource` is set to `specificBuildId`, specify the exact PowerToys build ID to download and test against.

  **Default value**: `"xxxx"` (placeholder, enter actual build ID when using specificBuildId option)
  
  **When to use this**:
  - Testing against a specific known build for reproducibility
  - Regression testing against a particular build version
  - Validating fixes in a specific build before release
  
  **Usage**: Enter the build ID number (e.g., `12345`) to download that specific build. Only used when `buildSource` is set to `specificBuildId`.

- **uiTestModules**: Specify which UI test modules to build and run. This parameter controls both the `.csproj` projects to build and the `.dll` test assemblies to execute. Examples:
  - `['UITests-FancyZones']` - Only FancyZones UI tests
  - `['MouseUtils.UITests']` - Only MouseUtils UI tests
  - `['UITests-FancyZones', 'MouseUtils.UITests']` - Multiple specific modules
  - Leave empty to build and run all UI test modules

  **Important**: The `uiTestModules` parameter values must match both the test project names (for `.csproj` selection during build) and the test assembly names (for `.dll` execution during testing).

  **Mouse Without Borders**: Selecting `MouseWithoutBorders.UITests` automatically
  prepares its current-source x64 Release runtime bundle and runs the dedicated
  Sandbox suite; there is no separate enable/disable switch. Empty all-modules
  selections include it on `buildNow` and `buildNowSlim`. An explicit MWB selection
  with an official build is blocked; an empty all-modules official selection skips
  MWB with a warning and still runs the other suites. ARM64 Sandbox execution is
  skipped independently of the other ARM64 suites. PR builds prepare the bundle
  only when the affected-project resolver selects MWB. To omit MWB, select the
  other desired modules explicitly.

  MWB executes inside **Run UI Tests** and shares **Publish UI Test Results** with
  the other suites. Its isolated setup/standard-user execution preserves the
  existing timeouts and privilege boundaries. **Restore UI-test prerequisites**
  always attempts recovery and cleanup, even after failure or cancellation.
  Selection/skip reasons are logged; no separate selection artifact is uploaded.
  Allowlisted prerequisite reports are published as **UI-test diagnostics** only
  for failed, canceled or incomplete runs, including setup failures without TRX.
  Successful runs retain their normal test-result attachments without an extra
  prerequisite artifact.

### Build Modes

1. **Official Build Testing** (`buildSource = latestMainOfficialBuild` or `specificBuildId`)
   - Downloads and installs official PowerToys build (latest from main or specific build ID)
   - Builds only UI test projects (all or specific based on `uiTestModules`)
   - Runs UI tests against installed PowerToys
   - Tests both machine-level and per-user installation modes automatically

2. **Current Source Build Testing** (`buildSource = buildNow`)
   - Builds entire PowerToys solution from current source code
   - Builds UI test projects (all or specific based on `uiTestModules`)
   - Runs UI tests against freshly built PowerToys
   - Uses artifacts from current pipeline build

> **Note**: All modes support the `uiTestModules` parameter to control which specific UI test modules to build and run. Both machine-level and per-user installation modes are tested automatically when using official builds.

### Pipeline Access
- Pipeline: https://microsoft.visualstudio.com/Dart/_build?definitionId=161438&_a=summary

## How to add the first UI tests for your modules

Use the [UI-tests migration skill](../../../.github/skills/ui-tests-migration/SKILL.md) for new
`.Next` projects and ports. It contains the current executable project scaffold, API mapping, naming,
CI-stability checklist, and validated examples.

The project sample below describes the **legacy WinAppDriver framework** and is retained for existing
legacy suites. Do not use it as the starting point for a new `.Next` project.

- Follow the naming convention: ![{ModuleFolder}/Tests/{ModuleName}-{TestType(Fuzz/UI/Unit)}Tests](images/uitests/naming.png)
- Create a new project and add the following references to the project file. Change the OutputPath to your own module's path.
  ```
    <Project Sdk="Microsoft.NET.Sdk">
    <!-- Look at Directory.Build.props in root for common stuff as well -->
    <Import Project="..\..\..\Common.Dotnet.CsWinRT.props" />

    <PropertyGroup>
        <ProjectGuid>{4E0AE3A4-2EE0-44D7-A2D0-8769977254A0}</ProjectGuid>
        <RootNamespace>PowerToys.Hosts.UITests</RootNamespace>
        <AssemblyName>PowerToys.Hosts.UITests</AssemblyName>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <Nullable>enable</Nullable>
        <OutputType>Library</OutputType>

        <!-- This is a UI test, so don't run as part of MSBuild -->
        <RunVSTest>false</RunVSTest>
        </PropertyGroup>
        <PropertyGroup>
        <OutputPath>$(SolutionDir)$(Platform)\$(Configuration)\tests\Hosts.UITests\</OutputPath>
        </PropertyGroup>

        <ItemGroup>
        <PackageReference Include="MSTest" />
        <ProjectReference Include="..\..\..\common\UITestAutomation\UITestAutomation.csproj" />
        </ItemGroup>
    </Project>

  ```
- Inherit your test class from UITestBase.
  >Set Scope: The default scope starts from the PowerToys settings UI. If you want to start from your own module, set the constructor as shown below:
  
  >Specify Scope:
  ```
    [TestClass]
    public class HostModuleTests : UITestBase
    {
        public HostModuleTests()
            : base(PowerToysModule.Hosts, WindowSize.Small_Vertical)
        {
        }
    }
  ```

- Then you can start performing the UI operations.

**Example**
```
[TestMethod("Hosts.Basic.EmptyViewShouldWork")]
[TestCategory("Hosts File Editor #4")]
public void TestEmptyView()
{
    this.CloseWarningDialog();
    this.RemoveAllEntries();

    // 'Add an entry' button (only show-up when list is empty) should be visible
    Assert.IsTrue(this.HasOne<HyperlinkButton>("Add an entry"), "'Add an entry' button should be visible in the empty view");

    VisualAssert.AreEqual(this.TestContext, this.Find("Entries"), "EmptyView");

    // Click 'Add an entry' from empty-view for adding Host override rule
    this.Find<HyperlinkButton>("Add an entry").Click();

    this.AddEntry("192.168.0.1", "localhost", false, false);

    // Should have one row now and not more empty view
    Assert.IsTrue(this.Has<Button>("Delete"), "Should have one row now");
    Assert.IsFalse(this.Has<HyperlinkButton>("Add an entry"), "'Add an entry' button should be invisible if not empty view");

    VisualAssert.AreEqual(this.TestContext, this.Find("Entries"), "NonEmptyView");
}
```

### Visual assertions in UITestAutomation.Next

The `.Next` harness compares complete, equal-sized window captures using local
structural similarity (SSIM), with a default threshold of **95%**. It does not
resize images, crop their frames, or mask pixels. The existing embedded baseline
images are used unchanged, and capture retries remain bounded to 15 seconds.
The legacy harness shown above retains its original comparison behavior.

The score is the pixel-weighted mean of non-overlapping 8-by-8 blocks, including
partial blocks at the image edges. Each RGB channel uses local mean, variance
and covariance with the standard 8-bit SSIM stabilizers `(0.01 * 255)^2` and
`(0.03 * 255)^2`. Alpha is compared independently; the lower of the RGB and alpha
scores is used, so an opaque capture cannot pass by becoming transparent.
Dimension mismatches are retried during settling and reported explicitly if they
persist.

This replaces the `.Next` harness's former 64-bit average hash. That hash's
1.5625-percentage-point steps were not percentages of matching pixels: a small
frame-color change could alter the global average and flip unrelated hash bits,
while uniform black and white images could have identical hashes. Local SSIM
keeps changes local and detects missing preview structure. Regression tests cover
blank images, removed content, inverted structure, color and alpha changes,
dimension mismatches, and partial edge blocks. Peek PNG uses the same 95%
threshold as the other previews; there is no architecture-specific exception.

## Extra tools and information

 **Accessibility Tools**:
While working on tests, you may need a tool that helps you to view the element's accessibility data, e.g. for finding the button to click. For this purpose, you could use [AccessibilityInsights](https://accessibilityinsights.io/docs/windows/overview).
