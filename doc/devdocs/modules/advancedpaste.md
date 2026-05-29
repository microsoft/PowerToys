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

Advanced Paste is an unpackaged, self-contained WinUI 3 app (`PowerToys.AdvancedPaste.exe`). To call Windows AI APIs (Phi Silica / `Microsoft.Windows.AI.Text.LanguageModel`) it acquires **package identity** at runtime via a shared sparse MSIX package (`Microsoft.PowerToys.SparseApp`).

### Running and attaching the debugger

1. Set the **Runner** project (`src/runner`) as the startup project in Visual Studio.
2. Launch the Runner (F5). This starts the PowerToys tray icon and loads all module interfaces.
3. Open Settings (right-click tray icon → Settings) and enable the **Advanced Paste** module if it isn't already. The module launches `PowerToys.AdvancedPaste.exe` in the background immediately.
4. In Visual Studio, go to **Debug → Attach to Process** (`Ctrl+Alt+P`) and attach to `PowerToys.AdvancedPaste.exe` (select **Managed (.NET Core)** debugger).

Alternatively, use the VS Code launch configuration **"Run AdvancedPaste"** from [.vscode/launch.json](/.vscode/launch.json) to launch the exe directly — but note that without the Runner, IPC and hotkeys won't work.

### Sparse package identity (local development)

#### Why is this needed?

- The `LanguageModel` API requires a Limited Access Feature (LAF) unlock, which only succeeds when the calling process has a matching package identity.
- Advanced Paste is an unpackaged, self-contained WinUI 3 app. The sparse package grants it identity without converting it to a full MSIX.
- The csproj uses `<ProjectPriFileName>PowerToys.AdvancedPaste.pri</ProjectPriFileName>` (matching the convention of other WinUI3 apps like ImageResizer). This requires WindowsAppSDK Foundation >= 2.0.22 ([PR #6376](https://github.com/microsoft/WindowsAppSDK/pull/6376)) which fixes MRT PRI lookup under sparse identity so `Application.LoadComponent` resolves custom-named PRI files instead of hard-coding `resources.pri`.

#### One-step dev setup

```powershell
pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug -DevRegister
```

`-DevRegister`:
1. Generates a dev certificate under `src/PackageIdentity/.user/` (first run only).
2. Auto-imports that certificate into `CurrentUser\TrustedPeople` and `CurrentUser\Root` so the OS grants sparse identity to AP (without trust, `GetPackageFamilyName` returns `APPMODEL_ERROR_NO_PACKAGE` and LAF unlock silently fails).
3. Removes any prior registration.
4. Rewrites the publisher in a temp copy of `AppxManifest.xml` to match the dev cert subject.
5. Registers via `Add-AppxPackage -Register … -ExternalLocation X:\…\<Platform>\<Config>\WinUI3Apps`.

After registration verify:

```powershell
$pkg = Get-AppxPackage -Name '*SparseApp*'
$pkg.PackageFamilyName    # Microsoft.PowerToys.SparseApp_djwsxzxb4ksa8
$pkg.PublisherId          # djwsxzxb4ksa8
$pkg.IsDevelopmentMode    # True
```

Confirm AP picks up sparse identity at runtime:

```powershell
& 'ARM64\Debug\WinUI3Apps\PowerToys.AdvancedPaste.exe' --check-phi-silica
# Exit 0 = Available, 1 = NotReady, 2 = NotSupported
```

Re-register after rebuilding AP, changing `src/PackageIdentity/AppxManifest.xml`, or switching platforms/configurations by re-running the same command. Unregister with `-Unregister`.

#### Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `GetPackageFamilyName` returns `APPMODEL_ERROR_NO_PACKAGE` (15700) at runtime; LAF unlock returns `Unavailable` | Dev certificate not trusted (or sparse package not registered) | Re-run `BuildSparsePackage.ps1 -DevRegister` — auto-imports the cert into `TrustedPeople` and `Root`. |
| `Microsoft.UI.Xaml.dll` crash with `0xC000027B` / `REGDB_E_CLASSNOTREG` on AP or Settings startup | `<Application>` `Executable` path in `src/PackageIdentity/AppxManifest.xml` does not resolve under the registered `ExternalLocation` (`<Config>\WinUI3Apps\`) | Confirm every `Executable` is relative to `WinUI3Apps\` (per #47177) and the file exists under the build output. |
| AP launches but never shows a window when triggered via hotkey | Runner's pipe-server wait timed out before AP's cold-start finished bootstrapping WinAppSDK + DI host | Already mitigated by the 15 s pipe timeout in `AdvancedPasteProcessManager.cpp`; warm-start launches connect in well under 1 s. |
| `XamlParseException` / `ms-appx:///Microsoft.UI.Xaml/Themes/…` not found | WindowsAppSDK Foundation < 2.0.22; MRT can't resolve custom PRI name under sparse identity | Ensure `Microsoft.WindowsAppSDK.Foundation` >= 2.0.22 in `Directory.Packages.props`. |

### How Settings UI checks Phi Silica availability

Settings UI does not have sparse package identity. To check whether Phi Silica is available, it launches Advanced Paste as a short-lived subprocess:

```
PowerToys.AdvancedPaste.exe --check-phi-silica
```

`Program.Main` recognizes this flag, calls `PhiSilicaLafHelper.TryUnlock()` + `LanguageModel.GetReadyState()`, prints one of `Available` / `NotReady` / `NotSupported` to stdout, and exits with the matching code (0/1/2). Settings reads stdout with a 10 s wait. Because each call is a fresh process, transient `Unavailable` results are not cached across checks.

### See also

- [`src/PackageIdentity/readme.md`](/src/PackageIdentity/readme.md) — full sparse package documentation
- [microsoft/microsoft-ui-xaml#10856](https://github.com/microsoft/microsoft-ui-xaml/issues/10856) — original WinUI sparse-identity PRI bug
- [microsoft/WindowsAppSDK#6376](https://github.com/microsoft/WindowsAppSDK/pull/6376) — MRT sparse PRI fix (Foundation >= 2.0.22)

## Settings

| Setting | Description |
|---------|-------------|
| `ShowCustomPreview` | When enabled, shows AI-generated results in a preview window before pasting. Does not affect AI credit consumption. |

## Future Improvements

TODO: Add potential future improvements
