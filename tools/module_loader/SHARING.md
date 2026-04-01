# Sharing ModuleLoader and Modules

This guide explains how to share the ModuleLoader tool and PowerToy modules with others for testing purposes.

## Overview

The ModuleLoader is designed to be a **portable, standalone testing tool** that can be shared with module developers and testers. It has minimal dependencies and can work with any compatible PowerToy module DLL.

---

## What You Need to Share

### For Testing a Module (e.g., CursorWrap)

#### **Minimum Package** (Recommended for Quick Testing)

1. **ModuleLoader.exe** - The standalone loader application
   - Location: `x64\Debug\ModuleLoader.exe` or `x64\Release\ModuleLoader.exe`
   - No additional DLLs required (uses only Windows system libraries)

2. **The Module DLL** - The PowerToy module to test
   - Example: `CursorWrap.dll` from `x64\Debug\` or `x64\Release\`
   - Location varies by module (see module-specific locations below)

3. **settings.json** - Module configuration (place in same folder as the DLL)
   - **NEW**: Settings can be placed alongside the module DLL for portable testing
   - Location: Same directory as the module DLL (e.g., `settings.json` next to `CursorWrap.dll`)
   - Falls back to: `%LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\settings.json` if not found locally

#### **Complete Standalone Package** (For Users Without PowerToys Installed)

1. **ModuleLoader.exe**
2. **Module DLL**
3. **Sample settings.json** - Pre-configured settings file
4. **Installation instructions** - See "Standalone Package Setup" section below

---

### Debug Builds
If you build the module in Debug configuration:
- The module will output debug messages via `OutputDebugString()`
- View these with [DebugView](https://learn.microsoft.com/sysinternals/downloads/debugview) or Visual Studio Output window
- Example: CursorWrap outputs detailed topology and cursor wrapping debug info

---


## Module-Specific File Locations

### CursorWrap
```
Files to share:
  - x64\Debug\CursorWrap.dll  (or Release)
  - %LOCALAPPDATA%\Microsoft\PowerToys\CursorWrap\settings.json

Size: ~100KB
```

### MouseHighlighter
```
Files to share:
  - x64\Debug\MouseHighlighter.dll  (or Release)
  - %LOCALAPPDATA%\Microsoft\PowerToys\MouseHighlighter\settings.json

Size: ~150KB
```

### FindMyMouse
```
Files to share:
  - x64\Debug\FindMyMouse.dll  (or Release)
  - %LOCALAPPDATA%\Microsoft\PowerToys\FindMyMouse\settings.json

Size: ~120KB
```

### MousePointerCrosshairs
```
Files to share:
  - x64\Debug\MousePointerCrosshairs.dll  (or Release)
  - %LOCALAPPDATA%\Microsoft\PowerToys\MousePointerCrosshairs\settings.json

Size: ~140KB
```

### MouseJump
```
Files to share:
  - x64\Debug\MouseJump.dll  (or Release)
  - %LOCALAPPDATA%\Microsoft\PowerToys\MouseJump\settings.json

Note: MouseJump is a UI-based module and may not work fully with ModuleLoader
Size: ~200KB
```

### AlwaysOnTop
```
Files to share:
  - x64\Debug\AlwaysOnTop.dll  (or Release)
  - %LOCALAPPDATA%\Microsoft\PowerToys\AlwaysOnTop\settings.json

Size: ~100KB
```

---

## Dependency Analysis

### ModuleLoader.exe Dependencies
**Windows System Libraries Only** (automatically available on all Windows systems):
- `KERNEL32.dll` - Core Windows API
- `USER32.dll` - User interface functions
- `SHELL32.dll` - Shell functions
- `ole32.dll` - COM library

**No PowerToys dependencies required!** The ModuleLoader is completely standalone.

### Module DLL Dependencies (Typical)
Most PowerToy modules depend on:
- Windows system DLLs (automatically available)
- PowerToys common libraries (if any, they're typically statically linked)
- **Module settings** - Must be present in `%LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\`

**Important**: Modules are generally **self-contained** and statically link most dependencies. You typically only need the module DLL itself.

---

## Creating a Standalone Package

### Step 1: Prepare the Files

Create a folder structure like this:
```
ModuleLoaderPackage\
??? ModuleLoader.exe
??? CursorWrap.dll (or other module)
??? settings.json (module settings - placed locally!)
```

**NEW Simplified Structure**: You can now place `settings.json` directly alongside the module DLL! The ModuleLoader will check this location first before looking in the standard PowerToys settings directories.

### Step 2: Extract Settings from Your Machine

```powershell
# Copy settings from your development machine
$moduleName = "CursorWrap"  # Change as needed
$settingsPath = "$env:LOCALAPPDATA\Microsoft\PowerToys\$moduleName\settings.json"
Copy-Item $settingsPath ".\settings\$moduleName\settings.json"
```

### Step 3: Create Installation Instructions (README.txt)

```text
PowerToys Module Testing Package
=================================

This package contains the ModuleLoader tool for testing PowerToy modules.

Contents:
  - ModuleLoader.exe     : Standalone module loader
  - modules\*.dll        : PowerToy module(s) to test
  - settings\*\*.json    : Module configuration files

Setup (First Time):
-------------------
1. Create settings directory:
   %LOCALAPPDATA%\Microsoft\PowerToys\

2. Copy settings:
   Copy the entire "settings\<ModuleName>" folder to:
   %LOCALAPPDATA%\Microsoft\PowerToys\

   Example for CursorWrap:
   Copy "settings\CursorWrap" to:
   %LOCALAPPDATA%\Microsoft\PowerToys\CursorWrap\

Usage:
------
ModuleLoader.exe modules\CursorWrap.dll

The tool will:
  - Load the module DLL
  - Read settings from %LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\
  - Register hotkeys
  - Enable the module

Press Ctrl+C to exit.
Press the module's hotkey to toggle functionality.

Requirements:
-------------
- Windows 10 1803 or later
- No PowerToys installation required!

Troubleshooting:
----------------
If you see "Settings file not found":
  1. Make sure you copied the settings folder correctly
  2. Check that the path is:
     %LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\settings.json
  3. You can also run PowerToys once to generate default settings

Debug Logs:
-----------
Module logs are written to:
  %LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\Logs\

For debug builds, use DebugView to see real-time output.
```

---

## Quick Distribution Methods

### Method 1: ZIP Archive
```powershell
# Create a complete package
$moduleName = "CursorWrap"
$packageName = "ModuleLoader-$moduleName-Package"

# Collect files
New-Item $packageName -ItemType Directory
Copy-Item "x64\Debug\ModuleLoader.exe" "$packageName\"
New-Item "$packageName\modules" -ItemType Directory
Copy-Item "x64\Debug\$moduleName.dll" "$packageName\modules\"
New-Item "$packageName\settings\$moduleName" -ItemType Directory -Force
Copy-Item "$env:LOCALAPPDATA\Microsoft\PowerToys\$moduleName\settings.json" "$packageName\settings\$moduleName\"

# Create README
@"
See README in the tools\module_loader folder for instructions
"@ | Out-File "$packageName\README.txt"

# Zip it
Compress-Archive -Path $packageName -DestinationPath "$packageName.zip"
```

### Method 2: Direct Share (Advanced Users)
For developers who already have PowerToys installed:
```powershell
# Just share the executables
Copy-Item "x64\Debug\ModuleLoader.exe" "\\ShareLocation\"
Copy-Item "x64\Debug\CursorWrap.dll" "\\ShareLocation\"
```

They can run: `ModuleLoader.exe CursorWrap.dll`
(Settings will be loaded from their existing PowerToys installation)

---

## Platform-Specific Notes

### x64 vs ARM64

**Important**: Match architectures!
- `x64\Debug\ModuleLoader.exe` ? Only works with `x64` module DLLs
- `ARM64\Debug\ModuleLoader.exe` ? Only works with `ARM64` module DLLs

**Distribution Tip**: Provide both architectures if targeting multiple platforms:
```
ModuleLoaderPackage\
??? x64\
?   ??? ModuleLoader.exe
?   ??? modules\CursorWrap.dll
??? ARM64\
?   ??? ModuleLoader.exe
?   ??? modules\CursorWrap.dll
??? settings\...
```

### Debug vs Release

**Debug builds**:
- Larger file size
- Include debug symbols
- Verbose logging via `OutputDebugString()`
- Recommended for testing/development

**Release builds**:
- Smaller file size
- Optimized performance
- Minimal logging
- Recommended for end-user testing

---

## Testing Checklist

Before sharing a module package:

- [ ] ModuleLoader.exe is included
- [ ] Module DLL is included (matching architecture)
- [ ] Sample settings.json is included
- [ ] README/instructions are included
- [ ] Tested on a clean machine (no PowerToys installed)
- [ ] Verified hotkeys work
- [ ] Verified Ctrl+C exits cleanly
- [ ] Confirmed settings path in documentation

---

## Advanced: Portable Package Script

Here's a complete PowerShell script to create a fully portable package:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$ModuleName,
    
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64"
)

$packageName = "ModuleLoader-$ModuleName-$Platform-$Configuration"
$packagePath = ".\$packageName"

Write-Host "Creating portable package: $packageName" -ForegroundColor Green

# Create structure
New-Item $packagePath -ItemType Directory -Force | Out-Null
New-Item "$packagePath\modules" -ItemType Directory -Force | Out-Null
New-Item "$packagePath\settings\$ModuleName" -ItemType Directory -Force | Out-Null

# Copy ModuleLoader
$loaderPath = "$Platform\$Configuration\ModuleLoader.exe"
if (Test-Path $loaderPath) {
    Copy-Item $loaderPath "$packagePath\"
    Write-Host "? Copied ModuleLoader.exe" -ForegroundColor Green
} else {
    Write-Host "? ModuleLoader.exe not found at $loaderPath" -ForegroundColor Red
    exit 1
}

# Copy Module DLL
$modulePath = "$Platform\$Configuration\$ModuleName.dll"
if (Test-Path $modulePath) {
    Copy-Item $modulePath "$packagePath\modules\"
    Write-Host "? Copied $ModuleName.dll" -ForegroundColor Green
} else {
    Write-Host "? $ModuleName.dll not found at $modulePath" -ForegroundColor Red
    exit 1
}

# Copy Settings
$settingsPath = "$env:LOCALAPPDATA\Microsoft\PowerToys\$ModuleName\settings.json"
if (Test-Path $settingsPath) {
    Copy-Item $settingsPath "$packagePath\settings\$ModuleName\"
    Write-Host "? Copied settings.json" -ForegroundColor Green
} else {
    Write-Host "? Settings not found at $settingsPath - creating placeholder" -ForegroundColor Yellow
    @"
{
  "name": "$ModuleName",
  "version": "1.0"
}
"@ | Out-File "$packagePath\settings\$ModuleName\settings.json"
}

# Create README
@"
PowerToys $ModuleName Testing Package
======================================

Configuration: $Configuration
Platform: $Platform

Setup Instructions:
-------------------
1. Copy the 'settings\$ModuleName' folder to:
   %LOCALAPPDATA%\Microsoft\PowerToys\

2. Run:
   ModuleLoader.exe modules\$ModuleName.dll

3. Press Ctrl+C to exit

Logs are written to:
  %LOCALAPPDATA%\Microsoft\PowerToys\$ModuleName\Logs\

For more information, see:
  https://github.com/microsoft/PowerToys/tree/main/tools/module_loader
"@ | Out-File "$packagePath\README.txt"

# Create ZIP
$zipPath = "$packageName.zip"
Compress-Archive -Path $packagePath -DestinationPath $zipPath -Force
Write-Host "? Created $zipPath" -ForegroundColor Green

# Show summary
Write-Host "`nPackage Contents:" -ForegroundColor Cyan
Get-ChildItem $packagePath -Recurse | ForEach-Object {
    Write-Host "  $($_.FullName.Replace($packagePath, ''))"
}

Write-Host "`nPackage ready: $zipPath" -ForegroundColor Green
Write-Host "Size: $([math]::Round((Get-Item $zipPath).Length / 1KB, 2)) KB"
```

**Usage**:
```powershell
.\CreateModulePackage.ps1 -ModuleName "CursorWrap" -Configuration Release -Platform x64
```

---

## FAQ

### Q: Can I share just ModuleLoader.exe and the module DLL?
**A**: Yes, but the recipient must have PowerToys installed (or manually create the settings file).

### Q: Does the tester need PowerToys installed?
**A**: No, if you provide the complete package with settings. ModuleLoader is fully standalone.

### Q: What if settings.json doesn't exist?
**A**: ModuleLoader will show an error. Either:
1. Run PowerToys once with the module enabled to generate settings
2. Manually create a minimal settings.json file
3. Include a sample settings.json in your package

### Q: Can I test modules on a virtual machine?
**A**: Yes! This is a great use case. Just copy the package to the VM - no PowerToys installation needed.

### Q: Do I need to include PDB files?
**A**: Only for debugging. For normal testing, just the EXE and DLL are sufficient.

### Q: Can I distribute this to end users?
**A**: ModuleLoader is a **development/testing tool**, not intended for end-user distribution. For production use, direct users to install PowerToys.

---

## Security Considerations

When sharing module DLLs:

1. **Verify Source**: Only share modules you built from trusted source code
2. **Scan for Malware**: Run antivirus scans on the package before sharing
3. **HTTPS Only**: Use secure channels (HTTPS, OneDrive, SharePoint) for distribution
4. **Hash Verification**: Consider providing SHA256 hashes for file integrity:
   ```powershell
   Get-FileHash ModuleLoader.exe -Algorithm SHA256
   Get-FileHash modules\CursorWrap.dll -Algorithm SHA256
   ```

---

## Example Package (CursorWrap)

Here's what a complete CursorWrap testing package looks like:

```
ModuleLoader-CursorWrap-x64-Debug.zip (220 KB)
?
??? ModuleLoader-CursorWrap-x64-Debug\
    ??? ModuleLoader.exe (160 KB)
    ??? README.txt (2 KB)
    ??? modules\
    ?   ??? CursorWrap.dll (55 KB)
    ??? settings\
        ??? CursorWrap\
            ??? settings.json (3 KB)
```

**Total package size**: ~220 KB (compressed)

---

## Support

For issues with ModuleLoader, see:
- [ModuleLoader README](./README.md)
- [PowerToys Documentation](https://aka.ms/PowerToysOverview)
- [PowerToys GitHub Issues](https://github.com/microsoft/PowerToys/issues)

---

## License

ModuleLoader is part of PowerToys and is licensed under the MIT License.
See the LICENSE file in the PowerToys repository root for details.
