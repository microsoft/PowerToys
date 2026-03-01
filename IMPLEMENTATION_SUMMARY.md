# MouseScrollRemap Implementation Summary

## Overview
This implementation adds a new PowerToys utility called "MouseScrollRemap" that remaps `Shift + MouseWheel` to `Shift + Ctrl + MouseWheel` for consistent horizontal scrolling across all applications.

## Problem Solved
Many applications (Chrome, JetBrains IDEs, GIMP) use `Shift + MouseWheel` for horizontal scrolling, while Microsoft Office applications use `Ctrl + Shift + MouseWheel`. This creates an inconsistent user experience. The MouseScrollRemap feature solves this by automatically converting `Shift + MouseWheel` to `Shift + Ctrl + MouseWheel` system-wide.

## Implementation Details

### Files Created
1. **Module Core Files**:
   - `src/modules/MouseUtils/MouseScrollRemap/dllmain.cpp` - Main implementation with mouse hook
   - `src/modules/MouseUtils/MouseScrollRemap/pch.h/cpp` - Precompiled headers
   - `src/modules/MouseUtils/MouseScrollRemap/trace.h/cpp` - ETW telemetry tracing
   - `src/modules/MouseUtils/MouseScrollRemap/resource.h` - Resource definitions
   - `src/modules/MouseUtils/MouseScrollRemap/MouseScrollRemap.rc` - Resource file

2. **Build Configuration**:
   - `src/modules/MouseUtils/MouseScrollRemap/MouseScrollRemap.vcxproj` - Visual Studio project file
   - `src/modules/MouseUtils/MouseScrollRemap/MouseScrollRemap.vcxproj.filters` - Project filters
   - `src/modules/MouseUtils/MouseScrollRemap/packages.config` - NuGet packages

3. **Documentation**:
   - `src/modules/MouseUtils/MouseScrollRemap/README.md` - Module documentation
   - `src/modules/MouseUtils/MouseScrollRemap/SECURITY_REVIEW.md` - Security analysis
   - `doc/devdocs/modules/mouseutils/readme.md` - Updated with new module info

### Files Modified
1. `src/common/logger/logger_settings.h` - Added mouseScrollRemapLoggerName
2. `src/runner/main.cpp` - Added PowerToys.MouseScrollRemap.dll to module loading list
3. `PowerToys.slnx` - Added project to solution

### Technical Architecture

#### How It Works
1. **Hook Installation**: When enabled, the module installs a low-level mouse hook (WH_MOUSE_LL) using SetWindowsHookEx
2. **Event Detection**: The hook monitors WM_MOUSEWHEEL events
3. **Modifier Key Check**: Uses GetAsyncKeyState to detect if Shift is pressed but Ctrl is not
4. **Input Injection**: When Shift+MouseWheel is detected:
   - Blocks the original event
   - Injects Ctrl key down event
   - Injects mouse wheel event with original mouseData
   - Injects Ctrl key up event
   - Result: Application receives Shift+Ctrl+MouseWheel

#### Key Components
- **PowertoyModuleIface**: Standard PowerToys module interface implementation
- **Low-level Mouse Hook**: WH_MOUSE_LL hook for intercepting mouse wheel events
- **SendInput API**: Used for keyboard and mouse event injection
- **Error Handling**: Validates SendInput success and falls back gracefully on failure
- **ETW Tracing**: Implements telemetry for enable/disable events

## Building the Module

### Prerequisites
- Visual Studio 2022 (version 17.4 or later)
- Windows 10 SDK (minimum version 1803)
- PowerToys repository cloned locally

### Build Steps
1. Open PowerToys.slnx in Visual Studio 2022
2. Set configuration to Debug or Release
3. Set platform to x64 or ARM64
4. Build the solution or build just the MouseScrollRemap project

Alternatively, using command line:
```powershell
cd /path/to/PowerToys
.\tools\build\build-essentials.ps1
cd src\modules\MouseUtils\MouseScrollRemap
.\tools\build\build.ps1
```

### Output
- Build output: `x64\Debug\PowerToys.MouseScrollRemap.dll` (or Release/ARM64)
- The DLL will be loaded by PowerToys.exe at runtime

## Testing the Feature

### Manual Testing Steps
1. **Build and Install**:
   - Build the PowerToys solution
   - Run PowerToys.exe from the build output folder

2. **Enable the Feature**:
   - Open PowerToys Settings
   - Navigate to Mouse Utilities (if it has a settings page; otherwise it will auto-enable)
   - Enable MouseScrollRemap

3. **Test Horizontal Scrolling**:
   - Open Microsoft Word, Excel, or PowerPoint
   - Create a document wider than the viewport
   - Hold Shift and scroll with mouse wheel
   - Expected: Document should scroll horizontally (left/right)

4. **Test in Other Applications**:
   - Try in Chrome, Firefox, or other browsers
   - Try in Visual Studio Code or other text editors
   - Verify horizontal scrolling works consistently

### What to Verify
- ✅ Shift + MouseWheel scrolls horizontally in Office apps
- ✅ Normal MouseWheel (without Shift) still scrolls vertically
- ✅ Ctrl + MouseWheel still zooms (if application supports it)
- ✅ Feature can be enabled/disabled from settings
- ✅ No performance degradation during scrolling
- ✅ No input lag or delay
- ✅ Hook is properly uninstalled when disabled

### Known Limitations
- Some applications may not support horizontal scrolling via keyboard modifiers
- Applications must be at the same or lower integrity level as PowerToys Runner
- Protected processes (e.g., UAC prompts) will not receive injected events

## Troubleshooting

### Module Not Loading
1. Check PowerToys logs in `%LOCALAPPDATA%\Microsoft\PowerToys\Logs\`
2. Look for MouseScrollRemap initialization messages
3. Verify PowerToys.MouseScrollRemap.dll is in the same folder as PowerToys.exe

### Feature Not Working
1. Verify the module is enabled in settings
2. Check that Shift key is being detected (test in Notepad)
3. Review logs for any error messages from SendInput failures
4. Try disabling and re-enabling the module

### Build Errors
1. Ensure all NuGet packages are restored
2. Run `build-essentials.ps1` to restore and build core components
3. Check that Microsoft.Windows.CppWinRT package is available
4. Verify Visual Studio 2022 with C++ workload is installed

## Future Enhancements

### Potential Improvements
1. **Settings UI Integration**: Add a dedicated toggle in Mouse Utilities settings page
2. **Configurable Behavior**: Allow users to choose which modifier combination to use
3. **Application Whitelist/Blacklist**: Enable/disable for specific applications
4. **Visual Feedback**: Show a toast notification when first enabled
5. **Performance Optimization**: Add debouncing for rapid scroll events
6. **Horizontal Scroll Indicator**: Visual indicator when horizontal scroll is active

### Settings Page Integration (Future Work)
To add this to the Mouse Utilities settings page:
1. Update `src/settings-ui/Settings.UI/SettingsXAML/Views/MouseUtilsPage.xaml`
2. Add a toggle control for MouseScrollRemap
3. Update `src/settings-ui/Settings.UI/ViewModels/MouseUtilsViewModel.cs`
4. Bind the toggle to the module's enable/disable state

## Code Quality

### Code Review
- ✅ All code review comments addressed
- ✅ Proper error handling implemented
- ✅ ETW tracing implemented
- ✅ Follows PowerToys coding standards
- ✅ Comments explain non-obvious logic

### Security Review
- ✅ No security vulnerabilities identified
- ✅ Input validation implemented
- ✅ Resource management proper
- ✅ No buffer overflows or race conditions
- ✅ Follows Windows API best practices

## References
- [PowerToys Mouse Utilities Documentation](../../doc/devdocs/modules/mouseutils/readme.md)
- [PowerToys Build Guidelines](../../tools/build/BUILD-GUIDELINES.md)
- [Windows Low-Level Mouse Hook](https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc)
- [SendInput API](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput)
