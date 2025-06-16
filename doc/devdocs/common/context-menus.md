# PowerToys Context Menu Handlers

This document describes how context menu handlers are implemented in PowerToys, covering both Windows 10 and Windows 11 approaches.

## Context Menu Implementation Types

PowerToys implements two types of context menu handlers:

1. **Old-Style Context Menu Handlers**
   - Used for Windows 10 compatibility
   - Registered via registry entries
   - Implemented as COM objects exposing the `IContextMenu` interface
   - Registered for specific file extensions

2. **Windows 11 Context Menu Handlers**
   - Implemented as sparse MSIX packages
   - Exposing the `IExplorerCommand` interface
   - Located in `PowerToys\x64\Debug\modules\<module>\<module>.msix`
   - Registered for all file types and filtered in code
   - Requires signing to be installed

## Context Menu Handler Registration Approaches

PowerToys modules use two different approaches for registering context menu handlers:

### 1. Dual Registration (e.g., ImageResizer, PowerRename)

- Both old-style and Windows 11 context menu handlers are registered
- Results in duplicate entries in Windows 11's expanded context menu
- Ensures functionality even if Windows 11 handler fails to appear
- Old-style handlers appear in the "Show more options" expanded menu

### 2. Selective Registration (e.g., NewPlus)

- Windows 10: Uses old-style context menu handler
- Windows 11: Uses new MSIX-based context menu handler
- Avoids duplicates but can cause issues if Windows 11 handler fails to register

## Windows 11 Context Menu Handler Implementation

### Package Registration

- MSIX packages are defined in `AppManifest.xml` in each context menu project
- Registration happens in `DllMain` of the module interface DLL when the module is enabled
- Explorer restart may be required after registration for changes to take effect
- Registration can be verified with `Get-AppxPackage` PowerShell command:
  ```powershell
  Get-AppxPackage -Name *PowerToys*
  ```

### Technical Implementation

- Handlers implement the `IExplorerCommand` interface
- Key methods:
  - `GetState`: Determines visibility based on file type
  - `Invoke`: Handles the action when the menu item is clicked
  - `GetTitle`: Provides the text to display in the context menu
- For selective filtering (showing only for certain file types), the logic is implemented in the `GetState` method

### Example Implementation Flow

1. Build generates an MSIX package from the context menu project
2. When the module is enabled, PowerToys installs the package using `PackageManager.AddPackageAsync`
3. The package references the DLL that implements the actual context menu handler
4. When the user right-clicks, Explorer loads the DLL and calls into its methods

## Debugging Context Menu Handlers

### Debugging Old-Style (Windows 10) Handlers

1. Update the registry to point to your debug build
2. Restart Explorer
3. Attach the debugger to explorer.exe
4. Set breakpoints and test by right-clicking in File Explorer

### Debugging Windows 11 Handlers

1. Build PowerToys to get the MSIX packages
2. Sign the MSIX package with a self-signed certificate
3. Replace files in the PowerToys installation directory
4. Use PowerToys to install the package
5. Restart Explorer
6. Run Visual Studio as administrator
7. Set breakpoints in relevant code
8. Attach to DllHost.exe process when context menu is triggered

### Debugging Challenges

- Windows 11 handlers require signing and reinstalling for each code change
- DllHost loads the DLL only when context menu is triggered and unloads after
- For efficient development, use logging or message boxes instead of breakpoints
- Consider debugging the Windows 10 handler by removing OS version checks

## Common Issues

- Context menu entries not showing in Windows 11
  - Usually due to package not being removed/updated properly on PowerToys update
  - Fix: Uninstall and reinstall the package or restart Explorer
- Registering packages requires signing
  - For local testing, create and install a signing certificate
- Duplicate entries in Windows 11 context menu
  - By design for some modules to ensure availability if Windows 11 handler fails
