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

The `.Next` value-setting wrappers pass target options before `--`, followed by the literal
selector and value. Keep this boundary when adding wrappers: application arguments such as
`--payload "text"` and negative numeric values must not be parsed as winapp options.

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

### Optional virtual HID input

Some modules intentionally reject injected input. In a dedicated test VM with the PowerToys
UMDF virtual HID test driver already provisioned, opt the existing helpers into real device input.

For Grab and Move's complete local build, self-signing, provisioning, execution and cleanup
workflow, see the [module-specific testing guide](../modules/grabandmove.md#ui-tests).

For example:

```csharp
using var input = UmdfHidInput.Activate();
KeyboardHelper.PressKey(Key.Alt);
try
{
    MouseHelper.Drag(300, 300, 396, 364);
}
finally
{
    KeyboardHelper.ReleaseKey(Key.Alt);
}
```

The scope is process-wide; input tests must not run in parallel. Disposal releases held device
keys/buttons and restores the default helpers. Activation fails if the driver is missing, with no
`SendInput` fallback. The current report descriptor supports six simultaneous non-modifier keys,
primary-monitor mouse coordinates, and whole wheel detents (120 units).

Run driver-backed gestures on an active, unlocked **console** test desktop, not inside RDP or
Hyper-V Enhanced Session. The current backend is not an RDP-session input injector: the device
can open while its input never reaches that session's cursor. Use a physical console or Hyper-V
Basic Session; a host-side RDP connection is fine when tests execute in a guest's console desktop.

Call `MouseHelper.MoveTo` explicitly before click, wheel, or relative movement sequences. The HID
backend retains the last requested position because feature-report completion can precede the OS
cursor update; it does not track physical/injected mouse movement during the scope. Keyboard-only
scopes do not reposition the mouse. Restore any original off-primary position after disposing the
scope, when the default mouse backend is active again.

Driver provisioning is separate, elevated test infrastructure; tests themselves run non-elevated.
The test package targets UMDF 2.31 on Windows 10 2004+ and Windows 11 and is never shipped in the
PowerToys installer. The Microsoft sample-derived driver is MS-PL licensed; its attribution and
license accompany the test artifacts. ARM64 requires a separate, isolated CI image preconfigured
with TESTSIGNING enabled. Other ARM64 suites and all x64 suites retain their ordinary images.

> **Security:** provision this driver only in disposable, isolated test VMs without real credentials.
> While installed, local code can emit hardware-style keyboard/mouse input, including input to
> elevated desktops. Do not install it on a developer workstation. Do not change ordinary test
> images' boot/signing policies. Only the dedicated ARM64 image is provisioned for TESTSIGNING by
> its owner; the pipeline never enables it, disables Secure Boot/HVCI, or reboots to change policy.
> Remove the device, package and temporary trust immediately after testing. Activation never
> installs a driver or changes machine trust.

Build the test-only package on the build host (Visual Studio C++ tools and the SDK are required):

```pwsh
.\.pipelines\umdf-hid-smoke\Build-Smoke.ps1 `
  -WorkRoot C:\HidBuild\work -OutputRoot C:\HidBuild\artifacts -Platforms x64 -DriverOnly
```

Copy the `x64` artifact directory and `Manage-SmokeDriver.ps1` into the dedicated VM. In the VM's
separate administrator control session, provision the package with a persistent ownership journal:

```pwsh
.\Manage-SmokeDriver.ps1 -Action Install -ArtifactDirectory C:\HidTest\x64 `
  -StatePath C:\HidTest\state.json -ResultsDirectory C:\HidTest\results
```

Run the built module executable in the logged-on **standard-user** desktop, using the normal
runtime/winappcli setup described above:

```pwsh
.\GrabAndMove.UITests.exe --report-trx --results-directory C:\HidTest\test-results
```

Always clean up from the administrator control session, including after a failed install/test:

```pwsh
.\Manage-SmokeDriver.ps1 -Action Cleanup `
  -StatePath C:\HidTest\state.json -ResultsDirectory C:\HidTest\results
```

Cleanup only removes journaled test trust and owned present devices/packages, and retains the
journal on failure. Preserve that journal until cleanup succeeds. CI provisions automatically for
the x64 Grab and Move suite. Implicit ARM64 selections skip that suite with an infrastructure
warning while the dedicated image is unconfigured; explicitly requesting it reports
`BLOCKED_INFRASTRUCTURE` rather than a passing zero-test run.

#### Dedicated ARM64 Grab and Move CI image

The `umdfHidArm64Image` template parameter is declared by
`.pipelines\v2\templates\pipeline-ui-tests-automation.yml` and `pipeline-ci-build.yml`, then passed
explicitly through the stage/job templates so it is available during structural expansion:

- Manual internal UI-test runs default to `SHINE-W11-Preview-Testing-arm64-TestSigning` through
  `ImageOverride`. The image parameter permits only that name or `disabled` to turn off routing;
  root templates translate `disabled` to the internal empty-image convention.
- PR CI enables the image only in the verified internal collection (`SHINE-INT-Testing-arm64`).
  The shared CI template defaults to disabled; public `shine-oss` PR CI explicitly keeps it
  disabled until the image is provisioned and verified there. Implicit ARM64 selections then
  skip Grab and Move with a warning while other suites still run.
- Image isolation is explicit: ordinary jobs retain the pool's default image. Do not make the
  TESTSIGNING image the pool default. Registering the image in an OSS pool alone does not opt
  public CI into it; its root pipeline configuration must also be updated.
- The dedicated English-language Windows 11 ARM64 image needs TESTSIGNING enabled before boot,
  an elevated agent, and the same logged-on standard-user desktop/UI-test prerequisites as the
  ordinary test image. The job checks the architecture and existing boot configuration; it
  does not alter them. Actual driver-load readiness is still checked separately.

Once configured, PR selection, full builds and official/specific-build runs route only
`GrabAndMove.UITests` to a separate `_UmdfHid` job. All/mixed selections keep every other suite on
the ordinary pool. Official/specific-build runs retain both machine-level and per-user jobs.
Native ARM64 driver artifacts are built on the ordinary build agents, never installed there.
The experimental ARM64 transport-smoke path also explicitly selects the TESTSIGNING image.

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
