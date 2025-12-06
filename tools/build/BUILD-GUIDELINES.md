# Build scripts – quick guideline

Use these scripts to build PowerToys locally. They auto-detect your platform (x64/arm64), initialize the Visual Studio developer environment, and write helpful logs on failure.

## Quick start (from cmd.exe)
- Fast essentials (runner + settings) and NuGet restore first:
  - `tools\build\build-essentials.cmd`
- Build projects in the current folder:
  - `tools\build\build.cmd`

Tip: Add `D:\PowerToys\tools\build` to your PATH to use the wrappers anywhere.

## When to use which
1) `build-essentials.ps1`
   - Restores NuGet for `PowerToys.slnx` and builds essentials (runner, settings).
   - Auto-detects Platform; initializes VS Dev environment automatically.
   - Example (PowerShell):
     - `./tools/build/build-essentials.ps1`
     - `./tools/build/build-essentials.ps1 -Platform arm64 -Configuration Release`

2) `build.ps1` (from any folder)
   - Builds any `.sln/.csproj/.vcxproj` in the current directory.
   - Auto-detects Platform; initializes VS Dev environment automatically.
   - Accepts extra MSBuild args (forwarded to msbuild):
     - `./tools/build/build.ps1 '/p:CIBuild=true' '/p:SomeProp=Value'`
   - Restore only:
     - `./tools/build/build.ps1 -RestoreOnly`

3) `build-installer.ps1` (use with caution)
   - Full local packaging pipeline (restore, build, sign MSIX, WiX v5 MSI/bootstrapper).
   - Auto-inits VS Dev environment. Cleans some output (keeps *.exe) under `installer/`.
   - Key options: `-PerUser true|false`, `-InstallerSuffix wix5|vnext`.
   - Example:
     - `./tools/build/build-installer.ps1 -Platform x64 -Configuration Release -PerUser true -InstallerSuffix wix5`

## Logs and troubleshooting
- On failure, see logs next to the solution/project being built:
  - `build.<configuration>.<platform>.all.log` — full text log
  - `build.<configuration>.<platform>.errors.log` — errors only
  - `build.<configuration>.<platform>.warnings.log` — warnings only
  - `build.<configuration>.<platform>.trace.binlog` — open with MSBuild Structured Log Viewer
- VS environment init:
  - Scripts try DevShell first (`Microsoft.VisualStudio.DevShell.dll` / `Enter-VsDevShell`), then fall back to `VsDevCmd.bat`.
  - If VS isn’t found, run from “Developer PowerShell for VS 2022”, or ensure `vswhere.exe` exists under `Program Files (x86)\Microsoft Visual Studio\Installer`.

## Notes
- Override platform explicitly with `-Platform x64|arm64` if needed.
- CMD wrappers: `build.cmd`, `build-essentials.cmd` forward all arguments to the PowerShell scripts.
