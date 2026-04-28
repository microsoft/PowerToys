# PowerToys Run Standalone Repository Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a self-contained `microsoft/PowerToysRun` repository that builds, installs, and runs PowerToys Run independently of the PowerToys main repo, preserving all current functionality and binary plugin compatibility.

**Architecture:** Single-process WPF app (`PowerToys.PowerLauncher.exe`) absorbs the previous runner responsibilities (hotkey, tray, autostart, single-instance). Settings UI is rewritten as a WPF window inside the same process using built-in WPF controls. Common PowerToys dependencies are vendored as a trimmed subset under `src/Common/`. PT-specific systems (GPO, telemetry, runner IPC) are removed entirely. Distribution via WiX dual-mode MSI + winget.

**Tech Stack:** .NET 8, WPF, WiX 4, GitHub Actions, MSBuild, NuGet. Supplementary: PowerShell scripts for localization extraction. **No new runtime dependencies** — drops WinUI3 / CommunityToolkit.WinUI / Windows App SDK that the previous Settings UI relied on.

**Reference spec:** [`docs/superpowers/specs/2026-04-28-powertoys-run-standalone-repo-design.md`](../specs/2026-04-28-powertoys-run-standalone-repo-design.md)

**Source repo paths**: All references like `<PT>/src/modules/launcher/...` mean the existing PowerToys main repo (e.g., `c:\Users\yuleng\source\repos\ptrun\src\modules\launcher\...`). The work is done in a **new** repo, conventionally referred to in this plan as `<PTRUN>` (root of `microsoft/PowerToysRun`).

---

## Phase 0: Repository Bootstrap

### Task 0.1: Create new repo from launcher subtree using git filter-repo

**Files:**
- Create: `<PTRUN>` (entire new repo via git filter-repo)

- [ ] **Step 1: Install git-filter-repo if not present**

```bash
pip install git-filter-repo
git filter-repo --version
```

Expected: prints version (>=2.38).

- [ ] **Step 2: Clone PT main repo to a working directory**

```bash
git clone https://github.com/microsoft/PowerToys.git PowerToysRun-bootstrap
cd PowerToysRun-bootstrap
```

- [ ] **Step 3: Run filter-repo to keep only launcher subtree paths**

```bash
git filter-repo \
  --path src/modules/launcher/PowerLauncher/ \
  --path src/modules/launcher/Wox.Plugin/ \
  --path src/modules/launcher/Wox.Infrastructure/ \
  --path src/modules/launcher/Plugins/ \
  --path src/modules/launcher/Wox.Test/ \
  --path src/modules/launcher/LICENSE \
  --invert-paths --path src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.PowerToys/
```

Note: the second `--invert-paths` removes the to-be-deleted `Microsoft.PowerToys.Run.Plugin.PowerToys` plugin from history.

Expected: filter-repo rewrites history. Repo size should drop dramatically (from ~GBs to ~hundreds of MBs).

- [ ] **Step 4: Move launcher contents up to repo root**

```bash
git filter-repo --path-rename src/modules/launcher/:src/
```

After this, the layout is `src/PowerLauncher/`, `src/Wox.Plugin/`, etc.

- [ ] **Step 5: Verify resulting layout**

```bash
ls src/
```

Expected output: `LICENSE  Plugins  PowerLauncher  Wox.Infrastructure  Wox.Plugin  Wox.Test`

- [ ] **Step 6: Create empty stub directories that will be filled in later phases**

```bash
mkdir -p src/Common/ManagedCommon src/Common/Common.UI src/Common/Settings.Library
mkdir -p installer/PowerToysRunSetup
mkdir -p winget/manifest
mkdir -p doc
mkdir -p tools
mkdir -p .github/workflows
touch src/Common/.gitkeep installer/PowerToysRunSetup/.gitkeep winget/manifest/.gitkeep doc/.gitkeep tools/.gitkeep
```

- [ ] **Step 7: Create new GitHub repo `microsoft/PowerToysRun` and push**

(Out-of-band manual step; assume the GitHub repo exists.)

```bash
git remote remove origin
git remote add origin https://github.com/microsoft/PowerToysRun.git
git push -u origin main
```

Expected: push succeeds.

- [ ] **Step 8: Commit stub directories**

```bash
git add src/Common/.gitkeep installer/PowerToysRunSetup/.gitkeep winget/manifest/.gitkeep doc/.gitkeep tools/.gitkeep .github/workflows/
git commit -m "chore: scaffold top-level directories for standalone repo"
```

### Task 0.2: Add LICENSE, README, and .gitignore at root

**Files:**
- Create: `<PTRUN>/LICENSE` (MIT, copied from PT main repo root)
- Create: `<PTRUN>/README.md`
- Create: `<PTRUN>/.gitignore`

- [ ] **Step 1: Copy LICENSE from PT main repo root**

Source: `<PT>/LICENSE` (MIT). Copy verbatim to `<PTRUN>/LICENSE` and update copyright year if needed.

- [ ] **Step 2: Write minimal README.md**

```markdown
# PowerToys Run

Standalone repository for PowerToys Run, the keyboard launcher (Alt+Space).

This repository was split from [microsoft/PowerToys](https://github.com/microsoft/PowerToys) on 2026-04-28.

## Build

Open `PowerToysRun.sln` in Visual Studio 2022 (with .NET 8 workload). Build configuration: `Release | x64`.

## Install

Download the latest MSI from [Releases](https://github.com/microsoft/PowerToysRun/releases) or run:

```
winget install Microsoft.PowerToysRun
```

## License

MIT — see [LICENSE](LICENSE).
```

- [ ] **Step 3: Copy .gitignore from PT main repo root, trim unrelated entries**

Source: `<PT>/.gitignore`. Copy and remove entries that reference modules/paths not present in the new repo (e.g., `installer/PowerToysSetup/`, modules other than launcher).

- [ ] **Step 4: Commit**

```bash
git add LICENSE README.md .gitignore
git commit -m "chore: add LICENSE, README, .gitignore"
```

### Task 0.3: Create root Directory.Build.props

**Files:**
- Create: `<PTRUN>/Directory.Build.props`
- Reference: `<PT>/Directory.Build.props`, `<PT>/src/Common.Dotnet.CsWinRT.props`, `<PT>/src/Common.SelfContained.props`

- [ ] **Step 1: Create Directory.Build.props at repo root**

```xml
<Project>
  <PropertyGroup>
    <RepoRoot>$(MSBuildThisFileDirectory)</RepoRoot>
    <Configurations>Debug;Release</Configurations>
    <Platforms>x64;arm64</Platforms>
    <LangVersion>latest</LangVersion>
    <Nullable>annotations</Nullable>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Company>Microsoft Corporation</Company>
    <Copyright>Copyright (c) Microsoft Corporation. All rights reserved.</Copyright>
    <NeutralLanguage>en-US</NeutralLanguage>
  </PropertyGroup>

  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.20348.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
  </PropertyGroup>

  <PropertyGroup>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier Condition="'$(Platform)' == 'x64'">win-x64</RuntimeIdentifier>
    <RuntimeIdentifier Condition="'$(Platform)' == 'arm64'">win-arm64</RuntimeIdentifier>
    <PublishSingleFile>false</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Commit**

```bash
git add Directory.Build.props
git commit -m "chore: add root Directory.Build.props (net8.0, self-contained)"
```

### Task 0.4: Create empty solution file

**Files:**
- Create: `<PTRUN>/PowerToysRun.sln`

- [ ] **Step 1: Generate empty sln from CLI**

```bash
dotnet new sln -n PowerToysRun
```

- [ ] **Step 2: Commit**

```bash
git add PowerToysRun.sln
git commit -m "chore: add empty PowerToysRun.sln"
```

---

## Phase 1: Vendor Common Dependencies

### Task 1.1: Vendor `ManagedCommon` (trimmed)

**Files:**
- Create: `<PTRUN>/src/Common/ManagedCommon/ManagedCommon.csproj`
- Create: `<PTRUN>/src/Common/ManagedCommon/*.cs` (subset of `<PT>/src/common/ManagedCommon/`)
- Reference: `<PT>/src/common/ManagedCommon/`

- [ ] **Step 1: Copy `<PT>/src/common/ManagedCommon/` into `<PTRUN>/src/Common/ManagedCommon/`**

```bash
cp -r <PT>/src/common/ManagedCommon/* src/Common/ManagedCommon/
```

- [ ] **Step 2: Delete files that are not needed in standalone**

Delete:
- `RunnerHelper.cs` (waited for runner exit; no runner now)

- [ ] **Step 3: Modify `PowerToysPathResolver.cs` to use new install path**

Current code resolves via PT install registry. Replace with:

```csharp
namespace ManagedCommon
{
    public static class PowerToysPathResolver
    {
        public static string GetPowerToysInstallPath()
        {
            // For standalone PowerToys Run: install path = directory of current executable
            return AppContext.BaseDirectory;
        }
    }
}
```

- [ ] **Step 4: Open `ManagedCommon.csproj` and remove any ProjectReference to PT-specific projects**

Strip references to `PowerToys.Interop`, `GPOWrapperProjection`, `Telemetry` if any. The csproj should be a plain class library:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <UseWPF>false</UseWPF>
    <RootNamespace>ManagedCommon</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.CsWinRT" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Add ManagedCommon.csproj to solution**

```bash
dotnet sln PowerToysRun.sln add src/Common/ManagedCommon/ManagedCommon.csproj
```

- [ ] **Step 6: Build to surface errors**

```bash
dotnet build src/Common/ManagedCommon/ManagedCommon.csproj
```

Expected: may have errors due to references to removed types. Fix iteratively (delete unused helpers, replace `RunnerHelper` calls with no-ops).

- [ ] **Step 7: Commit**

```bash
git add src/Common/ManagedCommon/
git commit -m "feat: vendor trimmed ManagedCommon"
```

### Task 1.2: Vendor `Common.UI` (trimmed)

**Files:**
- Create: `<PTRUN>/src/Common/Common.UI/Common.UI.csproj`
- Create: `<PTRUN>/src/Common/Common.UI/*.cs`

- [ ] **Step 1: Copy `<PT>/src/common/Common.UI/` to `<PTRUN>/src/Common/Common.UI/`**

```bash
cp -r <PT>/src/common/Common.UI/* src/Common/Common.UI/
```

- [ ] **Step 2: Delete `SettingsDeepLink.cs`**

It only opens PT Settings UI — no consumers in launcher after deleting `Microsoft.PowerToys.Run.Plugin.PowerToys`.

```bash
rm src/Common/Common.UI/SettingsDeepLink.cs
```

- [ ] **Step 3: Open `Common.UI.csproj`, ensure ProjectReference to ManagedCommon points to new path**

Change `..\..\..\common\ManagedCommon\ManagedCommon.csproj` → `..\ManagedCommon\ManagedCommon.csproj`.

- [ ] **Step 4: Add to solution**

```bash
dotnet sln PowerToysRun.sln add src/Common/Common.UI/Common.UI.csproj
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Common/Common.UI/Common.UI.csproj
```

Expected: green.

- [ ] **Step 6: Commit**

```bash
git add src/Common/Common.UI/
git commit -m "feat: vendor trimmed Common.UI (drop SettingsDeepLink)"
```

### Task 1.3: Vendor `Settings.UI.Library` subset

**Files:**
- Create: `<PTRUN>/src/Common/Settings.Library/Settings.Library.csproj`
- Create: subset of `<PT>/src/settings-ui/Settings.UI.Library/*.cs`

- [ ] **Step 1: Inventory which classes are actually used in launcher**

Run from `<PT>` repo root:

```bash
grep -r "Microsoft.PowerToys.Settings.UI.Library" src/modules/launcher/ --include="*.cs" -l | xargs -I {} grep -h "^using\|: I\| : \(Settings\|PluginAdditionalOption\|HotkeySettings\|PowerLauncher\|GeneralSettings\|SettingsRepository\|SettingsUtils\)" {} | sort -u
```

Expected: list of types like `PowerLauncherSettings`, `PowerLauncherProperties`, `PowerLauncherPluginSettings`, `PluginAdditionalOption`, `HotkeySettings`, `KeyboardKeys`, `SettingsUtils`, `GeneralSettings`, `SettingsRepository<T>`, `Helper`, `IFileSystemWatcher`, `JsonSerializerOptions` constants, `BasePTSettingsViewModel`, etc.

- [ ] **Step 2: Copy the identified files from `<PT>/src/settings-ui/Settings.UI.Library/` to `<PTRUN>/src/Common/Settings.Library/`**

Concrete subset (verify each by grep before copying):
- `PowerLauncherSettings.cs`
- `PowerLauncherProperties.cs`
- `PowerLauncherPluginSettings.cs`
- `PluginAdditionalOption.cs`
- `HotkeySettings.cs`
- `KeyboardKeys.cs`
- `SettingsUtils.cs`
- `GeneralSettings.cs`
- `SettingsRepository.cs`
- `BasePTSettingsViewModel.cs`
- `Interfaces/ISettingsUtils.cs`, `Interfaces/IIOProvider.cs`, etc.
- `Utilities/JsonOptions.cs` if exists

Skip files specific to other modules: `FancyZonesSettings.cs`, `KeyboardManagerSettings.cs`, `ColorPickerSettings.cs`, etc.

- [ ] **Step 3: Skim each copied file and remove references to types not vendored**

Examples:
- Anything referring to `GPOWrapper` → delete or stub out
- Anything referring to `PowerToys.Telemetry` → delete

Build will surface these as errors; fix iteratively.

- [ ] **Step 4: Create `Settings.Library.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <RootNamespace>Microsoft.PowerToys.Settings.UI.Library</RootNamespace>
    <AssemblyName>Microsoft.PowerToys.Settings.UI.Library</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ManagedCommon\ManagedCommon.csproj" />
  </ItemGroup>
</Project>
```

**CRITICAL**: `RootNamespace` and `AssemblyName` must remain `Microsoft.PowerToys.Settings.UI.Library` to satisfy Q4 binary compatibility.

- [ ] **Step 5: Add to solution**

```bash
dotnet sln PowerToysRun.sln add src/Common/Settings.Library/Settings.Library.csproj
```

- [ ] **Step 6: Build, fix iteratively**

```bash
dotnet build src/Common/Settings.Library/Settings.Library.csproj
```

Expected: errors that surface unused dependencies. Resolve by deleting code paths that reference deleted-type members.

- [ ] **Step 7: Commit**

```bash
git add src/Common/Settings.Library/
git commit -m "feat: vendor Settings.Library subset (PowerLauncher* + base types)"
```

---

## Phase 2: Get Baseline Compilation Green

### Task 2.1: Wire up `Wox.Plugin` to vendored common deps

**Files:**
- Modify: `<PTRUN>/src/Wox.Plugin/Wox.Plugin.csproj`

- [ ] **Step 1: Open `src/Wox.Plugin/Wox.Plugin.csproj`**

Replace existing PT-relative ProjectReference paths with new layout:

```xml
<ItemGroup>
  <ProjectReference Include="..\Common\ManagedCommon\ManagedCommon.csproj" />
  <ProjectReference Include="..\Common\Common.UI\Common.UI.csproj" />
  <ProjectReference Include="..\Common\Settings.Library\Settings.Library.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Wox.Plugin/Wox.Plugin.csproj
```

Expected: green.

- [ ] **Step 3: Add to solution**

```bash
dotnet sln PowerToysRun.sln add src/Wox.Plugin/Wox.Plugin.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/Wox.Plugin/Wox.Plugin.csproj
git commit -m "fix: rewire Wox.Plugin ProjectReference to vendored common"
```

### Task 2.2: Wire up `Wox.Infrastructure`

**Files:**
- Modify: `<PTRUN>/src/Wox.Infrastructure/Wox.Infrastructure.csproj`

- [ ] **Step 1: Replace ProjectReferences**

```xml
<ItemGroup>
  <ProjectReference Include="..\Wox.Plugin\Wox.Plugin.csproj" />
  <ProjectReference Include="..\Common\ManagedCommon\ManagedCommon.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Build, fix any namespace errors (likely Telemetry or GPOWrapper imports)**

```bash
dotnet build src/Wox.Infrastructure/Wox.Infrastructure.csproj
```

If errors mention `Microsoft.PowerToys.Telemetry`: open file, delete `using` and remove telemetry calls.
If errors mention `PowerToys.GPOWrapper`: delete usings + GPO checks.

- [ ] **Step 3: Add to solution**

```bash
dotnet sln PowerToysRun.sln add src/Wox.Infrastructure/Wox.Infrastructure.csproj
```

- [ ] **Step 4: Commit**

```bash
git add src/Wox.Infrastructure/
git commit -m "fix: rewire Wox.Infrastructure; strip PT-specific imports"
```

### Task 2.3: Wire up `PowerLauncher` (largest task — expect compile errors)

**Files:**
- Modify: `<PTRUN>/src/PowerLauncher/PowerLauncher.csproj`
- Modify: many `*.cs` files in `<PTRUN>/src/PowerLauncher/`

- [ ] **Step 1: Open `src/PowerLauncher/PowerLauncher.csproj` and replace ItemGroup**

Replace:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\common\GPOWrapperProjection\GPOWrapperProjection.csproj" />
  <ProjectReference Include="..\..\..\common\interop\PowerToys.Interop.vcxproj" />
  <ProjectReference Include="..\..\..\common\ManagedCommon\ManagedCommon.csproj" />
  <ProjectReference Include="..\..\..\common\Common.UI\Common.UI.csproj" />
  <ProjectReference Include="..\..\..\settings-ui\Settings.UI.Library\Settings.UI.Library.csproj" />
  <ProjectReference Include="..\PowerLauncher.Telemetry\PowerLauncher.Telemetry.csproj" />
  <ProjectReference Include="..\Wox.Infrastructure\Wox.Infrastructure.csproj" />
  <ProjectReference Include="..\Wox.Plugin\Wox.Plugin.csproj" />
</ItemGroup>
```

With:

```xml
<ItemGroup>
  <ProjectReference Include="..\Common\ManagedCommon\ManagedCommon.csproj" />
  <ProjectReference Include="..\Common\Common.UI\Common.UI.csproj" />
  <ProjectReference Include="..\Common\Settings.Library\Settings.Library.csproj" />
  <ProjectReference Include="..\Wox.Infrastructure\Wox.Infrastructure.csproj" />
  <ProjectReference Include="..\Wox.Plugin\Wox.Plugin.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Remove Imports referencing `<RepoRoot>` paths from PT**

In csproj header, remove any `<Import Project="$(RepoRoot)src\Common.SelfContained.props" />` etc. The new `Directory.Build.props` already covers this.

- [ ] **Step 3: Try to build to enumerate errors**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj 2>&1 | tee build-errors.log
```

Expected: many errors. Triage by category (see Phase 3 tasks).

- [ ] **Step 4: Add to solution**

```bash
dotnet sln PowerToysRun.sln add src/PowerLauncher/PowerLauncher.csproj
```

- [ ] **Step 5: Commit (intentionally broken state)**

```bash
git add src/PowerLauncher/PowerLauncher.csproj
git commit -m "wip: rewire PowerLauncher.csproj (expect compile errors next)"
```

### Task 2.4: Wire up all 19 plugins

**Files:**
- Modify: each `<PTRUN>/src/Plugins/*/<PluginName>.csproj`

- [ ] **Step 1: For each plugin csproj, rewrite paths**

In every `src/Plugins/*/<PluginName>.csproj`:
- Change `..\..\..\..\settings-ui\Settings.UI.Library\Settings.UI.Library.csproj` → `..\..\Common\Settings.Library\Settings.Library.csproj`
- Change `..\..\..\..\common\GPOWrapper\GPOWrapper.vcxproj` → REMOVE (no GPO)
- Keep `..\..\Wox.Plugin\Wox.Plugin.csproj`, `..\..\Wox.Infrastructure\Wox.Infrastructure.csproj`

- [ ] **Step 2: Add each plugin csproj to solution**

```bash
for f in src/Plugins/*/*.csproj; do dotnet sln PowerToysRun.sln add "$f"; done
```

- [ ] **Step 3: Build all plugins**

```bash
dotnet build PowerToysRun.sln
```

Expected: failures (will be fixed in Phase 3).

- [ ] **Step 4: Commit**

```bash
git add src/Plugins/
git commit -m "fix: rewire plugin ProjectReferences to vendored common"
```

### Task 2.5: Wire up `Wox.Test` and plugin unit tests

**Files:**
- Modify: `<PTRUN>/src/Wox.Test/Wox.Test.csproj`
- Modify: each `<PTRUN>/src/Plugins/*UnitTests/*.csproj`

- [ ] **Step 1: Rewrite ProjectReferences in Wox.Test.csproj**

Same pattern as PowerLauncher: drop PT common refs, point at `..\Common\` paths.

- [ ] **Step 2: Add to solution + try to build**

```bash
dotnet sln PowerToysRun.sln add src/Wox.Test/Wox.Test.csproj
for f in src/Plugins/*UnitTests/*.csproj; do dotnet sln PowerToysRun.sln add "$f"; done
dotnet build PowerToysRun.sln
```

- [ ] **Step 3: Commit**

```bash
git add src/Wox.Test src/Plugins/*UnitTests/
git commit -m "fix: rewire test project references"
```

---

## Phase 3: Strip PT-Specific Code Paths

### Task 3.1: Remove all GPO checks

**Files:**
- Modify: `<PTRUN>/src/PowerLauncher/App.xaml.cs`
- Modify: `<PTRUN>/src/PowerLauncher/SettingsReader.cs`
- Modify: `<PTRUN>/src/PowerLauncher/Plugin/PluginManager.cs`

- [ ] **Step 1: In `App.xaml.cs`, delete the GPO startup check block**

Find and delete:

```csharp
if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredPowerLauncherEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
{
    Log.Warn("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.", typeof(App));
    return;
}
```

Also remove `using global::PowerToys.GPOWrapperProjection;` if present.

- [ ] **Step 2: In `Plugin/PluginManager.cs`, remove GPO per-plugin checks**

Find blocks like:

```csharp
var enabledPolicyState = GPOWrapper.GetRunPluginEnabledValue(pair.Metadata.ID);
if (enabledPolicyState == GpoRuleConfigured.Enabled) { ... }
else if (enabledPolicyState == GpoRuleConfigured.Disabled) { ... }
```

Remove them entirely (no policy enforcement). Keep only the user-setting-driven enable/disable logic.

Remove `using global::PowerToys.GPOWrapper;`.

- [ ] **Step 3: In `SettingsReader.cs`, remove GPO state population**

Find:

```csharp
EnabledPolicyUiState = (int)GpoRuleConfigured.NotConfigured,
...
var enabledPolicyState = GPOWrapper.GetRunPluginEnabledValue(id);
```

Replace with default (NotConfigured / no override). Remove `using global::PowerToys.GPOWrapper;`.

- [ ] **Step 4: Build**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
```

Expected: GPO-related compile errors gone. Other errors remain.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: remove all GPO policy checks (no policy in standalone)"
```

### Task 3.2: Remove `PowerLauncher.Telemetry` references

**Files:**
- Delete: `<PTRUN>/src/PowerLauncher/PowerLauncher.Telemetry/` (entire directory if it was copied)
- Modify: many files in `<PTRUN>/src/PowerLauncher/` and `<PTRUN>/src/Plugins/`

- [ ] **Step 1: Verify the Telemetry project was excluded**

```bash
ls src/ | grep -i telemetry
```

Expected: nothing (we excluded it via filter-repo).

- [ ] **Step 2: Find all telemetry references in copied code**

```bash
grep -rn "Microsoft.PowerToys.Telemetry\|PowerLauncher.Telemetry\|TelemetryEvent\|ETWTrace" src/ --include="*.cs"
```

- [ ] **Step 3: For each match, delete the using + the call site**

Common patterns to delete:
- `using Microsoft.PowerToys.Telemetry;`
- `using Microsoft.PowerLauncher.Telemetry;`
- `private ETWTrace etwTrace = new ETWTrace();` and its `Dispose()`
- `PowerToysTelemetry.Log.WriteEvent(new XxxEvent(...));`

Use sed or batch edit:

```bash
grep -l "PowerToysTelemetry.Log.WriteEvent" src/ -r --include="*.cs" | xargs -I {} sed -i '/PowerToysTelemetry\.Log\.WriteEvent/d' {}
```

(verify result manually, some may need block deletion not single-line)

- [ ] **Step 4: Build**

```bash
dotnet build PowerToysRun.sln
```

Expected: telemetry errors gone.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: remove telemetry references throughout"
```

### Task 3.3: Remove `PowerToys.Interop` references

**Files:**
- Modify: `<PTRUN>/src/PowerLauncher/App.xaml.cs`
- Modify: `<PTRUN>/src/PowerLauncher/MainWindow.xaml.cs`
- Modify: `<PTRUN>/src/PowerLauncher/ViewModel/MainViewModel.cs`

- [ ] **Step 1: Find all PowerToys.Interop usages**

```bash
grep -rn "PowerToys.Interop\|Constants.PowerLauncherSharedEvent\|Constants.RunExitEvent\|Constants.PowerLauncherCentralizedHookSharedEvent\|GetPowerToysPId\|RunnerHelper.WaitForPowerToysRunner" src/ --include="*.cs"
```

- [ ] **Step 2: In `App.xaml.cs::Main()`, delete the runner-coordination block**

Delete:

```csharp
int powerToysPid = GetPowerToysPId();
if (powerToysPid != 0)
{
    Log.Info($"Runner pid={powerToysPid}", typeof(App));
    SingleInstance<App>.CreateInstanceMutex();
}
else
{
    if (!SingleInstance<App>.InitializeAsFirstInstance())
    {
        Log.Warn("There is already running PowerToys Run instance. Exiting PowerToys Run", typeof(App));
        return;
    }
}
```

Replace with:

```csharp
if (!SingleInstance<App>.InitializeAsFirstInstance())
{
    Log.Warn("There is already running PowerToys Run instance. Exiting PowerToys Run", typeof(App));
    return;
}
```

- [ ] **Step 3: Delete the RunExitEvent + RunnerHelper waiter block**

Delete:

```csharp
Common.UI.NativeEventWaiter.WaitForEventLoop(
    Constants.RunExitEvent(),
    () => { ... ExitPowerToys(application); },
    Application.Current.Dispatcher,
    NativeThreadCTS.Token);

if (powerToysPid != 0)
{
    RunnerHelper.WaitForPowerToysRunner(powerToysPid, () => { ... ExitPowerToys(application); });
}
```

- [ ] **Step 4: Delete `GetPowerToysPId()` method definition entirely**

- [ ] **Step 5: In `MainViewModel.cs`, replace `Constants.PowerLauncherSharedEvent()` waiter**

The hotkey path that listens to a runner-set event will be replaced by Win32 `RegisterHotKey` in Phase 4. For now, delete the waiter setup:

Delete:

```csharp
NativeEventWaiter.WaitForEventLoop(Constants.PowerLauncherSharedEvent(), OnHotkey, ..., _nativeWaiterCancelToken);
NativeEventWaiter.WaitForEventLoop(Constants.PowerLauncherCentralizedHookSharedEvent(), OnCentralizedKeyboardHookHotKey, ..., _nativeWaiterCancelToken);
```

Leave `OnHotkey` method intact — it will be called from new `HotkeyService` (Phase 4).

- [ ] **Step 6: Remove `using PowerToys.Interop;` from all touched files**

- [ ] **Step 7: Build**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
```

Expected: PowerToys.Interop errors gone. May still have errors related to deleted `Centralized hook` references.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: remove PowerToys.Interop and runner coordination"
```

### Task 3.4: Final build green check

**Files:**
- (no specific files, this is a sanity check)

- [ ] **Step 1: Build whole solution**

```bash
dotnet build PowerToysRun.sln 2>&1 | tee build.log
```

If errors remain, triage and fix. Common leftovers:
- References to `LauncherConstants.h` (C++ side, ignore)
- `OnCentralizedKeyboardHookHotKey` method definition with no callers — delete the method body but keep file compilable
- Other `RunnerHelper.X` calls — replace with no-op or delete

- [ ] **Step 2: Run tests once they compile**

```bash
dotnet test PowerToysRun.sln
```

Expected: a subset may fail (those requiring runner interaction). Note failures for fixing later in Phase 13.

- [ ] **Step 3: Commit any remaining cleanup**

```bash
git add -A
git commit -m "fix: final cleanup of PT-coupled code paths"
```

---

## Phase 4: Standalone Process Services

### Task 4.1: Add `HotkeyService` (Win32 RegisterHotKey)

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Services/HotkeyService.cs`
- Modify: `<PTRUN>/src/PowerLauncher/MainWindow.xaml.cs`
- Modify: `<PTRUN>/src/PowerLauncher/ViewModel/MainViewModel.cs`

- [ ] **Step 1: Write the failing test**

Create `src/Wox.Test/Services/HotkeyServiceTests.cs`:

```csharp
using NUnit.Framework;
using PowerLauncher.Services;
using Microsoft.PowerToys.Settings.UI.Library;

[TestFixture]
public class HotkeyServiceTests
{
    [Test]
    public void Register_NullHotkey_ReturnsFalse()
    {
        using var svc = new HotkeyService();
        var result = svc.Register(null, () => { });
        Assert.That(result, Is.False);
    }

    [Test]
    public void Register_NoModifiers_ReturnsFalse()
    {
        using var svc = new HotkeyService();
        var hk = new HotkeySettings { Code = (int)System.Windows.Forms.Keys.Space };
        var result = svc.Register(hk, () => { });
        Assert.That(result, Is.False);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test src/Wox.Test/Wox.Test.csproj --filter "HotkeyServiceTests"
```

Expected: FAIL — type `HotkeyService` does not exist.

- [ ] **Step 3: Implement `HotkeyService.cs`**

```csharp
// src/PowerLauncher/Services/HotkeyService.cs
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerLauncher.Services
{
    /// <summary>
    /// Registers a global hotkey via Win32 RegisterHotKey + WM_HOTKEY message pump.
    /// One instance owns one registration at a time. Re-registration replaces.
    /// </summary>
    public sealed class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0xB001;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource _source;
        private Action _onPressed;
        private bool _isRegistered;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyService() { }

        public void AttachWindow(System.Windows.Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(WndProc);
        }

        public bool Register(HotkeySettings hk, Action onPressed)
        {
            if (hk == null) return false;
            uint mods = 0;
            if (hk.Alt) mods |= MOD_ALT;
            if (hk.Ctrl) mods |= MOD_CONTROL;
            if (hk.Shift) mods |= MOD_SHIFT;
            if (hk.Win) mods |= MOD_WIN;

            if (mods == 0) return false;
            if (hk.Code == 0) return false;

            if (_isRegistered) UnregisterHotKey(_hwnd, HOTKEY_ID);

            _onPressed = onPressed;
            _isRegistered = RegisterHotKey(_hwnd, HOTKEY_ID, mods, (uint)hk.Code);
            return _isRegistered;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _onPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isRegistered) UnregisterHotKey(_hwnd, HOTKEY_ID);
            _source?.RemoveHook(WndProc);
            _isRegistered = false;
        }
    }
}
```

- [ ] **Step 4: Run test**

```bash
dotnet test src/Wox.Test/Wox.Test.csproj --filter "HotkeyServiceTests"
```

Expected: PASS.

- [ ] **Step 5: Wire into MainWindow + MainViewModel**

In `MainWindow.xaml.cs` constructor, after `InitializeComponent()`:

```csharp
_hotkeyService = new HotkeyService();
_hotkeyService.AttachWindow(this);
// Register based on current settings:
var settings = SettingsReader.LoadSettings();
_hotkeyService.Register(settings.Properties.OpenPowerLauncher, () => _viewModel.OnHotkey());
```

Where `OnHotkey()` is the existing method previously called from `NativeEventWaiter`.

In `App.xaml.cs::OnExit`, dispose: `_hotkeyService?.Dispose()`.

- [ ] **Step 6: Add a settings change listener that re-registers hotkey on change**

When `PowerLauncherSettings.Properties.OpenPowerLauncher` changes (via FileWatcher reload), call `_hotkeyService.Register(newHotkey, ...)` again.

- [ ] **Step 7: On registration failure, surface to UI**

In `MainWindow.xaml.cs`, if `_hotkeyService.Register(...)` returns false:

```csharp
ShowHotkeyConflictNotification("The configured hotkey is already in use. Please change it in Settings.");
```

(Implement `ShowHotkeyConflictNotification` as a system tray balloon or InfoBar in main window — use existing `MessageBox` for now if simpler; refine later.)

- [ ] **Step 8: Build + test + manual smoke test**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
dotnet run --project src/PowerLauncher/PowerLauncher.csproj
```

Press Alt+Space — verify search window appears.

- [ ] **Step 9: Commit**

```bash
git add src/PowerLauncher/Services/HotkeyService.cs src/PowerLauncher/MainWindow.xaml.cs src/PowerLauncher/ViewModel/MainViewModel.cs src/PowerLauncher/App.xaml.cs src/Wox.Test/Services/HotkeyServiceTests.cs
git commit -m "feat: add HotkeyService using Win32 RegisterHotKey"
```

### Task 4.2: Add `TrayIconService` (P/Invoke Shell_NotifyIconW)

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Services/TrayIconService.cs`
- Create: `<PTRUN>/src/PowerLauncher/Services/NativeTrayMethods.cs`
- Modify: `<PTRUN>/src/PowerLauncher/App.xaml.cs`

- [ ] **Step 1: Create `NativeTrayMethods.cs`** with P/Invoke definitions

```csharp
// src/PowerLauncher/Services/NativeTrayMethods.cs
using System;
using System.Runtime.InteropServices;

namespace PowerLauncher.Services
{
    internal static class NativeTrayMethods
    {
        public const int WM_USER = 0x0400;
        public const int WM_TRAYICON = WM_USER + 1024;
        public const int NIM_ADD = 0x00000000;
        public const int NIM_MODIFY = 0x00000001;
        public const int NIM_DELETE = 0x00000002;

        public const uint NIF_MESSAGE = 0x00000001;
        public const uint NIF_ICON = 0x00000002;
        public const uint NIF_TIP = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA pnid);
    }
}
```

- [ ] **Step 2: Create `TrayIconService.cs`** with a hidden window message pump

```csharp
// src/PowerLauncher/Services/TrayIconService.cs
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using static PowerLauncher.Services.NativeTrayMethods;

namespace PowerLauncher.Services
{
    /// <summary>Owns the system tray icon and right-click menu.</summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly Window _hiddenWindow;
        private readonly HwndSource _hwndSource;
        private NOTIFYICONDATA _data;
        private readonly Action _onLeftClick;
        private readonly Action _onShowSettings;
        private readonly Action _onRestart;
        private readonly Action _onExit;

        public TrayIconService(Action onLeftClick, Action onShowSettings, Action onRestart, Action onExit)
        {
            _onLeftClick = onLeftClick;
            _onShowSettings = onShowSettings;
            _onRestart = onRestart;
            _onExit = onExit;

            _hiddenWindow = new Window { Width = 0, Height = 0, ShowInTaskbar = false, Visibility = Visibility.Hidden, WindowStyle = WindowStyle.None };
            _hiddenWindow.Show();
            _hiddenWindow.Hide();

            var helper = new WindowInteropHelper(_hiddenWindow);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource.AddHook(WndProc);

            using var icon = new Icon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets/PowerLauncher/RunResource.ico"));
            _data = new NOTIFYICONDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = helper.Handle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = icon.Handle,
                szTip = "PowerToys Run"
            };
            Shell_NotifyIconW(NIM_ADD, ref _data);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int eventCode = lParam.ToInt32() & 0xFFFF;
                if (eventCode == 0x0202 /* WM_LBUTTONUP */) _onLeftClick?.Invoke();
                else if (eventCode == 0x0205 /* WM_RBUTTONUP */) ShowMenu();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ShowMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MakeItem("Settings", _onShowSettings));
            menu.Items.Add(MakeItem("Restart", _onRestart));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Exit", _onExit));
            menu.IsOpen = true;
        }

        private static MenuItem MakeItem(string header, Action onClick)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, __) => onClick?.Invoke();
            return mi;
        }

        public void Dispose()
        {
            Shell_NotifyIconW(NIM_DELETE, ref _data);
            _hwndSource?.RemoveHook(WndProc);
            _hiddenWindow?.Close();
        }
    }
}
```

- [ ] **Step 3: Wire into `App.xaml.cs`**

In `OnStartup`:

```csharp
_trayIcon = new TrayIconService(
    onLeftClick: () => _mainWindow.ShowSearch(),
    onShowSettings: () => SettingsWindow.ShowOrFocus(),
    onRestart: () => RestartApplication(),
    onExit: () => Application.Current.Shutdown());
```

In `OnExit`:

```csharp
_trayIcon?.Dispose();
```

- [ ] **Step 4: Build + manual smoke test**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
dotnet run --project src/PowerLauncher/PowerLauncher.csproj
```

Verify: tray icon appears; left-click toggles search; right-click shows menu.

- [ ] **Step 5: Commit**

```bash
git add src/PowerLauncher/Services/TrayIconService.cs src/PowerLauncher/Services/NativeTrayMethods.cs src/PowerLauncher/App.xaml.cs
git commit -m "feat: add TrayIconService via P/Invoke Shell_NotifyIconW"
```

### Task 4.3: Add `AutostartService` (HKCU\Run)

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Services/AutostartService.cs`
- Create: `<PTRUN>/src/Wox.Test/Services/AutostartServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// src/Wox.Test/Services/AutostartServiceTests.cs
using NUnit.Framework;
using PowerLauncher.Services;

[TestFixture]
public class AutostartServiceTests
{
    [Test]
    public void IsEnabled_AfterEnable_ReturnsTrue()
    {
        var svc = new AutostartService("PowerToysRun-TEST", "C:\\fake\\path.exe");
        try
        {
            svc.Enable();
            Assert.That(svc.IsEnabled(), Is.True);
        }
        finally { svc.Disable(); }
    }

    [Test]
    public void IsEnabled_AfterDisable_ReturnsFalse()
    {
        var svc = new AutostartService("PowerToysRun-TEST", "C:\\fake\\path.exe");
        svc.Enable();
        svc.Disable();
        Assert.That(svc.IsEnabled(), Is.False);
    }
}
```

- [ ] **Step 2: Run, expect FAIL (type missing)**

```bash
dotnet test --filter "AutostartServiceTests"
```

- [ ] **Step 3: Implement**

```csharp
// src/PowerLauncher/Services/AutostartService.cs
using Microsoft.Win32;

namespace PowerLauncher.Services
{
    public sealed class AutostartService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private readonly string _name;
        private readonly string _exePath;

        public AutostartService(string name, string exePath)
        {
            _name = name;
            _exePath = exePath;
        }

        public bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(_name) as string == _exePath;
        }

        public void Enable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(_name, _exePath, RegistryValueKind.String);
        }

        public void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(_name, false);
        }
    }
}
```

- [ ] **Step 4: Run tests, expect PASS**

```bash
dotnet test --filter "AutostartServiceTests"
```

- [ ] **Step 5: Wire into App.xaml.cs**

On first run, if user setting `Autostart=true`, call `_autostart.Enable()`. Settings UI exposes the toggle.

- [ ] **Step 6: Commit**

```bash
git add src/PowerLauncher/Services/AutostartService.cs src/Wox.Test/Services/AutostartServiceTests.cs src/PowerLauncher/App.xaml.cs
git commit -m "feat: add AutostartService (HKCU Run)"
```

### Task 4.4: Update single-instance mutex name to differentiate from PT integrated version

**Files:**
- Modify: `<PTRUN>/src/PowerLauncher/SingleInstance.cs` (or wherever `SingleInstance<App>` is defined)

- [ ] **Step 1: Find the mutex name**

```bash
grep -rn "PowerToys-Run\|Local\\\\PowerToys" src/PowerLauncher/
```

- [ ] **Step 2: Replace with new name**

E.g., change `Local\PowerToys-Run-XXXX` to `Local\PowerToys-Run-Standalone-XXXX`.

- [ ] **Step 3: Add detection of legacy PT-integrated PowerLauncher.exe still running**

In `App.xaml.cs::Main()`, after acquiring single-instance:

```csharp
var legacyProcesses = System.Diagnostics.Process.GetProcessesByName("PowerToys.PowerLauncher")
    .Where(p => p.MainModule?.FileName != Process.GetCurrentProcess().MainModule?.FileName)
    .ToArray();
if (legacyProcesses.Length > 0)
{
    System.Windows.MessageBox.Show(
        "PowerToys is currently running with the integrated Run module. " +
        "Please disable PowerToys Run in PowerToys Settings before using the standalone version.",
        "PowerToys Run",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    return;
}
```

- [ ] **Step 4: Build + smoke test**

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: rename single-instance mutex; warn on PT integrated coexistence"
```

---

## Phase 5: Custom WPF Controls (3 small ones)

### Task 5.1: `InfoBar` UserControl

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Controls/InfoBar.xaml`
- Create: `<PTRUN>/src/PowerLauncher/Controls/InfoBar.xaml.cs`
- Create: `<PTRUN>/src/Wox.Test/Controls/InfoBarTests.cs`

- [ ] **Step 1: Write a simple smoke test** (UI tests are limited in WPF; focus on dependency-property behavior)

```csharp
// src/Wox.Test/Controls/InfoBarTests.cs
using NUnit.Framework;
using PowerLauncher.Controls;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class InfoBarTests
{
    [Test]
    public void Severity_DefaultsToInformational()
    {
        var bar = new InfoBar();
        Assert.That(bar.Severity, Is.EqualTo(InfoBarSeverity.Informational));
    }

    [Test]
    public void Title_SetGet()
    {
        var bar = new InfoBar { Title = "Hello" };
        Assert.That(bar.Title, Is.EqualTo("Hello"));
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implement `InfoBar.xaml`**

```xml
<UserControl x:Class="PowerLauncher.Controls.InfoBar"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Border x:Name="RootBorder" CornerRadius="4" BorderThickness="1" Padding="12,8" Background="#F0F4FF" BorderBrush="#5B9DF9">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <TextBlock x:Name="IconBlock" Grid.Column="0" Text="&#xE946;" FontFamily="Segoe MDL2 Assets" FontSize="16" Foreground="#0078D4" VerticalAlignment="Center" Margin="0,0,8,0" />
      <StackPanel Grid.Column="1" VerticalAlignment="Center">
        <TextBlock x:Name="TitleBlock" FontWeight="SemiBold" />
        <TextBlock x:Name="MessageBlock" TextWrapping="Wrap" />
      </StackPanel>
      <Button x:Name="ActionButton" Grid.Column="2" Margin="8,0" Visibility="Collapsed" Click="OnActionClicked" />
      <Button x:Name="CloseButton" Grid.Column="3" Width="24" Height="24" Padding="0" Click="OnCloseClicked" Background="Transparent" BorderThickness="0" Visibility="Collapsed">
        <TextBlock Text="&#xE894;" FontFamily="Segoe MDL2 Assets" FontSize="12" />
      </Button>
    </Grid>
  </Border>
</UserControl>
```

- [ ] **Step 4: Implement `InfoBar.xaml.cs`**

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PowerLauncher.Controls
{
    public enum InfoBarSeverity { Informational, Success, Warning, Error }

    public partial class InfoBar : UserControl
    {
        public InfoBar() { InitializeComponent(); ApplySeverity(); }

        public static readonly DependencyProperty SeverityProperty =
            DependencyProperty.Register(nameof(Severity), typeof(InfoBarSeverity), typeof(InfoBar),
                new PropertyMetadata(InfoBarSeverity.Informational, OnSeverityChanged));

        public InfoBarSeverity Severity
        {
            get => (InfoBarSeverity)GetValue(SeverityProperty);
            set => SetValue(SeverityProperty, value);
        }

        private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((InfoBar)d).ApplySeverity();

        private void ApplySeverity()
        {
            (RootBorder.Background, RootBorder.BorderBrush, IconBlock.Foreground, IconBlock.Text) = Severity switch
            {
                InfoBarSeverity.Success => (new SolidColorBrush(Color.FromRgb(0xDF, 0xF6, 0xDD)), new SolidColorBrush(Color.FromRgb(0x10, 0x80, 0x10)), new SolidColorBrush(Color.FromRgb(0x10, 0x80, 0x10)), ""),
                InfoBarSeverity.Warning => (new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xCE)), new SolidColorBrush(Color.FromRgb(0xCA, 0x90, 0x10)), new SolidColorBrush(Color.FromRgb(0xCA, 0x90, 0x10)), ""),
                InfoBarSeverity.Error => (new SolidColorBrush(Color.FromRgb(0xFD, 0xE7, 0xE9)), new SolidColorBrush(Color.FromRgb(0xC4, 0x29, 0x21)), new SolidColorBrush(Color.FromRgb(0xC4, 0x29, 0x21)), ""),
                _ => (new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xFF)), new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF9)), new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), "")
            };
        }

        public string Title
        {
            get => TitleBlock.Text;
            set => TitleBlock.Text = value;
        }

        public string Message
        {
            get => MessageBlock.Text;
            set => MessageBlock.Text = value;
        }

        public string ActionText
        {
            get => ActionButton.Content as string;
            set
            {
                ActionButton.Content = value;
                ActionButton.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool IsClosable
        {
            get => CloseButton.Visibility == Visibility.Visible;
            set => CloseButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public event EventHandler ActionClicked;
        public event EventHandler Closed;

        private void OnActionClicked(object sender, RoutedEventArgs e) => ActionClicked?.Invoke(this, EventArgs.Empty);
        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 4: Run tests, expect PASS**

```bash
dotnet test --filter "InfoBarTests"
```

- [ ] **Step 5: Commit**

```bash
git add src/PowerLauncher/Controls/InfoBar.xaml src/PowerLauncher/Controls/InfoBar.xaml.cs src/Wox.Test/Controls/InfoBarTests.cs
git commit -m "feat: add InfoBar WPF UserControl"
```

### Task 5.2: `NumberBox` and `NumberValidationRule`

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Controls/NumberBox.cs`
- Create: `<PTRUN>/src/PowerLauncher/Helpers/NumberValidationRule.cs`
- Create: `<PTRUN>/src/Wox.Test/Helpers/NumberValidationRuleTests.cs`

- [ ] **Step 1: Write failing tests for `NumberValidationRule`**

```csharp
// src/Wox.Test/Helpers/NumberValidationRuleTests.cs
using NUnit.Framework;
using PowerLauncher.Helpers;
using System.Globalization;

[TestFixture]
public class NumberValidationRuleTests
{
    [Test]
    public void Validate_NumericString_ReturnsValid()
    {
        var rule = new NumberValidationRule { Min = 0, Max = 1000 };
        var result = rule.Validate("42", CultureInfo.InvariantCulture);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_OutOfRange_ReturnsInvalid()
    {
        var rule = new NumberValidationRule { Min = 0, Max = 100 };
        var result = rule.Validate("500", CultureInfo.InvariantCulture);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_NonNumeric_ReturnsInvalid()
    {
        var rule = new NumberValidationRule { Min = 0, Max = 100 };
        var result = rule.Validate("abc", CultureInfo.InvariantCulture);
        Assert.That(result.IsValid, Is.False);
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implement**

```csharp
// src/PowerLauncher/Helpers/NumberValidationRule.cs
using System.Globalization;
using System.Windows.Controls;

namespace PowerLauncher.Helpers
{
    public class NumberValidationRule : ValidationRule
    {
        public double Min { get; set; } = double.MinValue;
        public double Max { get; set; } = double.MaxValue;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is not string s) return new ValidationResult(false, "Value must be a string");
            if (!double.TryParse(s, NumberStyles.Number, cultureInfo, out var n))
                return new ValidationResult(false, "Not a number");
            if (n < Min || n > Max) return new ValidationResult(false, $"Must be between {Min} and {Max}");
            return ValidationResult.ValidResult;
        }
    }
}
```

- [ ] **Step 4: Implement `NumberBox.cs`** (a TextBox subclass with explicit Min/Max + arrow-key increment)

```csharp
// src/PowerLauncher/Controls/NumberBox.cs
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PowerLauncher.Controls
{
    public class NumberBox : TextBox
    {
        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(NumberBox), new PropertyMetadata(double.MinValue));
        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(NumberBox), new PropertyMetadata(double.MaxValue));
        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(NumberBox), new PropertyMetadata(1.0));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumberBox),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public double MinValue { get => (double)GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
        public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
        public double SmallChange { get => (double)GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumberBox nb)
                nb.Text = ((double)e.NewValue).ToString(CultureInfo.CurrentCulture);
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            if (double.TryParse(Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var n))
            {
                if (n < MinValue) n = MinValue;
                if (n > MaxValue) n = MaxValue;
                if (Value != n) Value = n;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Up) { Value = System.Math.Min(MaxValue, Value + SmallChange); e.Handled = true; }
            else if (e.Key == Key.Down) { Value = System.Math.Max(MinValue, Value - SmallChange); e.Handled = true; }
            else base.OnKeyDown(e);
        }
    }
}
```

- [ ] **Step 5: Run tests, expect PASS**

```bash
dotnet test --filter "NumberValidationRuleTests"
```

- [ ] **Step 6: Commit**

```bash
git add src/PowerLauncher/Controls/NumberBox.cs src/PowerLauncher/Helpers/NumberValidationRule.cs src/Wox.Test/Helpers/NumberValidationRuleTests.cs
git commit -m "feat: add NumberBox + NumberValidationRule"
```

### Task 5.3: Adapt the existing `ShortcutControl` (search/reuse)

**Files:**
- Locate: existing PowerLauncher hotkey/shortcut UI
- Modify: as needed

- [ ] **Step 1: Find existing hotkey UI in PowerLauncher**

```bash
grep -rn "HotkeySettings\|Hotkey.*Click\|RecordHotkey" src/PowerLauncher/ --include="*.xaml.cs" --include="*.xaml"
```

If `PowerLauncher` already has UI for capturing hotkeys (it does, used in main search window for plugin keyword binding), promote it to a reusable `ShortcutControl` UserControl.

- [ ] **Step 2: Extract into `src/PowerLauncher/Controls/ShortcutControl.xaml(.cs)`**

It should expose `HotkeySettings` as a TwoWay DP, capture key down events, build the modifiers + key, write back to the binding.

(Skip showing full code; it's a port of existing PowerLauncher code. Engineer should preserve existing behavior.)

- [ ] **Step 3: Verify with manual test**

Open Settings UI later; click hotkey field; press Alt+Space; verify it captures.

- [ ] **Step 4: Commit**

```bash
git add src/PowerLauncher/Controls/ShortcutControl.xaml src/PowerLauncher/Controls/ShortcutControl.xaml.cs
git commit -m "feat: extract reusable ShortcutControl from existing PowerLauncher"
```

---

## Phase 6: Settings Window + Page

### Task 6.1: Create `SettingsWindow.xaml`

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Views/Settings/SettingsWindow.xaml`
- Create: `<PTRUN>/src/PowerLauncher/Views/Settings/SettingsWindow.xaml.cs`

- [ ] **Step 1: Implement minimal `SettingsWindow.xaml`**

```xml
<Window x:Class="PowerLauncher.Views.Settings.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:PowerLauncher.Views.Settings"
        Title="PowerToys Run Settings"
        Width="900" Height="700"
        ResizeMode="CanResize"
        WindowStartupLocation="CenterScreen">
  <Grid>
    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
      <views:PowerLauncherSettingsPage />
    </ScrollViewer>
  </Grid>
</Window>
```

- [ ] **Step 2: Implement code-behind with single-instance ShowOrFocus pattern**

```csharp
// src/PowerLauncher/Views/Settings/SettingsWindow.xaml.cs
using System.Windows;

namespace PowerLauncher.Views.Settings
{
    public partial class SettingsWindow : Window
    {
        private static SettingsWindow _instance;

        public SettingsWindow() => InitializeComponent();

        public static void ShowOrFocus()
        {
            if (_instance == null)
            {
                _instance = new SettingsWindow();
                _instance.Closed += (_, __) => _instance = null;
                _instance.Show();
            }
            else
            {
                if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
                _instance.Activate();
            }
        }
    }
}
```

- [ ] **Step 3: Wire to tray menu (already done in Task 4.2 via `onShowSettings: () => SettingsWindow.ShowOrFocus()`)**

- [ ] **Step 4: Build + smoke test**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
dotnet run --project src/PowerLauncher/PowerLauncher.csproj
```

Right-click tray icon → Settings. Verify window opens.

- [ ] **Step 5: Commit**

```bash
git add src/PowerLauncher/Views/Settings/SettingsWindow.xaml src/PowerLauncher/Views/Settings/SettingsWindow.xaml.cs
git commit -m "feat: add SettingsWindow shell"
```

### Task 6.2: Adapt `PowerLauncherViewModel` for direct-write IPC and remove GPO

**Files:**
- Copy from PT: `<PT>/src/settings-ui/Settings.UI/ViewModels/PowerLauncherViewModel.cs` → `<PTRUN>/src/PowerLauncher/Views/Settings/PowerLauncherViewModel.cs`
- Copy from PT: `<PT>/src/settings-ui/Settings.UI/ViewModels/PowerLauncherPluginViewModel.cs` → `<PTRUN>/src/PowerLauncher/Views/Settings/PowerLauncherPluginViewModel.cs`

- [ ] **Step 1: Copy both files**

```bash
cp <PT>/src/settings-ui/Settings.UI/ViewModels/PowerLauncherViewModel.cs src/PowerLauncher/Views/Settings/
cp <PT>/src/settings-ui/Settings.UI/ViewModels/PowerLauncherPluginViewModel.cs src/PowerLauncher/Views/Settings/
```

- [ ] **Step 2: Change namespace to match new location**

In both files, replace `namespace Microsoft.PowerToys.Settings.UI.ViewModels` with `namespace PowerLauncher.Views.Settings`.

- [ ] **Step 3: Remove GPO usings and code paths**

Delete:
- `using global::PowerToys.GPOWrapper;`
- All `IsEnabledGpoConfigured`, `EnabledGpoRuleConfiguration`, `IsGpoConfigured` properties and their backing fields
- All `if (gpoState == GpoRuleConfigured.X) ...` branches

Keep equivalent user-only logic (e.g., `EnablePowerLauncher` is still bound to user setting, just no policy override).

- [ ] **Step 4: Replace IPC `SendDefaultIPCMessage` callback with direct write**

The ViewModel's constructor takes a `Func<string, int>` IPC delegate. Replace with:

```csharp
private readonly SettingsRepository<PowerLauncherSettings> _settingsRepo;

public PowerLauncherViewModel(/* old args */)
{
    // ... existing init logic ...
}

private void SaveSettings()
{
    _settingsRepo.SettingsConfig = _settings;
    SettingsUtils.Default.SaveSettings(_settings.ToJsonString(), PowerLauncherSettings.ModuleName);
    // Ask the running PowerLauncher.exe to reload immediately (same process):
    PowerLauncher.SettingsReader.Reload();
}
```

Change every `SendDefaultIPCMessageTimed(...)` call to `SaveSettings()`.

- [ ] **Step 5: Build, fix surface errors**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
```

Likely errors:
- `using Microsoft.PowerToys.Settings.UI.Helpers` — provide a small shim or remove the reference and replace `App.IsDarkTheme` with a local equivalent (see Step 6)
- `using Microsoft.PowerToys.Settings.UI.Library.Interfaces` — ensure `Settings.Library` exports these

- [ ] **Step 6: Fix `App.IsDarkTheme` reference**

Replace with a local theme accessor:

```csharp
private static bool IsDarkTheme() => PowerLauncher.Helper.ThemeManager.Current.IsDark;
```

- [ ] **Step 7: Build green**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
```

Expected: green.

- [ ] **Step 8: Commit**

```bash
git add src/PowerLauncher/Views/Settings/
git commit -m "feat: adapt PowerLauncherViewModel + PluginViewModel (remove GPO, direct write)"
```

### Task 6.3: Rewrite `PowerLauncherSettingsPage.xaml` as WPF UserControl

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Views/Settings/PowerLauncherSettingsPage.xaml`
- Create: `<PTRUN>/src/PowerLauncher/Views/Settings/PowerLauncherSettingsPage.xaml.cs`
- Reference: `<PT>/src/settings-ui/Settings.UI/SettingsXAML/Views/PowerLauncherPage.xaml` (812 lines, source of truth for which settings exist)

**Approach:** Translate each section of the source XAML to WPF equivalents. Use the §4 control mapping table from the design doc. Source has 4 main groups: Activation Shortcut, Search Results, Position & Appearance, Plugins. Maintain that structure.

- [ ] **Step 1: Create the UserControl skeleton**

```xml
<UserControl x:Class="PowerLauncher.Views.Settings.PowerLauncherSettingsPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:PowerLauncher.Controls"
             xmlns:vs="clr-namespace:PowerLauncher.Views.Settings"
             xmlns:p="clr-namespace:PowerLauncher.Properties">
  <UserControl.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="DataTemplates/PluginOptionTemplates.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </UserControl.Resources>

  <StackPanel Margin="24">
    <controls:InfoBar x:Name="ImportInfoBar" Visibility="Collapsed"
                      Title="{x:Static p:Resources.Import_InfoBar_Title}"
                      Message="{x:Static p:Resources.Import_InfoBar_Message}"
                      ActionText="{x:Static p:Resources.Import_InfoBar_Action}"
                      IsClosable="True"
                      ActionClicked="OnImportClicked"
                      Closed="OnImportInfoBarClosed" />

    <!-- Group 1: Enable + Activation shortcut -->
    <GroupBox Header="{x:Static p:Resources.PowerLauncher_Activation}" Margin="0,16,0,0">
      <StackPanel>
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_EnablePowerLauncher}" IsChecked="{Binding EnablePowerLauncher, Mode=TwoWay}" />
        <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
          <TextBlock Text="{x:Static p:Resources.PowerLauncher_ActivationShortcut}" Width="200" />
          <controls:ShortcutControl HotkeySettings="{Binding OpenPowerLauncher, Mode=TwoWay}" />
        </StackPanel>
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_IgnoreHotkeysInFullScreen}"
                  IsChecked="{Binding IgnoreHotkeysInFullScreen, Mode=TwoWay}" Margin="0,8,0,0" />
      </StackPanel>
    </GroupBox>

    <!-- Group 2: Search Results -->
    <GroupBox Header="{x:Static p:Resources.PowerLauncher_SearchResults}" Margin="0,16,0,0">
      <StackPanel>
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_SearchQueryResultsWithDelay}" IsChecked="{Binding SearchQueryResultsWithDelay, Mode=TwoWay}" />
        <Expander Header="{x:Static p:Resources.PowerLauncher_SearchInputDelays}" IsExpanded="False" Margin="0,4,0,0">
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*" />
              <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto" />
              <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>
            <TextBlock Grid.Row="0" Grid.Column="0" Text="{x:Static p:Resources.PowerLauncher_FastSearchInputDelayMs}" />
            <controls:NumberBox Grid.Row="0" Grid.Column="1" Width="100" MinValue="0" MaxValue="500" Value="{Binding SearchInputDelayFast, Mode=TwoWay}" />
            <TextBlock Grid.Row="1" Grid.Column="0" Text="{x:Static p:Resources.PowerLauncher_SlowSearchInputDelayMs}" Margin="0,8,0,0" />
            <controls:NumberBox Grid.Row="1" Grid.Column="1" Width="100" MinValue="0" MaxValue="1000" Value="{Binding SearchInputDelay, Mode=TwoWay}" Margin="0,8,0,0" />
          </Grid>
        </Expander>
        <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
          <TextBlock Text="{x:Static p:Resources.PowerLauncher_MaximumNumberOfResults}" Width="200" />
          <controls:NumberBox Width="100" MinValue="1" MaxValue="20" Value="{Binding MaximumNumberOfResults, Mode=TwoWay}" />
        </StackPanel>
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_ClearInputOnLaunch}" IsChecked="{Binding ClearInputOnLaunch, Mode=TwoWay}" Margin="0,8,0,0" />
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_TabSelectsContextButtons}" IsChecked="{Binding TabSelectsContextButtons, Mode=TwoWay}" />
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_UsePinyin}" IsChecked="{Binding UsePinyin, Mode=TwoWay}" />
        <CheckBox Content="{x:Static p:Resources.PowerLauncher_GenerateThumbnailsFromFiles}" IsChecked="{Binding GenerateThumbnailsFromFiles, Mode=TwoWay}" />
      </StackPanel>
    </GroupBox>

    <!-- Group 3: Position & Appearance -->
    <GroupBox Header="{x:Static p:Resources.Run_PositionAppearance}" Margin="0,16,0,0">
      <StackPanel>
        <StackPanel Orientation="Horizontal" Margin="0,4">
          <TextBlock Text="{x:Static p:Resources.Run_PositionHeader}" Width="200" />
          <ComboBox SelectedIndex="{Binding MonitorPositionIndex, Mode=TwoWay}" Width="240">
            <ComboBoxItem Content="{x:Static p:Resources.Run_Radio_Position_Cursor}" />
            <ComboBoxItem Content="{x:Static p:Resources.Run_Radio_Position_Primary_Monitor}" />
            <ComboBoxItem Content="{x:Static p:Resources.Run_Radio_Position_Focus}" />
          </ComboBox>
        </StackPanel>
        <StackPanel Orientation="Horizontal" Margin="0,4">
          <TextBlock Text="{x:Static p:Resources.ColorModeHeader}" Width="200" />
          <ComboBox SelectedIndex="{Binding ThemeIndex, Mode=TwoWay}" Width="240">
            <ComboBoxItem Content="{x:Static p:Resources.Radio_Theme_Dark}" />
            <ComboBoxItem Content="{x:Static p:Resources.Radio_Theme_Light}" />
            <ComboBoxItem Content="{x:Static p:Resources.Radio_Theme_Default}" />
          </ComboBox>
        </StackPanel>
        <StackPanel Orientation="Horizontal" Margin="0,4">
          <TextBlock Text="{x:Static p:Resources.PowerLauncher_TitleFontSize}" Width="200" />
          <Slider Width="240" Minimum="12" Maximum="24" SmallChange="2" LargeChange="2"
                  TickFrequency="2" TickPlacement="BottomRight" Value="{Binding TitleFontSize, Mode=TwoWay}" />
        </StackPanel>
      </StackPanel>
    </GroupBox>

    <!-- Group 4: Plugins -->
    <GroupBox Header="{x:Static p:Resources.PowerLauncher_Plugins}" Margin="0,16,0,0">
      <StackPanel>
        <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Mode=TwoWay}"
                 Tag="{x:Static p:Resources.PowerLauncher_SearchPluginsPlaceholder}" Margin="0,4" />
        <ItemsControl ItemsSource="{Binding Plugins}">
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Expander Margin="0,4">
                <Expander.Header>
                  <Grid>
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width="*" />
                      <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0">
                      <TextBlock Text="{Binding Name}" FontWeight="SemiBold" />
                      <TextBlock Text="{Binding Description}" Foreground="Gray" FontSize="11" />
                    </StackPanel>
                    <CheckBox Grid.Column="1" IsChecked="{Binding Disabled, Converter={StaticResource BoolNegationConverter}, Mode=TwoWay}" />
                  </Grid>
                </Expander.Header>
                <StackPanel Margin="16,4">
                  <StackPanel Orientation="Horizontal" Margin="0,2">
                    <TextBlock Text="{x:Static p:Resources.PowerLauncher_ActionKeyword}" Width="160" />
                    <TextBox Text="{Binding ActionKeyword, Mode=TwoWay}" Width="160" />
                  </StackPanel>
                  <CheckBox Content="{x:Static p:Resources.PowerLauncher_IncludeInGlobalResultTitle}" IsChecked="{Binding IsGlobal, Mode=TwoWay}" />
                  <StackPanel Orientation="Horizontal" Margin="0,2" IsEnabled="{Binding IsGlobalAndEnabled}">
                    <TextBlock Text="{x:Static p:Resources.PowerLauncher_PluginWeightBoost}" Width="160" />
                    <controls:NumberBox MinValue="-1000" MaxValue="1000" Value="{Binding WeightBoost, Mode=TwoWay}" Width="100" />
                  </StackPanel>
                  <ItemsControl ItemsSource="{Binding AdditionalOptions}"
                                ItemTemplateSelector="{StaticResource PluginOptionTemplateSelector}" />
                </StackPanel>
              </Expander>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </StackPanel>
    </GroupBox>
  </StackPanel>
</UserControl>
```

(Note: this is a representative simplification; engineer should verify each binding name against the source ViewModel and add any missing settings. Cross-check with `<PT>/src/settings-ui/Settings.UI/SettingsXAML/Views/PowerLauncherPage.xaml`.)

- [ ] **Step 2: Implement code-behind**

```csharp
// src/PowerLauncher/Views/Settings/PowerLauncherSettingsPage.xaml.cs
using System.Windows.Controls;
using PowerLauncher.Services;

namespace PowerLauncher.Views.Settings
{
    public partial class PowerLauncherSettingsPage : UserControl
    {
        private readonly SettingsImportService _import = new();

        public PowerLauncherSettingsPage()
        {
            InitializeComponent();
            DataContext = new PowerLauncherViewModel();
            UpdateImportInfoBarVisibility();
        }

        private void UpdateImportInfoBarVisibility()
        {
            ImportInfoBar.Visibility = (_import.IsLegacyDataAvailable() && _import.IsCurrentDataEmpty() && !UserPrefs.ImportDismissed)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void OnImportClicked(object sender, System.EventArgs e)
        {
            // dialog + import — implemented in Phase 8
        }

        private void OnImportInfoBarClosed(object sender, System.EventArgs e)
        {
            UserPrefs.ImportDismissed = true;
        }
    }
}
```

- [ ] **Step 3: Add `BoolNegationConverter` to `src/PowerLauncher/Helpers/`**

```csharp
// src/PowerLauncher/Helpers/BoolNegationConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace PowerLauncher.Helpers
{
    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}
```

Register in `App.xaml`:
```xml
<Application.Resources>
  <ResourceDictionary>
    <helpers:BoolNegationConverter x:Key="BoolNegationConverter"
                                   xmlns:helpers="clr-namespace:PowerLauncher.Helpers" />
  </ResourceDictionary>
</Application.Resources>
```

- [ ] **Step 4: Build, expect remaining errors about missing resources / templates**

Address by:
- Adding `PluginOptionTemplates.xaml` (next task)
- Adding `Resources.resx` keys (Phase 7) — for now, comment out `{x:Static p:Resources.X}` references and use literal English strings; uncomment after Phase 7

**IMPORTANT**: Phase 7 produces `Resources.Designer.cs` which generates the `p:Resources.X` symbols. The exact key names in the XAML must match what Phase 7 extracts. After Phase 7 runs, cross-reference each `{x:Static p:Resources.X}` against the generated Designer file; rename in XAML if a key is named differently in the source resw (e.g., `Run_PluginUseDescription` vs `PowerLauncher_PluginUseDescription`).

Comment out failing bindings temporarily; cycle build → fix → build.

- [ ] **Step 5: Commit**

```bash
git add src/PowerLauncher/Views/Settings/PowerLauncherSettingsPage.xaml src/PowerLauncher/Views/Settings/PowerLauncherSettingsPage.xaml.cs src/PowerLauncher/Helpers/BoolNegationConverter.cs src/PowerLauncher/App.xaml
git commit -m "feat: WPF rewrite of PowerLauncher Settings page"
```

### Task 6.4: Add 9 plugin AdditionalOption DataTemplates

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Views/Settings/DataTemplates/PluginOptionTemplates.xaml`
- Create: `<PTRUN>/src/PowerLauncher/Views/Settings/PluginOptionTemplateSelector.cs`

- [ ] **Step 1: Create `PluginOptionTemplateSelector.cs`**

```csharp
// src/PowerLauncher/Views/Settings/PluginOptionTemplateSelector.cs
using System.Windows;
using System.Windows.Controls;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerLauncher.Views.Settings
{
    public class PluginOptionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CheckBoxTemplate { get; set; }
        public DataTemplate ComboBoxTemplate { get; set; }
        public DataTemplate TextBoxTemplate { get; set; }
        public DataTemplate NumberBoxTemplate { get; set; }
        public DataTemplate MultilineTextBoxTemplate { get; set; }
        public DataTemplate CheckBoxComboBoxTemplate { get; set; }
        public DataTemplate CheckBoxTextBoxTemplate { get; set; }
        public DataTemplate CheckBoxNumberBoxTemplate { get; set; }
        public DataTemplate CheckBoxMultilineTextBoxTemplate { get; set; }
        public DataTemplate EmptyTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Checkbox => CheckBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Combobox => ComboBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Textbox => TextBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Numberbox => NumberBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.MultilineTextbox => MultilineTextBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndCombobox => CheckBoxComboBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox => CheckBoxTextBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndNumberbox => CheckBoxNumberBoxTemplate,
                PluginAdditionalOption opt when opt.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndMultilineTextbox => CheckBoxMultilineTextBoxTemplate,
                _ => EmptyTemplate
            };
        }
    }
}
```

(verify exact `AdditionalOptionType` enum names against `PluginAdditionalOption.cs` source)

- [ ] **Step 2: Create `PluginOptionTemplates.xaml`** with 9 + 1 templates

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="clr-namespace:PowerLauncher.Controls"
                    xmlns:vs="clr-namespace:PowerLauncher.Views.Settings">

  <DataTemplate x:Key="CheckBoxTemplate">
    <CheckBox Content="{Binding DisplayLabel}" IsChecked="{Binding Value, Mode=TwoWay}" Margin="0,2" />
  </DataTemplate>

  <DataTemplate x:Key="ComboBoxTemplate">
    <StackPanel Orientation="Horizontal" Margin="0,2">
      <TextBlock Text="{Binding DisplayLabel}" Width="200" />
      <ComboBox ItemsSource="{Binding ComboBoxItems}" DisplayMemberPath="Key" SelectedValuePath="Value"
                SelectedValue="{Binding ComboBoxValue, Mode=TwoWay}" Width="200" />
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="TextBoxTemplate">
    <StackPanel Orientation="Horizontal" Margin="0,2">
      <TextBlock Text="{Binding DisplayLabel}" Width="200" />
      <TextBox Text="{Binding TextValue, Mode=TwoWay}" Width="240" />
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="NumberBoxTemplate">
    <StackPanel Orientation="Horizontal" Margin="0,2">
      <TextBlock Text="{Binding DisplayLabel}" Width="200" />
      <controls:NumberBox MinValue="{Binding NumberBoxMin}" MaxValue="{Binding NumberBoxMax}"
                          Value="{Binding NumberValue, Mode=TwoWay}" Width="100" />
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="MultilineTextBoxTemplate">
    <StackPanel Margin="0,2">
      <TextBlock Text="{Binding DisplayLabel}" />
      <TextBox Text="{Binding TextValue, Mode=TwoWay}" AcceptsReturn="True" MinHeight="100" TextWrapping="Wrap" />
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="CheckBoxComboBoxTemplate">
    <StackPanel Margin="0,2">
      <CheckBox Content="{Binding DisplayLabel}" IsChecked="{Binding Value, Mode=TwoWay}" />
      <StackPanel Orientation="Horizontal" Margin="20,4,0,0" IsEnabled="{Binding SecondSettingIsEnabled}">
        <TextBlock Text="{Binding SecondDisplayLabel}" Width="180" />
        <ComboBox ItemsSource="{Binding ComboBoxItems}" DisplayMemberPath="Key" SelectedValuePath="Value"
                  SelectedValue="{Binding ComboBoxValue, Mode=TwoWay}" Width="200" />
      </StackPanel>
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="CheckBoxTextBoxTemplate">
    <StackPanel Margin="0,2">
      <CheckBox Content="{Binding DisplayLabel}" IsChecked="{Binding Value, Mode=TwoWay}" />
      <StackPanel Orientation="Horizontal" Margin="20,4,0,0" IsEnabled="{Binding SecondSettingIsEnabled}">
        <TextBlock Text="{Binding SecondDisplayLabel}" Width="180" />
        <TextBox Text="{Binding TextValue, Mode=TwoWay}" Width="240" />
      </StackPanel>
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="CheckBoxNumberBoxTemplate">
    <StackPanel Margin="0,2">
      <CheckBox Content="{Binding DisplayLabel}" IsChecked="{Binding Value, Mode=TwoWay}" />
      <StackPanel Orientation="Horizontal" Margin="20,4,0,0" IsEnabled="{Binding SecondSettingIsEnabled}">
        <TextBlock Text="{Binding SecondDisplayLabel}" Width="180" />
        <controls:NumberBox MinValue="{Binding NumberBoxMin}" MaxValue="{Binding NumberBoxMax}"
                            Value="{Binding NumberValue, Mode=TwoWay}" Width="100" />
      </StackPanel>
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="CheckBoxMultilineTextBoxTemplate">
    <StackPanel Margin="0,2">
      <CheckBox Content="{Binding DisplayLabel}" IsChecked="{Binding Value, Mode=TwoWay}" />
      <StackPanel Margin="20,4,0,0" IsEnabled="{Binding SecondSettingIsEnabled}">
        <TextBlock Text="{Binding SecondDisplayLabel}" />
        <TextBox Text="{Binding TextValue, Mode=TwoWay}" AcceptsReturn="True" MinHeight="100" TextWrapping="Wrap" />
      </StackPanel>
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="EmptyTemplate" />

  <vs:PluginOptionTemplateSelector x:Key="PluginOptionTemplateSelector"
    CheckBoxTemplate="{StaticResource CheckBoxTemplate}"
    ComboBoxTemplate="{StaticResource ComboBoxTemplate}"
    TextBoxTemplate="{StaticResource TextBoxTemplate}"
    NumberBoxTemplate="{StaticResource NumberBoxTemplate}"
    MultilineTextBoxTemplate="{StaticResource MultilineTextBoxTemplate}"
    CheckBoxComboBoxTemplate="{StaticResource CheckBoxComboBoxTemplate}"
    CheckBoxTextBoxTemplate="{StaticResource CheckBoxTextBoxTemplate}"
    CheckBoxNumberBoxTemplate="{StaticResource CheckBoxNumberBoxTemplate}"
    CheckBoxMultilineTextBoxTemplate="{StaticResource CheckBoxMultilineTextBoxTemplate}"
    EmptyTemplate="{StaticResource EmptyTemplate}" />
</ResourceDictionary>
```

- [ ] **Step 3: Build + open Settings window for visual check**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
dotnet run --project src/PowerLauncher/PowerLauncher.csproj
```

Right-click tray → Settings. Verify all 4 groups render and plugin list shows.

- [ ] **Step 4: Commit**

```bash
git add src/PowerLauncher/Views/Settings/DataTemplates/ src/PowerLauncher/Views/Settings/PluginOptionTemplateSelector.cs
git commit -m "feat: add 9 plugin AdditionalOption DataTemplates + selector"
```

---

## Phase 7: Localization

### Task 7.1: Write `tools/extract-localization.ps1`

**Files:**
- Create: `<PTRUN>/tools/extract-localization.ps1`

- [ ] **Step 1: Implement extraction script**

```powershell
# tools/extract-localization.ps1
# Extracts PowerLauncher-related localization keys from PT Settings.UI resw files
# and converts them to WPF resx files in the new repo.

param(
    [Parameter(Mandatory=$true)] [string] $PtSettingsUiStringsDir,  # e.g., <PT>/src/settings-ui/Settings.UI/Strings
    [Parameter(Mandatory=$true)] [string] $OutputDir                 # e.g., <PTRUN>/src/PowerLauncher/Properties
)

# Prefixes / exact key names to extract
$keyPatterns = @(
    '^PowerLauncher_',
    '^Run_',
    '^Activation_',
    '^Shortcut',
    '^Radio_Theme_',
    '^ColorModeHeader',
    '^ShowPluginsOverview_',
    '^GPO_SomeRunPlugins',
    '^LearnMore_Run',
    '^ToggleSwitch'
)

function Test-Key([string] $name)
{
    foreach ($p in $keyPatterns) {
        if ($name -match $p) { return $true }
    }
    return $false
}

Get-ChildItem -Path $PtSettingsUiStringsDir -Directory | ForEach-Object {
    $lang = $_.Name
    $reswPath = Join-Path $_.FullName "Resources.resw"
    if (-not (Test-Path $reswPath)) { return }

    $xml = [xml](Get-Content $reswPath -Encoding UTF8)
    $extracted = [System.Xml.XmlDocument]::new()
    $extracted.LoadXml($xml.OuterXml)
    $rootNode = $extracted.root
    $toRemove = @()
    foreach ($d in $rootNode.SelectNodes("data")) {
        if (-not (Test-Key $d.name)) { $toRemove += $d }
    }
    $toRemove | ForEach-Object { $rootNode.RemoveChild($_) | Out-Null }

    # Save as Resources.<lang>.resx (en-US becomes Resources.resx)
    $outName = if ($lang -eq "en-us") { "Resources.resx" } else { "Resources.$lang.resx" }
    $outPath = Join-Path $OutputDir $outName
    $extracted.Save($outPath)
    Write-Host "Wrote $outPath ($($rootNode.SelectNodes('data').Count) keys)"
}
```

- [ ] **Step 2: Run script for first time**

```bash
pwsh tools/extract-localization.ps1 -PtSettingsUiStringsDir "<PT>/src/settings-ui/Settings.UI/Strings" -OutputDir "src/PowerLauncher/Properties"
```

Expected: 18 .resx files created.

- [ ] **Step 3: Spot-check 3 languages**

Open `Resources.resx`, `Resources.zh-Hans.resx`, `Resources.de.resx`. Verify each contains keys like `PowerLauncher_EnablePowerLauncher`, `Run_PositionHeader`, etc.

- [ ] **Step 4: Add files to PowerLauncher.csproj**

In `src/PowerLauncher/PowerLauncher.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Update="Properties\Resources.resx">
    <Generator>PublicResXFileCodeGenerator</Generator>
    <LastGenOutput>Resources.Designer.cs</LastGenOutput>
  </EmbeddedResource>
</ItemGroup>
```

(Visual Studio will auto-generate `Resources.Designer.cs`.)

- [ ] **Step 5: Build**

```bash
dotnet build src/PowerLauncher/PowerLauncher.csproj
```

Expected: green; satellite assemblies generated for each language.

- [ ] **Step 6: Add custom InfoBar import keys (not in PT)**

These keys don't exist in PT — add them by hand to all 18 .resx files (English first, then translation TBD or copy English):
- `Import_InfoBar_Title` = "Existing PowerToys Run settings detected"
- `Import_InfoBar_Message` = "We found existing PowerToys Run settings. Would you like to import them?"
- `Import_InfoBar_Action` = "Import"

(For non-English: leave English text initially; translation is a follow-up task that's out-of-scope for this plan.)

- [ ] **Step 7: Commit**

```bash
git add tools/extract-localization.ps1 src/PowerLauncher/Properties/Resources*.resx src/PowerLauncher/PowerLauncher.csproj
git commit -m "feat: extract PowerLauncher localization (18 languages) and wire to WPF"
```

### Task 7.2: Verify localization at runtime

- [ ] **Step 1: Set system language to non-English (e.g., German)**

Or set `Thread.CurrentUICulture` programmatically in `App.xaml.cs::Main()` for testing:

```csharp
System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");
```

- [ ] **Step 2: Run app, open Settings window**

Verify labels appear in German.

- [ ] **Step 3: Revert temp culture override**

- [ ] **Step 4: Commit any fixes**

---

## Phase 8: Settings Import Service

### Task 8.1: Implement `SettingsImportService`

**Files:**
- Create: `<PTRUN>/src/PowerLauncher/Services/SettingsImportService.cs`
- Create: `<PTRUN>/src/Wox.Test/Services/SettingsImportServiceTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// src/Wox.Test/Services/SettingsImportServiceTests.cs
using NUnit.Framework;
using PowerLauncher.Services;
using System.IO;

[TestFixture]
public class SettingsImportServiceTests
{
    private string _tempLegacyDir;
    private string _tempNewDir;

    [SetUp]
    public void Setup()
    {
        _tempLegacyDir = Path.Combine(Path.GetTempPath(), "ptrun-test-legacy-" + Path.GetRandomFileName());
        _tempNewDir = Path.Combine(Path.GetTempPath(), "ptrun-test-new-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempLegacyDir);
        Directory.CreateDirectory(_tempNewDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_tempLegacyDir)) Directory.Delete(_tempLegacyDir, true);
        if (Directory.Exists(_tempNewDir)) Directory.Delete(_tempNewDir, true);
    }

    [Test]
    public void IsLegacyDataAvailable_NoLegacyFile_ReturnsFalse()
    {
        var svc = new SettingsImportService(_tempLegacyDir, _tempNewDir);
        Assert.That(svc.IsLegacyDataAvailable(), Is.False);
    }

    [Test]
    public void IsLegacyDataAvailable_WithFile_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempLegacyDir, "settings.json"), "{}");
        var svc = new SettingsImportService(_tempLegacyDir, _tempNewDir);
        Assert.That(svc.IsLegacyDataAvailable(), Is.True);
    }

    [Test]
    public void Import_CopiesSettingsFile()
    {
        File.WriteAllText(Path.Combine(_tempLegacyDir, "settings.json"), "{\"foo\":42}");
        var svc = new SettingsImportService(_tempLegacyDir, _tempNewDir);
        var result = svc.Import();
        Assert.That(result.Success, Is.True);
        var copied = File.ReadAllText(Path.Combine(_tempNewDir, "settings.json"));
        Assert.That(copied, Is.EqualTo("{\"foo\":42}"));
    }

    [Test]
    public void Import_CopiesPluginConfigsButNotCache()
    {
        var pluginDir = Path.Combine(_tempLegacyDir, "Plugins", "Calculator");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "settings.json"), "{}");
        var cacheDir = Path.Combine(pluginDir, "cache");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "data.bin"), "binary");

        var svc = new SettingsImportService(_tempLegacyDir, _tempNewDir);
        var result = svc.Import();

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(Path.Combine(_tempNewDir, "Plugins", "Calculator", "settings.json")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempNewDir, "Plugins", "Calculator", "cache")), Is.False);
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

- [ ] **Step 3: Implement**

```csharp
// src/PowerLauncher/Services/SettingsImportService.cs
using System;
using System.IO;

namespace PowerLauncher.Services
{
    public sealed class SettingsImportService
    {
        public static string DefaultLegacyDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "PowerToys", "PowerToys Run");

        public static string DefaultNewDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerToys Run");

        // Plugin to skip (deleted in standalone)
        private const string DeletedPlugin = "Microsoft.PowerToys.Run.Plugin.PowerToys";

        private readonly string _legacyDir;
        private readonly string _newDir;

        public SettingsImportService() : this(DefaultLegacyDir, DefaultNewDir) { }
        public SettingsImportService(string legacyDir, string newDir)
        {
            _legacyDir = legacyDir;
            _newDir = newDir;
        }

        public bool IsLegacyDataAvailable() => File.Exists(Path.Combine(_legacyDir, "settings.json"));

        public bool IsCurrentDataEmpty()
        {
            var settingsPath = Path.Combine(_newDir, "settings.json");
            if (!File.Exists(settingsPath)) return true;
            var info = new FileInfo(settingsPath);
            return info.Length < 100; // empirical: empty default settings file is ~50-80 bytes
        }

        public ImportResult Import(IProgress<string> progress = null)
        {
            if (!IsLegacyDataAvailable()) return ImportResult.Fail("Legacy data not found");

            try
            {
                Directory.CreateDirectory(_newDir);

                // Backup current
                BackupCurrent();

                // Copy settings.json
                progress?.Report("Copying settings.json");
                File.Copy(
                    Path.Combine(_legacyDir, "settings.json"),
                    Path.Combine(_newDir, "settings.json"),
                    overwrite: true);

                // Copy plugin configs (settings.json only — skip cache + deleted plugin)
                var legacyPluginsDir = Path.Combine(_legacyDir, "Plugins");
                if (Directory.Exists(legacyPluginsDir))
                {
                    foreach (var pluginDir in Directory.EnumerateDirectories(legacyPluginsDir))
                    {
                        var pluginName = Path.GetFileName(pluginDir);
                        if (string.Equals(pluginName, DeletedPlugin, StringComparison.OrdinalIgnoreCase)) continue;

                        var srcSettings = Path.Combine(pluginDir, "settings.json");
                        if (!File.Exists(srcSettings)) continue;

                        var dstPluginDir = Path.Combine(_newDir, "Plugins", pluginName);
                        Directory.CreateDirectory(dstPluginDir);
                        File.Copy(srcSettings, Path.Combine(dstPluginDir, "settings.json"), overwrite: true);
                        progress?.Report($"Copied {pluginName}");
                    }
                }

                return ImportResult.Ok();
            }
            catch (Exception ex)
            {
                return ImportResult.Fail(ex.Message);
            }
        }

        private void BackupCurrent()
        {
            var backupDir = Path.Combine(_newDir, "backups");
            Directory.CreateDirectory(backupDir);
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var settingsPath = Path.Combine(_newDir, "settings.json");
            if (File.Exists(settingsPath))
                File.Copy(settingsPath, Path.Combine(backupDir, $"settings-{ts}.json"));
        }
    }

    public sealed class ImportResult
    {
        public bool Success { get; init; }
        public string Error { get; init; }

        public static ImportResult Ok() => new() { Success = true };
        public static ImportResult Fail(string err) => new() { Success = false, Error = err };
    }
}
```

- [ ] **Step 4: Run tests, expect PASS**

```bash
dotnet test --filter "SettingsImportServiceTests"
```

- [ ] **Step 5: Commit**

```bash
git add src/PowerLauncher/Services/SettingsImportService.cs src/Wox.Test/Services/SettingsImportServiceTests.cs
git commit -m "feat: add SettingsImportService"
```

### Task 8.2: Wire import to Settings UI

**Files:**
- Modify: `<PTRUN>/src/PowerLauncher/Views/Settings/PowerLauncherSettingsPage.xaml.cs`

- [ ] **Step 1: Implement `OnImportClicked`**

```csharp
private async void OnImportClicked(object sender, EventArgs e)
{
    var confirm = MessageBox.Show(
        "This will overwrite your current PowerToys Run settings. A backup will be saved. Continue?",
        "Import settings",
        MessageBoxButton.OKCancel,
        MessageBoxImage.Question);

    if (confirm != MessageBoxResult.OK) return;

    var progress = new Progress<string>(msg => { /* show in status bar; minimal here */ });
    var result = await Task.Run(() => _import.Import(progress));

    if (result.Success)
    {
        // Trigger reload in same process (Phase 4 wired this)
        SettingsReader.Reload();
        MessageBox.Show("Settings imported. A restart may be needed for some plugins.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        ImportInfoBar.Visibility = Visibility.Collapsed;
    }
    else
    {
        MessageBox.Show($"Import failed: {result.Error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 2: Add `UserPrefs` static for `ImportDismissed` flag**

```csharp
// src/PowerLauncher/Helpers/UserPrefs.cs
namespace PowerLauncher.Helpers
{
    public static class UserPrefs
    {
        // Reads/writes a small JSON file in %LOCALAPPDATA%\PowerToys Run\userprefs.json
        public static bool ImportDismissed { get; set; }  // implement get/set with file backing
    }
}
```

(Engineer fills in trivial JSON file persistence.)

- [ ] **Step 3: Build + manual smoke test**

Open Settings; if old PT data exists, verify InfoBar shows. Click Import → verify settings copied.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: wire SettingsImportService to UI InfoBar"
```

---

## Phase 9: Installer (WiX 4 dual-mode MSI)

### Task 9.1: Bootstrap WiX project

**Files:**
- Create: `<PTRUN>/installer/PowerToysRunSetup/PowerToysRunSetup.wixproj`
- Create: `<PTRUN>/installer/PowerToysRunSetup/Product.wxs`
- Reference: `<PT>/installer/PowerToysSetupVNext/Run.wxs`, `<PT>/installer/PowerToysSetupVNext/Common.wxi`

- [ ] **Step 1: Create `PowerToysRunSetup.wixproj`**

```xml
<Project Sdk="WixToolset.Sdk/4.0.0">
  <PropertyGroup>
    <OutputType>Package</OutputType>
    <OutputName>PowerToysRunSetup-$(Platform)</OutputName>
    <Cultures>en-us</Cultures>
    <Platforms>x64;arm64</Platforms>
    <DefineConstants>BinDir=$(SolutionDir)bin\$(Platform)\$(Configuration)\</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Product.wxs" />
    <Compile Include="Run.wxs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create `Product.wxs`** (minimal, dual-scope)

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="PowerToys Run"
           Manufacturer="Microsoft Corporation"
           Version="1.0.0.0"
           UpgradeCode="PUT-NEW-GUID-HERE-XXX-XXX-XXX-XXX"
           Scope="perUserOrMachine"
           InstallerVersion="500">

    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />

    <Feature Id="MainFeature" Title="PowerToys Run" Level="1">
      <ComponentGroupRef Id="PowerLauncherFiles" />
      <ComponentGroupRef Id="PluginFiles" />
    </Feature>

    <ui:WixUI Id="WixUI_InstallDir" />
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
  </Package>
</Wix>
```

Replace `PUT-NEW-GUID-HERE-XXX...` with a freshly generated GUID via `[guid]::NewGuid()`.

- [ ] **Step 3: Adapt `Run.wxs` from PT**

Copy `<PT>/installer/PowerToysSetupVNext/Run.wxs` to `<PTRUN>/installer/PowerToysRunSetup/Run.wxs`. Strip:
- All references to `BaseApplicationsAssetsFolder` (PT-specific) — replace with own `INSTALLFOLDER`
- `LauncherImagesFolder` parent reference — make it a child of `INSTALLFOLDER`
- The `Microsoft.PowerToys.Run.Plugin.PowerToys` plugin entries (deleted)
- `PowerLauncher.Telemetry` files

- [ ] **Step 4: Add WixUI_InstallDir license + ARP entries**

Set `ARPCONTACT`, `ARPHELPLINK`, `ARPNOREPAIR=1`.

- [ ] **Step 5: Build MSI**

```bash
dotnet build installer/PowerToysRunSetup/PowerToysRunSetup.wixproj /p:Platform=x64
```

Expected: produces `bin\x64\Release\PowerToysRunSetup-x64.msi`.

- [ ] **Step 6: Manual install test**

```bash
msiexec /i bin\x64\Release\PowerToysRunSetup-x64.msi
```

Verify: installs to `%LOCALAPPDATA%\Programs\PowerToys Run\` (per-user); `PowerToys.PowerLauncher.exe` runs.

- [ ] **Step 7: Commit**

```bash
git add installer/PowerToysRunSetup/
git commit -m "feat: add WiX 4 installer (per-user/per-machine dual-mode)"
```

### Task 9.2: Verify per-machine install path

- [ ] **Step 1: Uninstall any prior install**

```bash
msiexec /x bin\x64\Release\PowerToysRunSetup-x64.msi
```

- [ ] **Step 2: Install per-machine**

```bash
msiexec /i bin\x64\Release\PowerToysRunSetup-x64.msi ALLUSERS=1
```

(Requires admin elevation.)

Verify: installs to `C:\Program Files\PowerToys Run\`.

- [ ] **Step 3: Commit any fixes needed**

---

## Phase 10: CI / GitHub Actions

### Task 10.1: PR build workflow

**Files:**
- Create: `<PTRUN>/.github/workflows/build.yml`

- [ ] **Step 1: Implement workflow**

```yaml
name: Build
on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

jobs:
  build:
    runs-on: windows-2022
    strategy:
      matrix:
        platform: [x64, arm64]
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - name: Restore
        run: dotnet restore PowerToysRun.sln
      - name: Build
        run: dotnet build PowerToysRun.sln /p:Platform=${{ matrix.platform }} /p:Configuration=Release --no-restore
      - name: Test
        if: matrix.platform == 'x64'  # tests only on x64 (no arm64 runner available)
        run: dotnet test PowerToysRun.sln /p:Platform=${{ matrix.platform }} /p:Configuration=Release --no-build
      - name: Build MSI
        run: dotnet build installer/PowerToysRunSetup/PowerToysRunSetup.wixproj /p:Platform=${{ matrix.platform }} /p:Configuration=Release
      - name: Upload MSI
        uses: actions/upload-artifact@v4
        with:
          name: PowerToysRunSetup-${{ matrix.platform }}
          path: installer/PowerToysRunSetup/bin/${{ matrix.platform }}/Release/*.msi
```

- [ ] **Step 2: Push and verify CI runs green**

```bash
git add .github/workflows/build.yml
git commit -m "ci: add PR build workflow (x64 + arm64)"
git push origin main
```

Watch GitHub Actions tab. Fix any CI-only issues.

- [ ] **Step 3: Once green, commit fixes**

### Task 10.2: Release workflow (no signing yet)

**Files:**
- Create: `<PTRUN>/.github/workflows/release.yml`

- [ ] **Step 1: Implement workflow** (signing left as TODO; see §8 R2)

```yaml
name: Release
on:
  push:
    tags: ['v*']

jobs:
  release:
    runs-on: windows-2022
    permissions:
      contents: write
    strategy:
      matrix:
        platform: [x64, arm64]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build PowerToysRun.sln /p:Platform=${{ matrix.platform }} /p:Configuration=Release
      - run: dotnet build installer/PowerToysRunSetup/PowerToysRunSetup.wixproj /p:Platform=${{ matrix.platform }} /p:Configuration=Release
      # TODO: sign MSI with ESRP (Phase 11 / §8 R2)
      - uses: softprops/action-gh-release@v2
        with:
          files: installer/PowerToysRunSetup/bin/${{ matrix.platform }}/Release/*.msi
          generate_release_notes: true
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add release workflow (signing TBD)"
```

---

## Phase 11: winget Manifest

### Task 11.1: Author winget manifest

**Files:**
- Create: `<PTRUN>/winget/manifest/Microsoft.PowerToysRun.installer.yaml`
- Create: `<PTRUN>/winget/manifest/Microsoft.PowerToysRun.locale.en-US.yaml`
- Create: `<PTRUN>/winget/manifest/Microsoft.PowerToysRun.yaml`

- [ ] **Step 1: Create version manifest**

```yaml
# Microsoft.PowerToysRun.yaml
PackageIdentifier: Microsoft.PowerToysRun
PackageVersion: 1.0.0
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
```

- [ ] **Step 2: Create locale manifest**

```yaml
# Microsoft.PowerToysRun.locale.en-US.yaml
PackageIdentifier: Microsoft.PowerToysRun
PackageVersion: 1.0.0
PackageLocale: en-US
Publisher: Microsoft Corporation
PublisherUrl: https://github.com/microsoft/PowerToysRun
PackageName: PowerToys Run
PackageUrl: https://github.com/microsoft/PowerToysRun
License: MIT
LicenseUrl: https://github.com/microsoft/PowerToysRun/blob/main/LICENSE
ShortDescription: Quick keyboard launcher for Windows.
Tags: [launcher, productivity, windows]
ManifestType: defaultLocale
ManifestVersion: 1.6.0
```

- [ ] **Step 3: Create installer manifest with both scopes**

```yaml
# Microsoft.PowerToysRun.installer.yaml
PackageIdentifier: Microsoft.PowerToysRun
PackageVersion: 1.0.0
InstallerType: msi
Installers:
  - Architecture: x64
    Scope: user
    InstallerUrl: https://github.com/microsoft/PowerToysRun/releases/download/v1.0.0/PowerToysRunSetup-x64.msi
    InstallerSha256: TBD-after-release
    InstallerSwitches:
      Custom: ALLUSERS=""
  - Architecture: x64
    Scope: machine
    InstallerUrl: https://github.com/microsoft/PowerToysRun/releases/download/v1.0.0/PowerToysRunSetup-x64.msi
    InstallerSha256: TBD-after-release
    InstallerSwitches:
      Custom: ALLUSERS=1
  - Architecture: arm64
    Scope: user
    InstallerUrl: https://github.com/microsoft/PowerToysRun/releases/download/v1.0.0/PowerToysRunSetup-arm64.msi
    InstallerSha256: TBD-after-release
    InstallerSwitches:
      Custom: ALLUSERS=""
  - Architecture: arm64
    Scope: machine
    InstallerUrl: https://github.com/microsoft/PowerToysRun/releases/download/v1.0.0/PowerToysRunSetup-arm64.msi
    InstallerSha256: TBD-after-release
    InstallerSwitches:
      Custom: ALLUSERS=1
ManifestType: installer
ManifestVersion: 1.6.0
```

(After first release, replace `TBD-after-release` SHAs with actual file SHA256.)

- [ ] **Step 4: Test locally (optional)**

```bash
winget validate winget/manifest/
```

- [ ] **Step 5: Commit**

```bash
git add winget/manifest/
git commit -m "feat: add winget manifest template"
```

(Actual PR to `microsoft/winget-pkgs` happens after first release. Out of scope for this plan; document in release runbook.)

---

## Phase 12: Compatibility & Acceptance Validation

### Task 12.1: Top-N community plugin compatibility test

**Files:**
- (no code; manual testing)

- [ ] **Step 1: Build a Release MSI and install it**

- [ ] **Step 2: Pick at least 3 popular Run community plugins** (search GitHub for "PowerToys Run plugin"). Examples (verify currently popular):
  - `WingetUI / SomePackageManagerPlugin` (or similar)
  - A "translator" plugin
  - A "GitHub repos search" plugin

- [ ] **Step 3: For each plugin, drop the released DLL bundle into the new install path**

```bash
# Path varies per install scope:
copy <plugin-bundle>\* "%LOCALAPPDATA%\Programs\PowerToys Run\RunPlugins\<PluginName>\"
```

- [ ] **Step 4: Restart PowerToys Run; verify each plugin appears in Settings UI plugin list and works at runtime**

- [ ] **Step 5: Document results**

Create `doc/COMPATIBILITY.md` listing tested community plugins + status.

- [ ] **Step 6: If any plugin fails to load**, capture error log and triage:
- Missing dependency? Add to PowerLauncher
- Strong-name version mismatch? Investigate further (this is the biggest R1 risk)
- Settings.UI.Library namespace miss? Verify our vendored copy has the class

- [ ] **Step 7: Commit**

```bash
git add doc/COMPATIBILITY.md
git commit -m "docs: record top community plugin compat test results"
```

### Task 12.2: Run all 10 acceptance criteria from spec

For each of the 10 criteria in spec section 4 ("验收标准"), run the procedure and check pass/fail.

- [ ] **AC1: `dotnet build PowerToysRun.sln` x64 — green**
- [ ] **AC2: `dotnet test PowerToysRun.sln` — all pass**
- [ ] **AC3: Install MSI; `PowerToys.PowerLauncher.exe` runs**
- [ ] **AC4: Press Alt+Space; search window appears**
- [ ] **AC5: Test Calculator / Program / Folder / Shell / WindowWalker plugins**
- [ ] **AC6: Open Settings from tray; change hotkey, theme, enable/disable plugin; verify immediate effect**
- [ ] **AC7: With legacy PT data on disk, verify Import button works and copies settings**
- [ ] **AC8: Switch to non-English locale (e.g., zh-Hans, de); verify no empty strings in Settings UI**
- [ ] **AC9: Drop in 3 community plugins (Task 12.1) — verify they load and function**
- [ ] **AC10: Uninstall MSI; verify `%LOCALAPPDATA%\PowerToys Run\` user data is preserved**

- [ ] **Document results in `doc/ACCEPTANCE.md`**

- [ ] **Commit**

```bash
git add doc/ACCEPTANCE.md
git commit -m "docs: record acceptance criteria results"
```

### Task 12.3: Tag v1.0.0 and trigger release

- [ ] **Step 1: Once all AC pass, tag**

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions release workflow runs; MSIs uploaded to GitHub Release.

- [ ] **Step 2: Compute SHA256 of each released MSI; update winget manifest**

```bash
sha256sum PowerToysRunSetup-x64.msi
sha256sum PowerToysRunSetup-arm64.msi
```

Update `winget/manifest/Microsoft.PowerToysRun.installer.yaml` with real SHAs.

- [ ] **Step 3: Submit winget PR to `microsoft/winget-pkgs`**

(Manual; not in CI.)

---

## Done

Once all phases complete and acceptance criteria green, the standalone repository **`microsoft/PowerToysRun`** has shipped v1.0.0 and is independently maintainable.

Subsequent (out-of-scope) work:
- Remove `src/modules/launcher/` and related coupling from PT main repo
- Add deprecation notice in PT pointing to new repo
- Plan future improvements (modern UI, telemetry opt-in, in-app update, etc.)
