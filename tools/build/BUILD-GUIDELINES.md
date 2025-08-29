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
