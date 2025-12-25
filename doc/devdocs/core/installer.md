# PowerToys Installer

## Installer Architecture (WiX 5)

- Uses a bootstrapper to check dependencies and close PowerToys
- MSI defined in product.wxs
- Custom actions in C++ for special operations:
  - Getting install folder
  - User impersonation
  - PowerShell module path retrieval
  - GPO checking
  - Process termination

### Installer Components

- Separate builds for machine-wide and user-scope installation
- Supports x64 and ARM64
- Custom actions DLL must be signed separately before installer build
- WXS files generated during build process for file components
- Localization handling for resource DLLs
- Firewall exceptions for certain modules

### MSI Installer Build Process

- First builds `PowerToysSetupCustomActionsVNext` DLL and signs it
- Then builds the installer without cleaning, to reuse the signed DLL
- Uses PowerShell scripts to modify .wxs files before build
- Restores original .wxs files after build completes
- Scripts (`applyBuildInfo.ps1` and `generateFileList.ps1`) dynamically update files list for installer
  - Helps manage all self-contained dependencies (.NET, WinAppSDK DLLs, etc.)
  - Avoids manual maintenance of file lists

### Special Build Processes

- .NET applications need publishing for correct WebView2 DLL inclusion
- WXS files backed up and regenerated during build
- Monaco UI components (JavaScript/HTML) generated during build
- Localization files downloaded from server during CI release builds

## Per-User vs Per-Machine Installation

- Functionality is identical
- Differences:
  - Per-User: 
    - Installed to `%LOCALAPPDATA%\PowerToys`
    - Registry entries in HKCU
    - Different users can have different installations/settings
  - Per-Machine:
    - Installed to `Program Files\PowerToys`
    - Registry entries in HKLM
    - Single installation shared by all users
- Default is now Per-User installation
- Guards prevent installing both types simultaneously

## MSIX Usage in PowerToys

- Context menu handlers for Windows 11 use sparse MSIX packages
- Previous attempts to create full MSIX installers were abandoned
- Command Palette will use MSIX when merged into PowerToys
- The main PowerToys application still uses MSI for installation

### MSIX Packaging and Extensions

- MSIX packages for extensions (like context menus) are included in the PowerToys installer
- The MSIX files are built as part of the PowerToys build process
- MSIX files are saved directly into the root folder with base application files
- The installer includes MSIX files but doesn't install them automatically
- Packages are registered when a module is enabled
- Code in `package.h` checks if a package is registered and verifies the version
- Packages will be installed if a version mismatch is detected
- When uninstalling PowerToys, the system checks for installed packages with matching display names and attempts to uninstall them

## GPO Files (Group Policy Objects)

- GPO files for x64 and ARM64 are identical
- Only one set is needed
- GPO files in pipeline are copies of files in source

## Installer Debugging

- Can only build installer in Release mode
- Typically debug using logs and message boxes
- Logs located in:
  - `%LOCALAPPDATA%\Temp\PowerToys_bootstrapper_*.log` - MSI tool logs
  - `%LOCALAPPDATA%\Temp\PowerToys_*.log` - Custom installer logs
- Logs in Bug Reports are useful for troubleshooting installation issues

### Building PowerToys Locally

#### One stop script for building installer
1. Open developer powershell for vs 2022
2. Run tools\build\build-installer.ps1
> For the first-time setup, please run the installer as an administrator. This ensures that the Wix tool can move wix.target to the desired location and trust the certificate used to sign the MSIX packages.

The following manual steps will not install the MSIX apps (such as Command Palette) on your local installer.

#### Prerequisites for building the MSI installer

PowerToys uses WiX v5 for creating installers. The WiX v5 tools are automatically installed during the build process via dotnet tool.

For manual installation of WiX v5 tools:
```powershell
dotnet tool install --global wix --version 5.0.2
```

> **Note:** As of release 0.94, PowerToys has migrated from WiX v3 to WiX v5. The WiX v3 toolset is no longer required.

#### Building prerequisite projects

##### From the command line

1. From the start menu, open a `Developer Command Prompt for VS 2022`
1. Ensure `nuget.exe` is in your `%path%`
1. In the repo root, run these commands:
  
```
nuget restore .\tools\BugReportTool\BugReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\BugReportTool\BugReportTool.sln

nuget restore .\tools\StylesReportTool\StylesReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\StylesReportTool\StylesReportTool.sln
```

##### From Visual Studio

If you prefer, you can alternatively build prerequisite projects for the installer using the Visual Studio UI.

1. Open `tools\BugReportTool\BugReportTool.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu, choose `Build Solution`.
1. Open `tools\StylesReportTool\StylesReportTool.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu, choose `Build Solution`.

#### Locally compiling the installer

1. Open `installer\PowerToysSetup.slnx`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu choose `Build Solution`.

The resulting installer will be available in the `installer\PowerToysSetupVNext\x64\Release\` folder.

To build the installer from the command line, run `Developer Command Prompt for VS 2022` in admin mode and execute the following commands. The generated installer package will be located at `\installer\PowerToysSetupVNext\{platform}\Release\MachineSetup`.

```
git clean -xfd  -e *exe -- .\installer\
MSBuild -t:restore  .\installer\PowerToysSetup.slnx -p:RestorePackagesConfig=true /p:Platform="x64" /p:Configuration=Release
MSBuild -t:Restore -m .\installer\PowerToysSetup.slnx /t:PowerToysInstallerVNext /p:Configuration=Release /p:Platform="x64"
MSBuild -t:Restore -m .\installer\PowerToysSetup.slnx /t:PowerToysBootstrapperVNext /p:Configuration=Release /p:Platform="x64" 
```

### Supported arguments for the .EXE Bootstrapper installer

Head over to the wiki to see the [full list of supported installer arguments][installerArgWiki].

[installerArgWiki]: https://github.com/microsoft/PowerToys/wiki/Installer-arguments
