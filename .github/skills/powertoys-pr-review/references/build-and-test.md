# Build & Test (Steps 3, 7, 7b)

Goal: `x64/Debug/PowerToys.exe` launches and the changed feature works end-to-end, so the user can just run the exe. Run all build commands from the worktree directory.

## Spectre override (required on machines without Spectre libs)

Set this **before** building:
```powershell
$env:POWERTOYS_DISABLE_SPECTRE = "1"
```
This works because `Cpp.Build.props` respects `<SpectreMitigation Condition="'$(SpectreMitigation)' == ''">Spectre</SpectreMitigation>` and `Directory.Build.props` sets it to false when the env var is set. If the worktree lacks these conditions, add them (LOCAL to the worktree only — do **not** commit or push them):

1. In `Cpp.Build.props`:
   ```xml
   <SpectreMitigation Condition="'$(SpectreMitigation)' == ''">Spectre</SpectreMitigation>
   ```
2. In `Directory.Build.props`, inside the first `<PropertyGroup>`:
   ```xml
   <SpectreMitigation Condition="'$(POWERTOYS_DISABLE_SPECTRE)' == '1'">false</SpectreMitigation>
   ```

## Step 3: Build locally (Debug)

Use the repo's standard build scripts — do **not** craft manual msbuild commands.

```powershell
$env:POWERTOYS_DISABLE_SPECTRE = "1"
cd <worktree-path>
# 1. Restore packages + build runner and settings
.\tools\build\build-essentials.cmd
# 2. Build the changed module(s)
cd src\modules\<ModuleName>\<ProjectFolder>
..\..\..\..\tools\build\build.cmd
```

Verify: exit code 0, `x64/Debug/PowerToys.exe` exists, and the changed module's output exists.

### Handling build failures

- **Spectre errors** (`MSB8040`): ensure `POWERTOYS_DISABLE_SPECTRE=1` is set and the `Cpp.Build.props` condition is in place.
- **NuGet missing**: run `build-essentials.cmd` first (it restores packages).
- **spdlog/fmt errors** (old submodule): the branch was not rebased on main — go back to Step 2b.
- **Compilation errors in the PR's changed files**: attempt to fix; if not easily fixable, stop and leave a comment on the fork PR.
- **Resource file missing** (e.g., a `.scr`): build the dependency project first (check the vcxproj for `ProjectReference` items).
- **LIB path errors** (`cannot open file 'libcpmtd.lib'`): the build scripts should handle this via env var; if not, apply the LIB fix (see the known-issues table).

## Step 7: Final build (full module chain)

`build-essentials.cmd` only builds the runner + settings. It does **not** build module interface DLLs or managed module executables. For end-to-end testing, build the full chain:

```powershell
$env:POWERTOYS_DISABLE_SPECTRE = "1"
cd <worktree-path>
# 1. Runner + settings + NuGet restore
.\tools\build\build-essentials.cmd
# 2. Native module interface DLL (the DLL the runner loads: PowerToys.<Module>.dll)
cd src\modules\<Module>\<NativeInterfaceProject>
..\..\..\..\tools\build\build.cmd
# 3. Managed module application, if separate (e.g. PowerToys.PowerLauncher.exe)
cd ..\<ManagedProject>
..\..\..\..\tools\build\build.cmd
# 4. The specific changed project/plugin
cd <ChangedProjectFolder>
..\..\..\..\tools\build\build.cmd
```

Common module interface projects:
- PowerToys Run: `src/modules/launcher/Microsoft.Launcher/` → `PowerToys.Launcher.dll`
- FancyZones: `src/modules/fancyzones/FancyZonesModuleInterface/` → `PowerToys.FancyZonesModuleInterface.dll`
- Keyboard Manager: `src/modules/keyboardmanager/KeyboardManagerModule/` → `PowerToys.KeyboardManager.dll`
- Color Picker: `src/modules/colorPicker/ColorPickerModuleInterface/` → `PowerToys.ColorPicker.dll`
- ZoomIt: `src/modules/ZoomIt/ZoomItModuleInterface/` → `PowerToys.ZoomItModuleInterface.dll`

To find it: look for a `.vcxproj` whose output the runner log references as "Failed to load X.dll".

**Why all four steps?** `build-essentials.cmd` only builds the runner shell and settings; the runner discovers modules by loading native interface DLLs; the managed app provides the actual UI/functionality; the plugin/changed-project is the code the PR modifies.

Verify: exit code 0 for all steps; `x64/Debug/PowerToys.exe` exists; the module interface DLL exists in `x64/Debug/`; and after launching, the runner log has no "Failed to load" line for the target module:
```powershell
Get-Content "$env:LOCALAPPDATA\Microsoft\PowerToys\RunnerLogs\runner-log_$(Get-Date -Format yyyy-MM-dd).log" | Select-String "<ModuleName>"
```

## Step 7b: End-to-end testing instructions

After a successful build, give the user a way to verify the change in under two minutes.

**Always include:**
1. **Launch command** — the exact path to `PowerToys.exe` in the worktree (e.g., `<worktree>\x64\Debug\PowerToys.exe`).
2. **How to access the feature** — what to click / shortcut to press / setting to change.
3. **What to look for** — specific UI elements, output text, or behaviors that confirm the change.
4. **Test scenarios** — 2–4 concrete steps exercising the new behavior, including edge cases addressed by the review fixes.

**Template:**
```
## End-to-End Testing
1. Launch: <worktree>\x64\Debug\PowerToys.exe
2. <How to trigger the feature>
3. <What to verify — expected output/behavior>
4. <Edge case from the review fixes, if applicable>
```

## Known environment issues and workarounds

| Issue | Symptom | Fix |
|-------|---------|-----|
| Spectre libs missing | `error MSB8040: Spectre-mitigated libraries are required` | Set `$env:POWERTOYS_DISABLE_SPECTRE = "1"` + ensure the `Cpp.Build.props` condition |
| LIB env includes spectre paths | `LINK : fatal error LNK1104: cannot open file 'libcpmtd.lib'` | `$env:LIB = $env:LIB -replace 'spectre\\x64','x64'` after VS env init |
| PR based on old main | spdlog/fmt compilation errors, missing vcpkg deps | Rebase onto `origin/main` first (Step 2b) |
| Force-push blocked by repo rules | `remote rejected: Cannot force-push` | Push to a new branch name (e.g., `pr-iterate/N-v2`) |
| Resource file missing (e.g., `.scr`) | `error RC2135: file not found` | Build the dependency project first (check `ProjectReference` in the vcxproj) |
| NuGet packages missing | `This project references NuGet package(s) that are missing` | Run `build-essentials.cmd` first |

## Fallback: manual msbuild (only if the scripts fail)

```cmd
call "C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat" -no_logo >nul 2>&1
cd /d <worktree-path>
set LIB=%LIB:spectre\x64=x64%
set POWERTOYS_DISABLE_SPECTRE=1
msbuild <project.vcxproj> /p:Platform=x64 /p:Configuration=Debug /p:SpectreMitigation=false /p:SolutionDir=<worktree-path>\ /nologo /v:minimal
```

Build logs live next to the project being built: `build.<config>.<platform>.errors.log` (check first) and `build.<config>.<platform>.all.log`.

## PowerToys style reference

- C#: `src/.editorconfig`, StyleCop.Analyzers
- C++: `src/.clang-format`
- XAML: XamlStyler
- Atomic changes, no drive-by refactors
- No noisy logs in hot paths
