# Advanced Paste

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/advanced-paste)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Advanced%20Paste%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Advanced%20Paste%22%20label%3AIssue-Bug)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Advanced+Paste%22)

## Overview

Advanced Paste is a PowerToys module that provides enhanced clipboard pasting with formatting options and additional functionality.

## Implementation Details

[Source code](/src/modules/AdvancedPaste)

TODO: Add implementation details

### Paste with AI Preview

The "Show preview" setting (`ShowCustomPreview`) controls whether AI-generated results are displayed in a preview window before pasting. **The preview feature does not consume additional AI credits**—the preview displays the same AI response that was already generated, cached locally from a single API call.

The implementation flow:
1. User initiates "Paste with AI" action
2. A single AI API call is made via `ExecutePasteFormatAsync`
3. The result is cached in `GeneratedResponses`
4. If preview is enabled, the cached result is displayed in the preview UI
5. User can paste the cached result without any additional API calls

See the `ExecutePasteFormatAsync(PasteFormat, PasteActionSource)` method in `OptionsViewModel.cs` for the implementation.

## Debugging

Advanced Paste is packaged as a self-contained MSIX with its own identity (`Microsoft.PowerToys.AdvancedPaste`). This gives it native package identity for Windows AI APIs (Phi Silica) and clean `ms-appx:///` resource resolution without workarounds.

The MSIX is output to `WinUI3Apps/AdvancedPaste/` and registered by the module interface at runtime (or by the installer on release builds).

### Running and attaching the debugger

1. Set the **Runner** project (`src/runner`) as the startup project in Visual Studio.
2. Launch the Runner (F5). This starts the PowerToys tray icon and loads all module interfaces.
3. Open Settings (right-click tray icon → Settings) and enable the **Advanced Paste** module if it isn't already. The module launches `PowerToys.AdvancedPaste.exe` in the background immediately.
4. In Visual Studio, go to **Debug → Attach to Process** (`Ctrl+Alt+P`) and attach to `PowerToys.AdvancedPaste.exe` (select **Managed (.NET Core)** debugger).

Alternatively, use the VS Code launch configuration **"Run AdvancedPaste"** from [.vscode/launch.json](/.vscode/launch.json) to launch the exe directly — but note that without the Runner, IPC and hotkeys won't work.

### MSIX package identity

Advanced Paste uses the Windows AI APIs (Phi Silica / `Microsoft.Windows.AI.Text.LanguageModel`) which require **package identity** at runtime. The app is packaged as a self-contained MSIX (following the same pattern as Command Palette).

#### How it works

- **Build**: The csproj has `EnableMsixTooling=true` and `GenerateAppxPackageOnBuild=true` (CI builds). This produces an MSIX in `AppPackages/`.
- **Local dev**: VS registers the package automatically via `Add-AppxPackage -Register AppxManifest.xml` when you build.
- **Installer**: The WiX installer deploys the MSIX file and a custom action (`InstallAdvancedPastePackageCA`) registers it via `PackageManager`.

#### Local development setup

For local debug builds, Visual Studio handles MSIX registration automatically. Select the **"PowerToys.AdvancedPaste (Package)"** launch profile in the debug dropdown.

To manually register:
```powershell
Add-AppxPackage -Register "ARM64\Debug\WinUI3Apps\AdvancedPaste\AppxManifest.xml"
```

Verify:
```powershell
$pkg = Get-AppxPackage -Name "*AdvancedPaste*"
$pkg.Name               # Microsoft.PowerToys.AdvancedPaste.Dev
$pkg.IsDevelopmentMode   # True
```

### How Settings UI checks Phi Silica availability

Settings UI does not have MSIX package identity. To check whether Phi Silica is available, it queries the running Advanced Paste process via a named pipe (`powertoys_advancedpaste_phi_status`).

Advanced Paste checks LAF + `GetReadyState()` once on startup (with MSIX identity), caches the result, and serves it to any client that connects. Settings connects with a 5-second timeout and reads one of:
- `Available` — model is ready
- `NotReady` — model needs download via Windows Update
- `NotSupported` — not a Copilot+ PC, API unavailable, or Advanced Paste not running

## Settings

| Setting | Description |
|---------|-------------|
| `ShowCustomPreview` | When enabled, shows AI-generated results in a preview window before pasting. Does not affect AI credit consumption. |

## Future Improvements

TODO: Add potential future improvements
