## Developing PowerToys with Visual Studio Code

This guide shows how to build, debug, and contribute to PowerToys using VS Code instead of (or alongside) full Visual Studio. It focuses on common inner‑loop tasks for C++, .NET, and mixed scenarios present in the solution.

> PowerToys is a large mixed C++ / C# / WinAppSDK solution. VS Code works well for incremental development and quick module iterations, but occasionally you may still prefer full Visual Studio for designer tooling or specialized diagnostics.

---
VS Code extensions Needed:

| Area | Extension | Notes |
|------|-----------|-------|
| C++ | ms-vscode.cpptools | IntelliSense, debugging (cppvsdbg) |
| C# | ms-dotnettools.csdevkit (or C#) | Language service / test explorer |

---

## Building in VS Code
### Configure developer powershell for vs2022 for more convenient dev in vscode.
1. Configure profile in in settings, entry:  "terminal.integrated.profiles.windows"
2. Add below config as entry:
```json
    "Developer PowerShell for VS 2022": {
		// Configure based on your preference
        "path": "C:\\Program Files\\WindowsApps\\Microsoft.PowerShell_7.5.2.0_arm64__8wekyb3d8bbwe\\pwsh.exe",
        "args": [
            "-NoExit",
            "-Command",
            "& {",
            "$orig = Get-Location;",
            // Configure based on your environment
            "& 'C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\Common7\\Tools\\Launch-VsDevShell.ps1';",
            "Set-Location $orig",
            "}"
        ]
    },
```
3. [Optional] Set Developer PowerShell for VS 2022 as your default profile, so that you can get a deep integration with vscode coding agent. 

4. Now You can build with plain `msbuild` or configure tasks.json in below section
Or reach out to "tools\build\BUILD-GUIDELINES.md"

### Sample plain msbuild command
```powershell
# Restore:
msbuild powertoys.slnx -t:restore -p:configuration=debug -p:platform=x64 -m

# Build powertoys slnx
msbuild powertoys.slnx -p:configuration=debug -p:platform=x64 -m

# dotnet project
msbuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj -p:Platform=x64 -p:Configuration=Debug -m

# native project
msbuild "src\modules\MouseUtils\FindMyMouse\FindMyMouse.vcxproj" -p:Configuration=Debug -p:Platform=x64 -m
```

---

## Debugging

### Existing launch configuration

The repo provides `.vscode/launch.json` with:

- `Run PowerToys.exe (no build)`: Launches the already-built executable at `x64/Debug/PowerToys.exe` using `cppvsdbg`.

Build first, then press F5. To switch configuration (Release / ARM64) either edit the path or create additional launch entries.

### Attaching to a running instance

If PowerToys is already running, you can attach to that process:

2. VS Code command palette: “C/C++: (Windows) Attach to Process”.
3. Filter for `PowerToys.exe` / module-specific processes.

### Debugging managed components

Many modules have a managed component loaded into the PowerToys process. `cppvsdbg` can debug mixed mode, but if you need richer .NET inspection you can create a second configuration using `type: coreclr` and `processId` attachment after the native launch, or just attach separately:

Similar for attach to managed code.
> Note: In arm64 machine, can only debug arm64 code.

```jsonc
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Run native executable (no build)",
            "type": "cppvsdbg",
            "request": "launch",
            "program": "${workspaceFolder}\\x64\\Debug\\PowerToys.exe",
            "args": [],
            "stopAtEntry": false,
            "cwd": "${workspaceFolder}",
            "environment": [],
            "console": "integratedTerminal"
        },
        {
            "name": "C/C++ Attach to PowerToys Process (native)",
            "type": "cppvsdbg",
            "request": "attach",
            "processId": "${command:pickProcess}",
            "symbolSearchPath": "${workspaceFolder}\\x64\\Debug;${workspaceFolder}\\Debug;${workspaceFolder}\\symbols"
        },
        {
            "name": "Run managed code (managed, no build)",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}\\arm64\\Debug\\WinUI3Apps\\PowerToys.Settings.exe",
            "args": [],
            "cwd": "${workspaceFolder}",
            "env": {},
            "console": "internalConsole",
            "stopAtEntry": false
        }
    ]
}
```
---

## 6. Common tasks & tips

| Task | Command / Action | Notes |
|------|------------------|-------|
| Clean | `git clean -xdf` (careful) or `msbuild /t:Clean PowerToys.slnx` | Deep clean removes packages & build outputs |
| Rebuild single project | `msbuild path\to\proj.vcxproj /t:Rebuild -p:Platform=x64 -p:Configuration=Debug` | Faster than whole solution |
| Generate installer (rare in inner loop) | See `tools\build\build-installer.ps1` | Usually not needed for local debug |
| Resource conversion errors | Re-run restore + build | Triggers custom PowerShell targets |
