# Project setup & scaffolding

How to create, place, name, register, and build the new `.Next` test project. The starter files live
in [../templates/](../templates/).

## 1. Decide the name and location

| Scenario | Project name | Folder |
|---|---|---|
| **A — Port** (legacy UI tests exist) | `[Module].UITests.Next` | `src/modules/[Module]/Tests/[Module].UITests.Next/` |
| **B — Greenfield** (no UI tests) | `[Module].UITests` | `src/modules/[Module]/Tests/[Module].UITests/` |

Rules and judgment:

- **The `.Next` suffix exists only to avoid colliding with an existing legacy project.** If there is
  nothing to live alongside (Scenario B), drop it.
- **Match the module's existing test layout.** Many modules already nest tests under a `Tests/`
  folder (`MeasureTool/Tests/ScreenRuler.UITests`, `LightSwitch/Tests/LightSwitch.UITests`); others
  put the UI-tests project directly under the module root (`colorPicker/ColorPicker.UITests`,
  `fancyzones/FancyZones.UITests`). **Mirror whatever the module already does** — don't invent a new
  structure. The path-segment count only changes the relative `..\` depth to `common\` in the csproj.
- Keep the **`AssemblyName`** matching the project name (`[Module].UITests.Next`) so logs and build
  artifacts are unambiguous; there's no need to strip the `.Next` from the assembly name.
- If the legacy project has an unusual file name (e.g. `HostsEditor.UITests.csproj` inside a
  `Hosts.UITests/` folder), prefer a clean `[Module].UITests.Next.csproj`; consistency with the new
  examples (`ColorPicker.UITests.csproj`, `Settings.UITests.csproj`) wins.

## 2. Scaffold the csproj

Copy [../templates/Module.UITests.Next.csproj](../templates/Module.UITests.Next.csproj) and replace the
`__MODULE__` placeholder (and fix the `..\` depth on the ProjectReference). The reference csproj
(ColorPicker, whose project folder sits 3 levels under `src/`) is:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Look at Directory.Build.props in root for common stuff as well -->
  <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <RootNamespace>Microsoft.__MODULE__.UITests</RootNamespace>
    <AssemblyName>__MODULE__.UITests.Next</AssemblyName>

    <!-- Microsoft.Testing.Platform: appears in Test Explorer AND runs via dotnet test / vstest. -->
    <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>

    <!-- UI tests need a live desktop; never run them as part of MSBuild. -->
    <RunVSTest>false</RunVSTest>
  </PropertyGroup>

  <!-- Stage the built test app under <Platform>\<Configuration>\tests\ so the UI-tests build
       pipeline (CopyFiles glob **/<plat>/<config>/tests/**) picks it up. -->
  <PropertyGroup>
    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)\tests\__MODULE__.UITests.Next\</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" />
  </ItemGroup>

  <ItemGroup>
    <!-- Adjust the ..\ depth to reach src\common from THIS project's folder. -->
    <ProjectReference Include="..\..\..\common\UITestAutomation.Next\UITestAutomation.Next.csproj" />
  </ItemGroup>
</Project>
```

Critical, non-negotiable bits (CI audits or the build will fail without them):

1. **`<Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />`** immediately after the
   `<Project Sdk=...>` line. `.pipelines/verifyCommonProps.ps1` requires it on every `src/**` csproj.
2. **`OutputType=Exe`**, **`IsTestingPlatformApplication=true`**, **`EnableMSTestRunner=true`** — the
   Microsoft.Testing.Platform runner the rest of the repo uses; this is what makes the class appear in
   Test Explorer and run via `dotnet test`/`vstest.console.exe`.
3. **`<OutputPath>$(RepoRoot)$(Platform)\$(Configuration)\tests\<Name>\</OutputPath>`** — stages the
   build output where the UI-tests pipeline globs (`**/<plat>/<config>/tests/**`). Without it the app
   builds to `bin\` and is never picked up by the test job.
4. **`RunVSTest=false`** — UI tests must not run during MSBuild.
5. **ProjectReference to `UITestAutomation.Next.csproj` only** — never the legacy
   `UITestAutomation.csproj`. Fix the `..\` depth to match the folder nesting:
   - `src/modules/<M>/Tests/<M>.UITests.Next/` (4 levels under `src`) → `..\..\..\..\common\UITestAutomation.Next\UITestAutomation.Next.csproj`
   - `src/modules/<M>/<M>.UITests/` (3 levels under `src`) → `..\..\..\common\UITestAutomation.Next\UITestAutomation.Next.csproj`
   - `src/settings-ui/<M>.UITests/` (2 levels under `src`) → `..\..\common\UITestAutomation.Next\UITestAutomation.Next.csproj`

> Use `MSTest` (the meta-package) for a test **Exe**, matching the ColorPicker/Settings examples — not
> the bare `MSTest.TestFramework` the harness library itself uses.

## 3. Register in `PowerToys.slnx`

Add the project to [../../../../PowerToys.slnx](../../../../PowerToys.slnx) inside the module's
`<Folder>`, right next to the legacy project (Scenario A) so they're visually paired:

```xml
<Project Path="src/modules/<Module>/Tests/<Module>.UITests.Next/<Module>.UITests.Next.csproj">
  <Platform Solution="*|ARM64" Project="ARM64" />
  <Platform Solution="*|x64" Project="x64" />
</Project>
```

Match the `<Platform>` mapping block of the sibling projects in the same folder (every UI-tests entry
uses the `*|ARM64 → ARM64` / `*|x64 → x64` pair shown above).

## 4. Add the test class(es) and shared helper

Copy [../templates/ModuleEndToEndTests.cs](../templates/ModuleEndToEndTests.cs) into the project,
rename it to `[Module]EndToEndTests.cs` (or keep the legacy test-class names in Scenario A), and start
filling in test methods.

For anything beyond a single trivial test, also copy
[../templates/TestHelper.cs](../templates/TestHelper.cs) — a static helper with the reusable building
blocks every port needs (navigate to the page, toggle + verify the process, read the activation
shortcut, discover/activate/close the module window with patient retry, clipboard, screen-center).
Fill in the `__MODULE__` / `__MODULEUI__` / AutomationId placeholders and delete what you don't use.
This mirrors how the legacy suites are organized (a `TestHelper` + thin test classes) and is exactly
the shape of the validated ScreenRuler port.

The standard file header is required on every `.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
```

## 4b. (Coordinate-exact tests only) add a DPI-aware `app.manifest`

If any test drives the mouse by **pixel coordinates** and asserts on an **exact** value (a drag that
must measure `100 x 100`, a click at a precise point), the test host MUST be per-monitor DPI aware,
otherwise `MouseHelper`'s `SetCursorPos`/`GetCursorPos` are virtualized by the display scale and stop
matching winappcli's physical-pixel bounds (a 99px drag measured ~149px on a 150% display).

Copy [../templates/app.manifest](../templates/app.manifest) into the project (or the one from the
module's legacy UITests project) and reference it in the csproj:

```xml
<PropertyGroup>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

Tests that only assert on **format** (regex like `\d+ x \d+`) or never touch raw coordinates don't
need the manifest — which is why ColorPicker/Settings `.Next` projects omit it.

## 5. Build & run

```pwsh
# 0. FIRST build of a new project: restore so project.assets.json exists (else NETSDK1004).
dotnet restore src\modules\<Module>\Tests\<Module>.UITests.Next\<Module>.UITests.Next.csproj -p:Platform=x64
#    (or run tools\build\build-essentials.cmd once at the start of the session.)

# 1. Build only this project (fast). Exit code 0 = success.
tools\build\build.cmd -Path src\modules\<Module>\Tests\<Module>.UITests.Next -Platform x64 -Configuration Debug

# 2. Run (needs a live desktop + winapp.exe). A .Next project is a Microsoft.Testing.Platform Exe,
#    so run the produced exe directly (Test Explorer also works). Filter + TRX report for a tight loop:
$exe = "$PWD\x64\Debug\tests\<Module>.UITests.Next\net10.0-windows10.0.26100.0\<Module>.UITests.Next.exe"
& $exe --filter "TestCategory=<Cat>" --report-trx --report-trx-filename run.trx --results-directory .\TestResults\<Module>
#    --filter accepts "TestCategory=X" or "FullyQualifiedName~Y"; omit it to run everything. Exit 0 = all passed.
```

- On build failure, read `build.<Configuration>.<Platform>.errors.log` next to the project.
- `winapp.exe` is a **run-time** prerequisite only (`winget install Microsoft.winappcli`, or set
  `WINAPP_CLI_PATH`). A migration that compiles clean is valid even where the CLI/desktop is absent;
  say so and list coverage.
- `dotnet test` also works for a one-shot run, but prefer the produced exe for a fast iterate loop and
  do **not** run UI tests from inside an MSBuild step — they need an interactive session.
