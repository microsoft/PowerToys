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
- [Windows Sandbox UI-tests skill](../../../.github/skills/windows-sandbox-ui-tests/SKILL.md): enable
  and launch Windows Sandbox, package current build/test artifacts, execute tests in a clean
  interactive desktop, collect TRX/logs/screenshots, compare revisions, and tear down automatically.

For new or migrated tests, use both skills. Build first, then use Windows Sandbox as the default live
agentic loop: run one deterministic test, diagnose and fix it, and finally widen to the module suite.

## Before running tests

### `.Next` tests

- Build the PowerToys runtime and `.UITests.Next` test executable.
- Install the pinned `winappcli` runtime or set `WINAPP_CLI_PATH`. The pipeline helper is
  `.pipelines/InstallWinAppCli.ps1`.
- Use a live interactive desktop. UIA, foreground input, Explorer, hotkeys, and rendering do not work
  in session 0.
- Exit an existing PowerToys instance before a host-desktop run. The harness owns the runner and
  module lifecycle.

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

## Running `.Next` tests in Windows Sandbox

Windows Sandbox provides a disposable interactive desktop and clean user profile. It is the preferred
local environment for the agentic creation/migration loop because it reveals first-run, profile,
Explorer, WebView2, foreground, and process-lifecycle assumptions without changing the host profile.

### Enable Sandbox

From an elevated PowerShell window:

```pwsh
Enable-WindowsOptionalFeature `
  -Online `
  -FeatureName Containers-DisposableClientVM `
  -All `
  -NoRestart
```

Reboot if requested. Verify `wsb.exe`, the Store Sandbox package, and Start AppID as described in the
[Sandbox skill setup reference](../../../.github/skills/windows-sandbox-ui-tests/references/setup.md).

### Run the agentic loop

Create a dedicated exchange containing zipped test output, PowerToys runtime, winappcli, a private
.NET runtime, the guest template, and optional signed WebView2 installer. Do not map the repository or
run tests directly from a mapped folder; extract archives to guest-local storage.

Stage those archives before invoking the controller; see the
[agentic loop payload steps](../../../.github/skills/windows-sandbox-ui-tests/references/agentic-loop.md).

```pwsh
$exchange = 'C:\Temp\PowerToysSandbox\<Module>'
pwsh .github\skills\windows-sandbox-ui-tests\scripts\Invoke-SandboxUiTest.ps1 `
  -ExchangeRoot $exchange `
  -TestExecutable '<Module>.UITests.Next.exe' `
  -Filter 'TestCategory=<Module>' `
  -Platform x64Win11 `
  -BuildLabel (git rev-parse HEAD) `
  -CleanupProcess 'PowerToys.<Module>.UI' `
  -ProcessorAffinityMask 0x3 `
  -InstallWebView2
```

The controller launches Sandbox through its registered Start-menu AppID, waits for the interactive
`WDAGUtilityAccount` login, dynamically shares the lean exchange, runs as `ExistingLogin`, streams
progress, returns `status.json` and TRX artifacts, and stops the exact guest in `finally`.

By default, the guest runner and its directly launched descendants (the test host, PowerToys,
winappcli, and module processes) are limited to logical processors 0 and 1 with affinity mask `0x3`.
Pass another mask to select a different CPU set or `0` to disable affinity limiting. Sandbox has no
supported vCPU-count/VM-affinity setting; changing host `WindowsSandboxServer.exe` affinity does not
throttle guest execution. The mask selects guest vCPUs and limits process concurrency; it does not
pin the Sandbox VM to those same numbered host CPUs.

For a fast edit/build/rerun loop, retain the first guest with `-KeepSandbox`. After rebuilding, replace
only the changed archive in the exchange and rerun with the returned `SandboxId`,
`-ReuseSandboxId`, and `-ReuseStagedPayload`. Per-component hashes refresh only changed tests,
product, winappcli, or .NET files; unchanged SDK/runtime payloads and WebView2 stay staged. No guest
service is required because `wsb share` and `wsb exec` provide the file and command channels. Use a
fresh Sandbox for final clean-profile validation.

Timeouts are independently adjustable. The Sandbox controller defaults to a two-hour guest suite and
a 150-minute host deadline so broad project runs can complete. Tighten both for focused/module runs,
or increase both for a known longer suite; the host deadline must include startup, staging, execution,
and result export. WebView/Monaco tests need WebView2 provisioning. Sandbox window size is not
configurable, so preserve visual baselines/thresholds and use the matching CI/VM display for final
pixel sign-off.

See the complete [agentic loop](../../../.github/skills/windows-sandbox-ui-tests/references/agentic-loop.md)
and [troubleshooting guide](../../../.github/skills/windows-sandbox-ui-tests/references/troubleshooting.md).

## Running tests in pipeline

The PowerToys UI test pipeline provides flexible options for building and testing:

### Pipeline Options

- **buildSource**: Select the build type for testing:
  - `latestMainOfficialBuild`: Downloads and uses the latest official PowerToys build from main branch
  - `buildNow`: Builds PowerToys from current source code and uses it for testing
  - `specificBuildId`: Downloads a specific PowerToys build using the build ID specified in `specificBuildId` parameter

  **Default value**: `latestMainOfficialBuild`

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

## Extra tools and information

 **Accessibility Tools**:
While working on tests, you may need a tool that helps you to view the element's accessibility data, e.g. for finding the button to click. For this purpose, you could use [AccessibilityInsights](https://accessibilityinsights.io/docs/windows/overview).
