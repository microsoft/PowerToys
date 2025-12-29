# Publication Setup

This folder contains tools to help you prepare your CmdPal extension for publication to the Microsoft Store and WinGet.

## Files and Folders in this Directory

### Scripts

- **`one-time-store-publishing-setup.ps1`** - Configure your project for Microsoft Store publishing (run once)
- **`build-msix-bundles.ps1`** - Build MSIX packages and create bundles for Store submission
- **`one-time-winget-publishing-setup.ps1`** - Configure your project for WinGet publishing (run once)

### Resource Folders

- **`microsoft-store-resources/`** - Contains files used for Microsoft Store publishing:
  - `bundle_mapping.txt` - Auto-generated file that maps MSIX files for bundle creation

- **`winget-resources/`** - Contains templates and scripts for WinGet publishing:
  - `build-exe.ps1` - Script to build standalone EXE installer
  - `setup-template.iss` - Inno Setup installer template
  - `release-extension.yml` - GitHub Actions workflow template (moved to `.github/workflows/` during setup)
  - `Backups/` - Backup copies of configuration files (created during setup)

## Microsoft Store Quick Start

1. Open PowerShell and navigate to the Publication folder:

   ```powershell
   cd <YourProject>\Publication
   ```

2. Run the one-time setup script:

   ```powershell
   .\one-time-store-publishing-setup.ps1
   ```

3. Follow the prompts to enter your Microsoft Store information from Partner Center:
   - Package Identity Name
   - Publisher Certificate
   - Display Name
   - Publisher Display Name

   The script will update your `Package.appxmanifest` with Store-specific values.

4. Once configured, build your bundle:

   ```powershell
   .\build-msix-bundles.ps1
   ```

   This script will:
   - Build x64 and ARM64 MSIX packages
   - Automatically update `microsoft-store-resources\bundle_mapping.txt` with correct paths
   - Create a combined MSIX bundle
   - Display the bundle location when complete

5. Upload the resulting `.msixbundle` file from `microsoft-store-resources\` to Partner Center

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
- `microsoft-store-resources\bundle_mapping.txt` paths are correct (auto-updated by script)
- No file locks on the MSIX files

## WinGet Quick Start

1. Open PowerShell and navigate to the Publication folder:

   ```powershell
   cd <YourProject>\Publication
   ```

2. Run the one-time setup script:

   ```powershell
   .\one-time-winget-publishing-setup.ps1
   ```

3. Follow the prompts to enter:
   - GitHub Repository URL (where releases will be published)
   - Developer/Publisher Name

   The script will:
   - Configure `winget-resources\build-exe.ps1` with your extension details
   - Configure `winget-resources\setup-template.iss` with your extension information
   - Move `release-extension.yml` to `.github\workflows\` in your repository root

4. Commit and push changes to GitHub:

   ```powershell
   git add .
   git commit -m "Configure extension for WinGet publishing"
   git push
   ```

5. Trigger the GitHub Action to build and release:

   ```powershell
   gh workflow run release-extension.yml --ref main -f "release_notes=**First Release of <ExtensionName> Extension for Command Palette**
   
   The inaugural release of the <ExtensionName> for Command Palette..."
   ```

   Or create a release manually through the GitHub web interface.

## Additional Resources

- [Command Palette Extension Publishing Documentation](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension)
- [Microsoft Store Publishing Guide](https://learn.microsoft.com/windows/apps/publish/)
