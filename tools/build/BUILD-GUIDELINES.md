<<<<<<< HEAD
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
   - Restores NuGet for `PowerToys.sln` and builds essentials (runner, settings).
   - Auto-detects Platform; initializes VS Dev environment automatically.
   - Example (PowerShell):
     - `./tools/build/build-essentials.ps1`
     - `./tools/build/build-essentials.ps1 -Platform arm64 -Configuration Release`

2) `build.ps1` (from any folder)
   - Builds any `.sln/.csproj/.vcxproj` in the current directory.
   - Auto-detects Platform; initializes VS Dev environment automatically.
   - Accepts extra MSBuild args positionally (forwarded to msbuild):
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
=======
# Build scripts - quick guideline

As the result of our recent changes, use the following guidance when working in the PowerToys repo:

1. Use `build-essentials.ps1` before any development in general
   - Purpose: restore NuGet packages for the full solution and build a small set of essential native projects (runner, settings). This is a fast way to ensure native artifacts required for local development are available.

2. Use `build.ps1` from any folder
   - Purpose: lightweight local builder. It auto-discovers the target platform (x64/arm64/x86) and builds projects it finds in the current directory.
   - Notes: you can pass additional MSBuild arguments (e.g. `./tools/build/build.ps1 '/p:CIBuild=true'`) — the script will forward them to MSBuild.
   - Use `-RestoreOnly` to only restore packages for local projects.

3. Use `build-installer.ps1` to create a local installer (use with caution)
   - Purpose: runs the full pipeline that restores, builds the full solution, signs packages, and builds the installer (MSI/bootstrapper).
   - Caution: this script performs cleaning (git clean) and installer packaging steps that may remove untracked files under `installer/`.

Additional notes
- Shared helpers live in `build-common.ps1` and are used by the other scripts (`RunMSBuild`, `RestoreThenBuild`, `BuildProjectsInDirectory`, platform auto-detection).
- If you want a different default platform selection, set the `-Platform` parameter explicitly when invoking the scripts.

If you want, I can add this guidance to the repository README instead or add a short one-liner comment to the top of `build-common.ps1` so tools can discover it automatically.
>>>>>>> main
