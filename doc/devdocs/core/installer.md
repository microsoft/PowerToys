# PowerToys Installer

## Installer Architecture (WiX 3)

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

- First builds `PowerToysSetupCustomActions` DLL and signs it
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

#### Prerequisites for building the MSI installer

1. Install the [WiX Toolset Visual Studio 2022 Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension).
1. Install the [WiX Toolset build tools](https://github.com/wixtoolset/wix3/releases/tag/wix3141rtm). (installer [direct link](https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314.exe))
1. Download [WiX binaries](https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip) and extract `wix.targets` to `C:\Program Files (x86)\WiX Toolset v3.14`.

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

1. Open `installer\PowerToysSetup.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu choose `Build Solution`.

The resulting `PowerToysSetup.msi` installer will be available in the `installer\PowerToysSetup\x64\Release\` folder.

### Supported arguments for the .EXE Bootstrapper installer

Head over to the wiki to see the [full list of supported installer arguments][installerArgWiki].

[installerArgWiki]: https://github.com/microsoft/PowerToys/wiki/Installer-arguments
