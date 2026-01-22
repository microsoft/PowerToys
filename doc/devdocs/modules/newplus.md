# NewPlus Module

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/newplus)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-New%2B)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3AProduct-New%2B)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-New%2B+)

## Overview

NewPlus is a PowerToys module that provides a context menu entry for creating new files directly from File Explorer. Unlike some other modules, NewPlus implements a different approach to context menu registration to avoid duplication issues in Windows 11.

## Context Menu Implementation

NewPlus implements two separate context menu handlers:

1. **Windows 10 Handler** (`NewPlus.ShellExtension.win10.dll`)
   - Implements "old-style" context menu handler for Windows 10 compatibility
   - Not shown in Windows 11 (this is intentional and controlled by a condition in `QueryContextMenu`)
   - Registered via registry keys

2. **Windows 11 Handler** (`NewPlus.ShellExtension.dll`)
   - Implemented as a sparse MSIX package for Windows 11's modern context menu
   - Only registered and used on Windows 11

This implementation differs from some other modules like ImageResizer which register both handlers on Windows 11, resulting in duplicate menu entries. NewPlus uses selective registration to provide a cleaner user experience, though it can occasionally lead to issues if the Windows 11 handler fails to register properly.

## Project Structure

- **NewPlus.ShellExtension** - Windows 11 context menu handler implementation
- **NewPlus.ShellExtension.win10** - Windows 10 "old-style" context menu handler implementation

## Debugging NewPlus Context Menu Handlers

### Debugging the Windows 10 Handler

1. Update the registry to point to your debug build:
   ```
   Windows Registry Editor Version 5.00

   [HKEY_CLASSES_ROOT\CLSID\{<NewPlus-CLSID>}]
   @="PowerToys NewPlus Extension"

   [HKEY_CLASSES_ROOT\CLSID\{<NewPlus-CLSID>}\InprocServer32]
   @="x:\GitHub\PowerToys\x64\Debug\PowerToys.NewPlusExt.win10.dll"
   "ThreadingModel"="Apartment"

   [HKEY_CURRENT_USER\Software\Classes\Directory\Background\shellex\ContextMenuHandlers\NewPlus]
   @="{<NewPlus-CLSID>}"
   ```

2. Restart Explorer:
   ```
   taskkill /f /im explorer.exe && start explorer.exe
   ```

3. Attach the debugger to explorer.exe
4. Add breakpoints in the NewPlus code
5. Right-click in File Explorer to trigger the context menu handler

### Debugging the Windows 11 Handler

Debugging the Windows 11 handler requires signing the MSIX package:

1. Build PowerToys to get the MSIX packages

2. **Create certificate** (if you don't already have one):
   ```powershell
   New-SelfSignedCertificate -Subject "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" `
    -KeyUsage DigitalSignature `
    -Type CodeSigningCert `
    -FriendlyName "PowerToys SelfCodeSigning" `
    -CertStoreLocation "Cert:\CurrentUser\My"
   ```

3. **Get the certificate thumbprint**:
   ```powershell
   $cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.FriendlyName -like "*PowerToys*" }
   $cert.Thumbprint
   ```

4. **Install the certificate in the Trusted Root** (requires admin Terminal):
   ```powershell
   Export-Certificate -Cert $cert -FilePath "$env:TEMP\PowerToysCodeSigning.cer"
   Import-Certificate -FilePath "$env:TEMP\PowerToysCodeSigning.cer" -CertStoreLocation Cert:\LocalMachine\Root
   ```

   Alternatively, you can manually install the certificate using the Certificate Import Wizard:

   ![wizard 1](../images/newplus/wizard1.png)
   ![wizard 2](../images/newplus/wizard2.png)
   ![wizard 3](../images/newplus/wizard3.png)
   ![wizard 4](../images/newplus/wizard4.png)

5. Sign the MSIX package:
   ```powershell
   SignTool sign /fd SHA256 /sha1 <THUMBPRINT> "x:\GitHub\PowerToys\x64\Debug\WinUI3Apps\NewPlusPackage.msix"
   ```
   
   Note: SignTool might not be in your PATH, so you may need to specify the full path, e.g.:
   ```powershell
   & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" sign /fd SHA256 /sha1 <THUMBPRINT> "x:\GitHub\PowerToys\x64\Debug\WinUI3Apps\NewPlusPackage.msix"
   ```

6. Check if the NewPlus package is already installed and remove it if necessary:
   ```powershell
   Get-AppxPackage -Name Microsoft.PowerToys.NewPlusContextMenu
   Remove-AppxPackage Microsoft.PowerToys.NewPlusContextMenu_<VERSION>_neutral__8wekyb3d8bbwe
   ```

7. Install the new signed MSIX package (optional if launching PowerToys settings first):
   ```powershell
   Add-AppxPackage -Path "x:\GitHub\PowerToys\x64\Debug\WinUI3Apps\NewPlusPackage.msix" -ExternalLocation "x:\GitHub\PowerToys\x64\Debug\WinUI3Apps"
   ```
   
   Note: If you prefer, you can simply launch PowerToys settings and enable the NewPlus module, which will install the MSIX package for you.

8. Restart Explorer to ensure the new context menu handler is loaded:
   ```powershell
   taskkill /f /im explorer.exe && start explorer.exe
   ```

9. Run Visual Studio as administrator (optional)

10. Set breakpoints in the code (e.g., in [shell_context_menu.cpp#L45](/src/modules/NewPlus/NewShellExtensionContextMenu/shell_context_menu.cpp#L45))

11. Right-click in File Explorer and attach the debugger to the `DllHost.exe` process (with NewPlus title) that loads when the context menu is invoked
![alt text](../images/newplus/debug.png)

12. Right-click again (quickly) after attaching the debugger to trigger the breakpoint

Note: The DllHost process loads the DLL only when the context menu is triggered and unloads after, making debugging challenging. For easier development, consider using logging or message boxes instead of breakpoints.

## Common Issues

- If the Windows 11 context menu entry doesn't appear, it may be due to:
  - The package not being properly registered
  - Explorer not being restarted after registration
  - A signature issue with the MSIX package
  
- For development and testing, using the Windows 10 handler can be easier since it doesn't require signing.

## Restoring Built-in Windows New context menu
If the Windows 11 built-in New context menu doesn't repear on uninstalling PowerToys, some issue with settings etc. here's how to restore the built-in New context menu.

1. Open Registry Editor
1. Go to the key "Computer\HKEY_CURRENT_USER\Software\Classes\Directory\background\ShellEx\ContextMenuHandlers"
1. Delete the "New" subkey (i.e. fullpath "Computer\HKEY_CURRENT_USER\Software\Classes\Directory\background\ShellEx\ContextMenuHandlers\New")