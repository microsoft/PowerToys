# Executable actions

This plugin adds actions for `.exe` and `.dll` files.

## Uninstall

If it finds an uninstaller associated with the file, it will launch the uninstaller.

### How does it find the uninstaller

This action resolves a shortcut (if applicable) and takes the destination path of the shortcut. Then it searches the registry key `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` for a value `InstallLocation` that contains the path of the exe. When the action is invoked the corresponding registry key `UninstallString` in the command line.

