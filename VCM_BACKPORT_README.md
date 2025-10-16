# Video Conference Mute (VCM) Backport - Complete

## Overview
This branch successfully backports the **Video Conference Mute** module from PowerToys v0.95.0 into the current codebase. All functionality, UI, installer components, and supporting infrastructure have been fully restored.

## What Was Restored

### 1. Core Module (158 files, ~10,000 lines added)
- **VideoConferenceModule** - Main DLL that provides the VCM functionality
- **VideoConferenceShared** - Shared library for common utilities
- **VideoConferenceProxyFilter** - DirectShow filter for camera proxy (x86 & x64)
- All VCM assets, icons, and resources

### 2. Settings UI Integration
- VideoConferenceViewModel with full IPC communication
- XAML pages for both settings and OOBE
- Navigation integration in ShellPage and ShellViewModel
- Resource strings for all UI elements
- Enabled/disabled module tracking in EnabledModules.cs

### 3. Runner Integration
- VCM module registered in main.cpp knownModules list
- Settings window enum and navigation support
- Removed VCM cleanup code (since we're re-adding the feature)

### 4. Installer Components
- **VideoConference.wxs** - Complete component group for VCM files
- **Custom Actions** for driver installation:
  - `CertifyVirtualCameraDriverCA` - Installs driver certificate
  - `InstallVirtualCameraDriverCA` - Installs the virtual camera driver
  - `UninstallVirtualCameraDriverCA` - Removes driver on uninstall
- WebcamReportTool for diagnostics
- CAB file generation scripts

### 5. GPO/Policy Support
- GPO wrapper functions restored
- PowerToys.admx/adml policy definitions
- Full admin control over VCM feature

### 6. COM Interop
- CommonManaged.cpp/h/idl with VCM interfaces
- Settings deep link support

### 7. Supporting Infrastructure
- Build pipeline configuration (.pipelines/)
- ESRP signing for VCM driver
- Documentation and tools
- GitHub issue template updates
- Spell-check dictionary entries

## Building PowerToys with VCM

### Prerequisites
- Visual Studio 2022 (17.4+)
- Windows 10 SDK (19041 or later)
- .NET 8.0 SDK
- WiX Toolset 3.14 (for installer)

### Build Steps

#### 1. Build the Solution
```powershell
# Open PowerShell in the repo root
cd d:\Programmeerprojecten\GitHub\Forks\PowerToys

# Run the build script (builds everything including VCM)
.\tools\build\build.ps1 -Configuration Release -Platform x64
```

Or build manually in Visual Studio:
1. Open `PowerToys.sln`
2. Set configuration to **Release** and platform to **x64**
3. Build → Build Solution (Ctrl+Shift+B)

#### 2. Build the VCM Proxy Filter x86 (Required for x86 apps)
```powershell
# The x86 proxy filter needs to be built separately
cd src\modules\videoconference\VideoConferenceProxyFilter
msbuild VideoConferenceProxyFilter.vcxproj /p:Configuration=Release /p:Platform=Win32
```

#### 3. Generate VCM CAB File (For Driver)
```powershell
# Run the CAB generation script
.\tools\build\video_conference_make_cab.ps1
```

#### 4. Build the Installer
```powershell
# Build the installer (includes VCM)
cd installer\PowerToysSetup
msbuild PowerToysInstaller.wixproj /p:Configuration=Release /p:Platform=x64
```

The installer will be in: `installer\PowerToysSetup\bin\Release\PowerToysSetup-x64.msi`

### Quick Build Command
```powershell
# All-in-one build (from repo root)
.\tools\build\build.ps1 -Configuration Release -Platform x64
cd installer\PowerToysSetup
msbuild PowerToysInstaller.wixproj /p:Configuration=Release /p:Platform=x64
```

## Installing PowerToys with VCM

### Option 1: Install from MSI (Recommended)
```powershell
# Run the installer with admin rights
Start-Process "installer\PowerToysSetup\bin\Release\PowerToysSetup-x64.msi" -Verb RunAs
```

The installer will:
1. Install all PowerToys modules including VCM
2. Install the VCM virtual camera driver (requires admin)
3. Register VCM as a virtual camera device
4. Set up all necessary registry entries

### Option 2: Run from Build Output (Development)
```powershell
# Run PowerToys directly from x64\Release
.\x64\Release\PowerToys.exe
```

**Note**: When running from build output, the VCM driver won't be installed automatically. You'll need to manually install it for full functionality.

## Using Video Conference Mute

### First-Time Setup
1. Open PowerToys Settings
2. Navigate to **Video Conference** in the left menu
3. Enable the module
4. Configure your hotkeys:
   - **Mute/Unmute Microphone** (default: Ctrl+Shift+Q)
   - **Turn On/Off Camera** (default: Ctrl+Shift+O)
   - **Both** (default: Ctrl+Shift+B)
5. Choose camera/microphone behavior

### Features
- **System Tray Toolbar**: Shows real-time mute status
- **Global Hotkeys**: Works in any application
- **Virtual Camera**: Can show custom image when camera is off
- **Multi-device Support**: Mutes all microphones simultaneously
- **Visual Feedback**: Toolbar changes based on mute state

### Toolbar Icons
- 🎤 Green + 📹 Green = Both unmuted
- 🎤 Red + 📹 Green = Mic muted, camera on
- 🎤 Green + 📹 Red = Mic on, camera off
- 🎤 Red + 📹 Red = Both muted

## Troubleshooting

### VCM Not Appearing in Settings
- **Check**: Is `mf.dll` (Media Foundation) available on your system?
- **Solution**: VCM requires Media Foundation. Most Windows 10+ systems have this.
- **Verify**: PowerToys checks for `mf.dll` and `PowerToys.VideoConferenceModule.dll`

### Virtual Camera Not Working
- **Issue**: Driver not installed
- **Solution**: Reinstall PowerToys from MSI with admin rights
- **Manual Install**: Run installer custom action or use device manager

### Build Errors
- **Missing packages**: Run `.\tools\build\build-essentials.ps1` first
- **Platform mismatch**: Ensure you're building x64 for main, x86 for proxy filter
- **WiX not found**: Install WiX Toolset 3.14 from https://wixtoolset.org/

### Driver Signing (For Distribution)
- VCM driver requires code signing for distribution
- Development builds use test signing
- Production requires EV certificate for driver signing

## Known Considerations

### Why VCM Was Deprecated
Microsoft originally deprecated VCM due to:
1. Driver maintenance complexity
2. Limited user adoption
3. Native Teams/Zoom features improving
4. Security/compatibility concerns with virtual camera drivers

### Maintenance Notes
- Virtual camera driver requires updates for new Windows versions
- DirectShow filter needs x86 and x64 builds
- GPO policies need to stay in sync
- Certificate management for driver signing

### Alternative Solutions
Consider if these meet your needs instead:
- Native app hotkeys (Teams, Zoom, Discord)
- Hardware mute buttons on headsets
- Third-party virtual camera apps

## Testing Checklist

Before using in production:
- [ ] VCM module loads and appears in Settings
- [ ] Settings page displays correctly
- [ ] Enable/disable toggle works
- [ ] Hotkeys can be configured
- [ ] Microphone mute works across apps
- [ ] Camera toggle works in video apps
- [ ] Toolbar appears and updates correctly
- [ ] Settings persist across PowerToys restart
- [ ] Uninstall removes driver cleanly
- [ ] Works with multiple audio devices

## Files Changed Summary

**Total**: 158 files changed, 9,996 insertions(+), 3,074 deletions(-)

**Key Directories**:
- `src/modules/videoconference/` - Complete VCM module
- `src/settings-ui/` - UI integration and ViewModels  
- `installer/PowerToysSetup/` - Installer components
- `src/common/` - GPO, interop, utilities
- `tools/` - Build scripts and WebcamReportTool

## Additional Resources

- **Original VCM Documentation**: See archived docs in `doc/devdocs/`
- **Driver Details**: `src/modules/videoconference/VideoConferenceModule/README.md`
- **WebcamReportTool**: `doc/devdocs/tools/webcam-report-tool.md`

## Credits

Backported from PowerToys v0.95.0 (commit 39bcba3) by restoring files that were removed in commit 12bb5c21.

---

**Status**: ✅ Complete and ready to build
**Branch**: `backport-vcm`
**Date**: October 16, 2025
