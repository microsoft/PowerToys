# Publication Setup

This folder contains tools to help you prepare your CmdPal extension for publication to the Microsoft Store.

## Files in this Folder

- **`one-time-store-pubishing-setup.ps1`** - Configure your project for Microsoft Store publishing
- **`build-msix-bundles.ps1`** - Automated script to build MSIX packages and create bundles
- **`bundle_mapping.txt`** - Auto-generated mapping file for MSIX bundle creation (updated by build script)

## Quick Start

1. Open PowerShell and navigate to the Publication folder:

   ```powershell
   cd <YourProject>\Publication
   ```

2. Run the one-time setup script:

   ```powershell
   .\one-time-store-pubishing-setup.ps1
   ```

3. Follow the prompts to enter your Microsoft Store information from Partner Center:
   - Package Identity Name
   - Publisher Certificate
   - Display Name
   - Publisher Display Name

4. Once configured, build your bundle:

   ```powershell
   .\build-msix-bundles.ps1
   ```

    - Build x64 and ARM64 MSIX packages
    - Automatically update `bundle_mapping.txt` with the correct paths
    - Create a combined MSIX bundle
    - Display the bundle location when complete

5. Upload the resulting `.msixbundle` file to Partner Center

## Troubleshooting

### makeappx.exe not found

The build script requires the Windows SDK. Install it via:

- Visual Studio Installer (Individual Components → Windows SDK)
- [Standalone Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/)

### Build errors

Ensure you have:

- .NET 9.0 SDK installed
- Windows SDK 10.0.26100.0 or compatible version
- No other instances of Visual Studio building the project

### Bundle creation fails

Check that:

- Both x64 and ARM64 builds completed successfully
- `bundle_mapping.txt` paths are correct (auto-updated by script)
- No file locks on the MSIX files

## Additional Resources

- [Command Palette Extension Publishing Documentation](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension)
- [Microsoft Store Publishing Guide](https://learn.microsoft.com/windows/apps/publish/)
