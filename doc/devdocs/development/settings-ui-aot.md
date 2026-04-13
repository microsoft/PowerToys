# Settings UI Native AOT

This document describes the current Native AOT status of the PowerToys Settings UI, the local publish workflow, and the formal packaging flow used to feed the installer without changing the installed layout.

## Current Status

As of March 13, 2026, the branch is at the following stage:

- Regular Settings builds succeed in Visual Studio.
- Native AOT publish for `src/settings-ui/Settings.UI/PowerToys.Settings.csproj` succeeds.
- The published `PowerToys.Settings.exe` can be launched as a standalone app with dummy pipe arguments and does not exit immediately.
- The installer pipeline can now stage Settings AOT publish output and normalize it back into `x64\Release\WinUI3Apps` before WiX harvest.
- The branch still has unresolved AOT follow-up work such as trimming/AOT warning cleanup and broader functional validation with the Runner.

## Build Requirements

- Visual Studio 2026 or Visual Studio 2022 with Native AOT support installed
- Windows App SDK prerequisites already available in the repo development environment
- A Visual Studio developer command prompt configured for the target architecture

Important:

- If you are publishing `win-x64`, use an x64 developer command prompt.
- On ARM64 developer machines, make sure the environment is configured for `-arch=x64`.
- If the environment is set up as x86 by mistake, Native AOT publish can fail at link time with hundreds of unresolved externals such as `wmainCRTStartup`, `CloseHandle`, or `CoInitializeEx`.

## Build Commands

### Standard Visual Studio build

The project builds successfully in Visual Studio without extra steps.

### Native AOT publish

Run this from a Visual Studio developer command prompt at the repository root:

```pwsh
msbuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /m
```

If you are on an ARM64 machine and need the equivalent environment setup from a regular terminal, use:

```cmd
call "C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat" -no_logo -arch=x64 -host_arch=arm64
```

Then run the same `msbuild` command.

## Publish Output

The published output is generated under:

```text
x64\Release\WinUI3Apps\win-x64\publish
```

The Native AOT executable is:

```text
x64\Release\WinUI3Apps\win-x64\publish\PowerToys.Settings.exe
```

## Run The Standalone AOT App

From the publish directory, run:

```pwsh
.\PowerToys.Settings.exe dummy_pipe_1 dummy_pipe_2 0 system false false false false false
```

These dummy arguments are sufficient to smoke test standalone startup of the published app.

## Formal Packaging Flow

For installer packaging, do not point WiX at `win-x64\publish` directly.

Instead, use the installer build pipeline with the Settings AOT switch:

```pwsh
.\tools\build\build-installer.ps1 -Platform x64 -Configuration Release -EnableSettingsAOT
```

That flow now does the following:

1. Builds the main solution, but skips the regular `PowerToys.Settings.csproj` build so that the non-AOT Settings app is not emitted into `WinUI3Apps`.
2. Publishes `PowerToys.Settings.csproj` with Native AOT into the staging directory:

```text
x64\Release\AotPublish\Settings
```

3. Cleans stale non-AOT Settings artifacts from `WinUI3Apps`.
4. Copies the AOT publish output back into the existing installer-facing layout:

```text
x64\Release\WinUI3Apps
```

This keeps the runner launch path and installer harvest path unchanged:

- installed executable: `[INSTALLFOLDER]\WinUI3Apps\PowerToys.Settings.exe`
- installed settings assets: `[INSTALLFOLDER]\WinUI3Apps\Assets\Settings\...`
- installed WinAppSDK/runtime files: still under `[INSTALLFOLDER]\WinUI3Apps\...`

The WiX responsibilities remain unchanged:

- `WinUI3Applications.wxs` packages the flattened `WinUI3Apps` executable/runtime tree.
- `Settings.wxs` packages `Assets\Settings` and the Settings helper scripts.

## How Settings Was Made AOT-Compatible

The current branch uses a combination of project-level and code-level changes:

1. AOT is enabled in [`src/settings-ui/Settings.UI/PowerToys.Settings.csproj`](/C:/PowerToys/src/settings-ui/Settings.UI/PowerToys.Settings.csproj) through the `EnableSettingsAOT` and `PublishAot` properties.
2. JSON serialization paths were moved to source-generated contexts in [`src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs`](/C:/PowerToys/src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs) and related callers.
3. Reflection-heavy code paths were replaced with compile-time type selection in places such as [`src/settings-ui/Settings.UI/Services/SearchIndexService.cs`](/C:/PowerToys/src/settings-ui/Settings.UI/Services/SearchIndexService.cs) and [`src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml.cs`](/C:/PowerToys/src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml.cs).
4. Command bindings were updated to use AOT-safe command types in [`src/settings-ui/Settings.UI.Library/ICommand.cs`](/C:/PowerToys/src/settings-ui/Settings.UI.Library/ICommand.cs) and related relay command implementations.
5. Some XAML interaction patterns were replaced with direct event handlers where the original behavior was not AOT-friendly.
6. A local deep-link helper is used for the standalone AOT path in [`src/settings-ui/Settings.UI/Helpers/SettingsDeepLink.cs`](/C:/PowerToys/src/settings-ui/Settings.UI/Helpers/SettingsDeepLink.cs), avoiding dependency on the WPF-based implementation for that flow.

## Known Notes

- Native AOT publish currently emits `WMC1510` XAML compiler warnings for bindings that may still need stronger trimming annotations or `x:Bind` migration.
- Some intermediate `CsWinRTInvokeGuidPatcher` steps may report exit code `-1` in project logs, but they did not block the successful publish observed on this branch.
- The successful publish depends on using the correct developer prompt architecture.
- The installer pipeline currently enables Settings AOT packaging only for `x64 / win-x64`.

## Validation Performed On This Branch

The following checks were completed on March 12, 2026:

- `msbuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /m`
- Standalone launch of the published AOT executable with dummy pipe arguments

Observed result:

- Publish succeeded with exit code `0`
- The standalone app started successfully and remained alive for at least 10 seconds during smoke validation
