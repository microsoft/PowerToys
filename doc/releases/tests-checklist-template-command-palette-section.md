[Go back](tests-checklist-template.md)

## Command Palette
 * Check if Command Palette successfully install/uninstall with PowerToys.
   - [ ] Install PowerToys. Then check if Command Palette exist in the System Settings/App/Installed Apps.
   - [ ] UnInstall PowerToys. Then check if Command Palette doesn't exist in the System Settings/App/Installed Apps.
 * Enable Command Palette in settings and ensure that the hotkey brings up Command Palette
   - [ ] when PowerToys is running unelevated on start-up
   - [ ] when PowerToys is running as admin on start-up
   - [ ] when PowerToys is restarted as admin, by clicking the restart as admin button in settings.
 * Check that each of the plugins is working:
   - [ ] Installed Apps - launch a Win32 application
   - [ ] Installed Apps - launch a Win32 application as admin
   - [ ] Installed Apps - launch a packaged application
   - [ ] Calculator - ensure a mathematical input returns a correct response and is copied on enter.
   - [ ] File Search - open a file on the disk.
   - [ ] File Search - find a file and copy file path.
   - [ ] File Search - find a file and open containing folder.
   - [ ] Run Commands - execute a command. (e.g. `ping google.com`).
   - [ ] Windows Walker - Switch to another opening window.
   - [ ] Windows Walker - Switch to another opening window when powertoys run as admin.
   - [ ] WinGet - Search and install application through WinGet. (eg. `vscode`)
   - [ ] Web Search - Search anything by this extension.
   - [ ] Windows Terminal Profiles - Open profile.
   - [ ] Windows Terminal Profiles - Open profile as Admin.
   - [ ] Windows Settings - Open settings from extension.
   - [ ] Registry - navigate through the registry tree and open registry editor. Enter the action keyword `:` to get the root keys.
   - [ ] Registry - navigate through the registry tree and copy key path.
   - [ ] Windows Service - start, stop, restart windows service.
   - [ ] Time And Date - type `now`, `year`, `week` and verify the result is correct. 
   - [ ] Windows System Command - test `lock`.
   - [ ] Windows System Command - test `empty recycle bin`.
   - [ ] Windows System Command - test `shutdown`.
   - [ ] Windows System Command - Click your network adapter item and paste the result at notepad.
   - [ ] Bookmark - Add bookmarks to command palette.
   - [ ] Bookmark - Open your bookmarks (in Command Palette).
 - [ ] Disable Command Palette and ensure that the hotkey doesn't bring up Command Palette.
 * Test Extensions Manager
   - [ ] Enable/disable extensions and verify changes are picked up by Command Palette
   - [ ] Change `Global hot key` and verify changes are picked up by Command Palette
   - [ ] Change `Alias` and verify changes picked up by Command Palette
   - [ ] Disable all extensions and verify the warning message is shown (Currently not support).
