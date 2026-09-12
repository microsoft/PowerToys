---
description: "Command Palette guidance for fast incremental builds, x64/ARM64 selection, loose package deployment, testing, launching, and debugging."
---

# Command Palette Build And Deploy

## Scope And Defaults

- Treat `src/modules/cmdpal` as an independently buildable product. Never build `PowerToys.slnx` for CmdPal work.
- Do not build either CmdPal solution filter by default. Build the narrowest owning project or test project directly so MSBuild traverses only its required `ProjectReference` graph.
- For a runnable CmdPal app, build `src/modules/cmdpal/Microsoft.CmdPal.UI/Microsoft.CmdPal.UI.csproj` directly. Do not build every project or test in `CommandPalette.slnf`.
- Use `Build`, not `Rebuild` or `Clean`. Preserve incremental outputs. Clean only when a demonstrated stale-output problem requires it or the user explicitly asks.
- Do not add `-restore` on every build. Restore only when assets are missing or package references changed.
- Use Visual Studio MSBuild, not `dotnet build`, because the project graph contains native `.vcxproj` dependencies.

## Architecture

- Never assume x64. Before the first build in a session, determine the native OS architecture:

  ```powershell
  [Runtime.InteropServices.RuntimeInformation]::OSArchitecture
  ```

- Map `Arm64` to `Platform=ARM64` and `RuntimeIdentifier=win-arm64`; map `X64` to `Platform=x64` and `RuntimeIdentifier=win-x64`.
- Prefer the matching native Visual Studio MSBuild host (`MSBuild\Current\Bin\arm64\MSBuild.exe` on ARM64, `MSBuild\Current\Bin\amd64\MSBuild.exe` on x64), discovered through `vswhere.exe`. Falling back to another installed MSBuild host is acceptable only when the native host is unavailable; the target `Platform` must still match the OS unless the user explicitly requests cross-compilation.
- Reuse the chosen `Configuration`, `Platform`, and `RuntimeIdentifier` throughout the session. Default to `Debug` unless the task requires another configuration.

## Fast Incremental Build

The normal app build is equivalent to:

```powershell
& $msbuild src\modules\cmdpal\Microsoft.CmdPal.UI\Microsoft.CmdPal.UI.csproj `
    -nologo -m -t:Build `
    -p:Configuration=$configuration `
    -p:Platform=$platform `
    -p:RuntimeIdentifier=$runtimeIdentifier `
    -p:CIBuild=false `
    -p:GeneratePackageLocally=false `
    -p:GenerateAppxPackageOnBuild=false `
    -p:PublishAppxPackage=false
```

- Run a focused test project directly after localized changes. Build and deploy the UI project only when the user needs the runnable app or the change affects app integration.
- Never set `CIBuild=true` or `GeneratePackageLocally=true` for normal iteration; either causes `Microsoft.CmdPal.UI.csproj` to generate a full package.

## Deploy For Start Menu Launch

- A build alone is not deployment. After a successful app build, register the generated loose manifest so CmdPal behaves like Visual Studio's **Deploy** command and remains launchable from the Start menu:

  ```powershell
  $manifest = Join-Path $repoRoot "$platform\$configuration\WinUI3Apps\CmdPal\AppxManifest.xml"
  Add-AppxPackage -Register $manifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion
  ```

- Registration does not build; always build first.
- This is a loose development package. Do not create or install an `.msix`, run packaging/publish targets, install signing certificates, or use the PowerToys sparse-package workflow for normal CmdPal iteration.
- Do not manually uninstall the existing CmdPal development package before routine deployment. Re-register it in place. Only investigate removal when registration reports a concrete identity or package conflict.
- Do not run `Microsoft.CmdPal.UI.exe` directly as an unpackaged app. After registration, leave it available through its normal Start menu entry unless the user explicitly asks to launch it.
- Do not use `msbuild ... -t:Deploy`; single-project MSIX deployment is supplied by Visual Studio's project system, not a callable target in this project. At the command line, `Add-AppxPackage -Register` is the deployment step.

## Escalation

- Full `CommandPalette.slnf` builds are for explicit broad validation, not the inner loop.
- Full MSIX/package generation is for explicit packaging or release work only.
- Ask before cleaning outputs, uninstalling packages, changing certificates, or building a different architecture.