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

Advanced Paste outputs to its own subfolder (`WinUI3Apps/AdvancedPaste/`) rather than the shared `WinUI3Apps/` folder. This isolates its `resources.pri` from other WinUI 3 apps due to a [known WinUI bug](https://github.com/microsoft/microsoft-ui-xaml/issues/10856).

### Running and attaching the debugger

1. Set the **Runner** project (`src/runner`) as the startup project in Visual Studio.
2. Launch the Runner (F5). This starts the PowerToys tray icon and loads all module interfaces.
3. Open Settings (right-click tray icon → Settings) and enable the **Advanced Paste** module if it isn't already. The module launches `PowerToys.AdvancedPaste.exe` in the background immediately.
4. In Visual Studio, go to **Debug → Attach to Process** (`Ctrl+Alt+P`) and attach to `PowerToys.AdvancedPaste.exe` (select **Managed (.NET Core)** debugger).

Alternatively, use the VS Code launch configuration **"Run AdvancedPaste"** from [.vscode/launch.json](/.vscode/launch.json) to launch the exe directly — but note that without the Runner, IPC and hotkeys won't work.

### Sparse package identity (local development)

Advanced Paste uses the Windows AI APIs (Phi Silica / `Microsoft.Windows.AI.Text.LanguageModel`) which require **package identity** at runtime. PowerToys provides this via a shared sparse MSIX package (`Microsoft.PowerToys.SparseApp`).

#### Why is this needed?

- The `LanguageModel` API requires a Limited Access Feature (LAF) unlock, which only succeeds when the calling process has a matching package identity.
- Advanced Paste is an unpackaged, self-contained WinUI 3 app. The sparse package grants it identity without converting it to a full MSIX.
- There is a [known WinUI bug](https://github.com/microsoft/microsoft-ui-xaml/issues/10856) where self-contained WinUI 3 apps with sparse identity only load `resources.pri` instead of module-specific PRI files. To avoid conflicts with other WinUI apps, Advanced Paste outputs to its own subfolder (`WinUI3Apps/AdvancedPaste/`) and uses `resources.pri` as its PRI filename.

#### One-time setup

1. **Build the sparse package** for your platform and configuration:

   ```powershell
   pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug
   ```

   This generates `PowerToysSparse.msix` in `ARM64\Debug\`, creates a dev certificate, and signs the package.

2. **Trust the dev certificate** (first time only):

   ```powershell
   Import-Certificate -FilePath "src/PackageIdentity/.user/PowerToysSparse.certificate.sample.cer" -CertStoreLocation Cert:\CurrentUser\TrustedPeople
   ```

3. **Register the sparse package for development** by adding `-DevRegister` to the build command:

   ```powershell
   pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug -DevRegister
   ```

   The `-DevRegister` flag automatically:
   - Removes any existing registration
   - Creates a temporary copy of `AppxManifest.xml` with the dev publisher
   - Registers it via `Add-AppxPackage -Register` with `-ExternalLocation` pointing to your build output
   - Verifies the result

   You can combine it with the initial build (step 1) in a single command.

4. **Verify the registration:**

   ```powershell
   $pkg = Get-AppxPackage -Name "*SparseApp*"
   $pkg.Publisher           # Should be: CN=PowerToys Dev, O=PowerToys, L=Redmond, S=Washington, C=US
   $pkg.PublisherId         # Should be: djwsxzxb4ksa8
   $pkg.IsDevelopmentMode   # Should be: True
   ```

#### Re-registration

Re-register after rebuilding the sparse package, changing `AppxManifest.xml`, or switching platforms/configurations:

```powershell
pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug -DevRegister
```

#### Unregistering

```powershell
pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Unregister
```

#### Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `Cannot locate resource from 'ms-appx:///Microsoft.UI.Xaml/Themes/themeresources.xaml'` | Sparse package not registered, or registered without `-ExternalLocation` | Re-register using step 3 above |
| `IsDevelopmentMode` is `False` after registration | Used `Add-AppxPackage -Path *.msix` instead of `-Register` | Remove and re-register using the `-Register` form |
| LAF unlock returns `Unavailable` | Publisher mismatch | Verify `$pkg.PublisherId` is `djwsxzxb4ksa8` |
| `HRESULT 0x800B0109` (trust failure) | Dev certificate not trusted | Run `Import-Certificate` (step 2) for both `TrustedPeople` and `TrustedRoot` |

### How Settings UI checks Phi Silica availability

Settings UI does not have sparse package identity. To check whether Phi Silica is available, it launches Advanced Paste as a subprocess:

```
PowerToys.AdvancedPaste.exe --check-phi-silica
```

This flag skips the WinUI app and outputs one of:
- `Available` (exit code 0) — model is ready
- `NotReady` (exit code 1) — model needs download via Windows Update
- `NotSupported` (exit code 2) — not a Copilot+ PC or API unavailable

### See also

- [`src/PackageIdentity/readme.md`](/src/PackageIdentity/readme.md) — full sparse package documentation
- [microsoft/microsoft-ui-xaml#10856](https://github.com/microsoft/microsoft-ui-xaml/issues/10856) — the WinUI bug requiring separate output folder

## Settings

| Setting | Description |
|---------|-------------|
| `ShowCustomPreview` | When enabled, shows AI-generated results in a preview window before pasting. Does not affect AI credit consumption. |

## Future Improvements

TODO: Add potential future improvements
