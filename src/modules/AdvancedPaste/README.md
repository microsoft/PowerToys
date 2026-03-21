# Advanced Paste

## Sparse package identity (local development)

Advanced Paste uses the Windows AI APIs (Phi Silica / `Microsoft.Windows.AI.Text.LanguageModel`) which require **package identity** at runtime. PowerToys provides this via a shared sparse MSIX package (`Microsoft.PowerToys.SparseApp`).

### Why is this needed?

- The `LanguageModel` API requires a Limited Access Feature (LAF) unlock, which only succeeds when the calling process has a matching package identity.
- Advanced Paste is an unpackaged, self-contained WinUI 3 app. The sparse package grants it identity without converting it to a full MSIX.
- There is a [known WinUI bug](https://github.com/microsoft/microsoft-ui-xaml/issues/10856) where self-contained WinUI 3 apps with sparse identity only load `resources.pri` instead of module-specific PRI files. As a workaround, `Program.cs` copies `PowerToys.AdvancedPaste.pri` to `resources.pri` at startup before WinUI initializes. This avoids polluting other apps that share the same output folder.

### One-time setup

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

### Re-registration

Re-register after:
- Rebuilding the sparse package
- Changing `AppxManifest.xml` (adding/removing applications)
- Switching between platforms or configurations

```powershell
pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug -DevRegister
```

The `-DevRegister` flag removes the old registration automatically before re-registering.

### Unregistering

```powershell
pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Unregister
```

### Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `Cannot locate resource from 'ms-appx:///Microsoft.UI.Xaml/Themes/themeresources.xaml'` | Sparse package not registered, or registered without `-ExternalLocation` | Re-register using `-Register AppxManifest.xml -ExternalLocation` (see step 3) |
| `IsDevelopmentMode` is `False` after registration | Used `Add-AppxPackage -Path *.msix` instead of `-Register` | Remove and re-register using the `-Register` form |
| LAF unlock returns `Unavailable` | Publisher mismatch — the registered package publisher doesn't match the LAF attestation | Verify `$pkg.PublisherId` is `djwsxzxb4ksa8` |
| `HRESULT 0x800B0109` (trust failure) | Dev certificate not trusted | Run `Import-Certificate` (step 2) for both `TrustedPeople` and `TrustedRoot` |
| Multiple WinUI apps crash with theme errors | All WinUI apps with sparse identity share the `resources.pri` bug | Only one app at a time can own `resources.pri` in the shared `WinUI3Apps` folder |

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

- [`src/PackageIdentity/readme.md`](../../PackageIdentity/readme.md) — full sparse package documentation
- [microsoft/microsoft-ui-xaml#10856](https://github.com/microsoft/microsoft-ui-xaml/issues/10856) — the WinUI bug requiring `resources.pri` workaround
