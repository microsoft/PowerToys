## Telemetry Prologue
 * Before starting your tests, go to General settings, enable "Diagnostic data" and "Enable viewing" and restart PowerToys.

## Install tests
 * install a **previous version** on a clean machine (a clean machine doesn't have the `%localappdata%\Microsoft\PowerToys` folder)
 * open the Settings and for each module change at least one option
 * open the FancyZones editor and create two custom layouts:
    * a canvas layout with 2 zones, use unicode chars in the layout's name
    * one from grid template using 4 zones and splitting one zone
    * apply the custom canvas layout to the primary desktop
    * create a virtual desktop and apply the custom grid layout
    * if you have a second monitor apply different templates layouts for the primary desktop and for the second virtual desktop
 * install the new version (it will uninstall the old version and install the new version)
 - [ ] verify the settings are preserved and FancyZones configuration is still the same
 - [ ] test installing as SYSTEM (LocalSystem account)
   * Download PsTools from https://learn.microsoft.com/en-us/sysinternals/downloads/psexec
   * Run PowerToys installer with psexec tool `psexec.exe -sid <path_to_installer_exe`
   * Brief check if all modules are working

 * PER-USER and PER-MACHINE TESTS:
   * Install **previous version** on a clean machine and update with new per-machine version. Ensure that it is installed in Program files and that registry entries are under **HKLM**/Software/Classes/PowerToys. Go trhough different modules and ensure that they are working correctly.
   * Try installing per-user version over already installed per-machine version and ensure that proper error message is shown.
   * Remove PowerToys and install per-user version. Ensure that it is installed in <APPDATA>/Local/PowerToys and that registry entries are under **HKCU**/Software/Classes/PowerToys. Go trhough different modules and ensure that they are working correctly.
   * Create a new user and install per-user version there as well. Go trhough different modules and ensure that they are working correctly. Ensure that changing settings for one user does not change settings of other user.

## Functional tests

 Regressions:
 - [ ] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038
 - [ ] https://github.com/microsoft/PowerToys/issues/1524

## General Settings

**Admin mode:**
 - [ ] restart PT and verify it runs as user
 - [ ] restart as admin and set "Always run as admin"
 - [ ] restart PT and verify it  runs as admin
 * if it's not on, turn on "Run at startup"
 - [ ] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [ ] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [ ] turn off all the modules and verify all module are off
 - [ ] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [ ] turn on all the module, all module are now working
 - [ ] restart PT and verify that all module are still on in the settings page and they are actually working

**Quick access tray icon flyout:**
 - [ ] Use left click on the system tray icon and verify the flyout appears. (It'll take a bit the first time)
 - [ ] Try to launch a module from the launch screen in the flyout.
 - [ ] Try disabling a module in the all apps screen in the flyout, make it a module that's launchable from the launch screen. Verify that the module is disabled and that it also disappeared from the launch screen in the flyout.
 - [ ] Open the main settings screen on a module page. Verify that when you disable/enable the module on the flyout, that the Settings page is updated too.

**Settings backup/restore:**
 - [ ] In the General tab, create a backup of the settings.
 - [ ] Change some settings in some PowerToys.
 - [ ] Restore the settings in the General tab and verify the Settings you've applied were reset.

## FancyZones Editor

- [ ] Open editor from the settings
- [ ] Open editor with a shortcut
- [ ] Create a new layout (grid and canvas)
- [ ] Duplicate a template and a custom layout
- [ ] Delete layout
- [ ] Edit templates (number of zones, spacing, distance to highlight adjacent zones). Verify after reopening the editor that saved settings are kept the same.
- [ ] Edit canvas layout: zones size and position, create or delete zones.
- [ ] Edit grid layout: split, merge, resize zones.
- [ ] Check `Save and apply` and `Cancel` buttons behavior after editing.
- [ ] Assign a layout to each monitor.
- [ ] Assign keys to quickly switch layouts (custom layouts only), `Win + Ctrl + Alt + number`.
- [ ] Assign horizontal and vertical default layouts
- [ ] Test duplicate layout focus
   * Select any layout X in 'Templates' or 'Custom' section by click left mouse button
   * Mouse right button click on any layout Y in 'Templates' or 'Custom' sections
   * Duplicate it by clicking 'Create custom layout' (Templates section) or 'Duplicate' in 'Custom' section
   * Expect the layout Y is duplicated

## FancyZones

### Appearance
- [ ] Change colors, opacity and `Show zone number` options. Verify they're applied.

### Excluded apps
- [ ] Exclude some apps, verify that they're not applicable to a zone.

### Dragging
- [ ] `Hold Shift key to activate zones while dragging` on, `Use a non-primary mouse button to toggle zone activation` off. Start dragging a window, then press shift. Zones are shown when dragging a window with shift pressed, hidden when you released shift or snapped zone.
- [ ] `Hold Shift key to activate zones while dragging` on, `Use a non-primary mouse button to toggle zone activation` off. Press shift first, then start dragging a window. Zones are shown when dragging a window with shift pressed, hidden when you released shift or snapped zone.
- [ ]  `Hold Shift key to activate zones while dragging` off, `Use a non-primary mouse button to toggle zone activation` on. Zones are shown immediately when dragging a window and hidden when you click a non-primary mouse button or press shift.
- [ ] `Hold Shift key to activate zones while dragging` off, `Use a non-primary mouse button to toggle zone activation` off. Zones are shown immediately when dragging a window, hidden when you press shift.
- [ ] `Hold Shift key to activate zones while dragging` on, `Use a non-primary mouse button to toggle zone activation` on. Zones aren't shown immediately, only when shift is pressed or when a non-primary mouse click changes the state.  
- [ ] `Show zones on all monitor whilw dragging a window` - turn on,off, verify behavior.
- [ ] Create a canvas layout with overlapping zones, check zone activation behavior with all `When multiple zones overlap` options
- [ ] `Make dragged window transparent` - turn on, off, verify behavior

### Snapping
Disable FZ and clear `app-zone-history.json` before starting. FancyZones should be disabled, otherwise, it'll save cashed values back to the file.

- [ ] Snap a window to a zone by dragging, verify `app-zone-history.json` contains info about the window position on the corresponding work area.
- [ ] Snap a window to a zone by a keyboard shortcut, verify `app-zone-history.json` contains info about the window position on the corresponding work area.
- [ ] Snap a window to another monitor, verify `app-zone-history.json` contains positions about zones on both monitors.
- [ ] Snap a window to several zones, verify zone numbers in the json file are correct.
- [ ] Snap a window to a zone, unsnap it, verify this app was removed from the json file.
- [ ] Snap the same window to a zone on two different monitors or virtual desktops. Then unsnap from one of them, verify that info about unsnapped zone was removed from `app-zone-history.json`. Verify info about the second monitor/virtual desktop is kept.  
- [ ] Enable `Restore the original size of windows when unsnapping`, snap window, unsnap window, verify the window changed its size to original.
- [ ] Disable `Restore the original size of windows when unsnapping`, snap window, unsnap window, verify window size wasn't changed.
- [ ] Disable `Restore the original size of windows when unsnapping`, snap window, enable `Restore the original size of windows when unsnapping`, unsnap window, verify window size wasn't changed. 
- [ ] Launch PT in user mode, try to assign a window with administrator privileges to a zone. Verify the notification is shown.
- [ ] Launch PT in administrator mode, assign a window with administrator privileges.
* Open `Task view` , right-click on the window, check the `Show this window on all desktops` or the `Show windows from this app on all desktops` option to turn it on.
    - [ ] Turn Show this window on all desktops on, verify you can snap this window to a zone.
    - [ ] Turn Show windows from this app on all desktops on, verify you can snap this window to a zone.

### Snapped window behavior
- [ ] `Keep windows in their zones when the screen resolution changes` on, snap a window to a zone, change the screen resolution or scaling, verify window changed its size and position.
- [ ] `Keep windows in their zones when the screen resolution changes` on, snap a window to a zone on the secondary monitor. Disconnect the secondary monitor (the window will be moved to the primary monitor). Reconnect the secondary monitor. Verify the window returned to its zone. 
- [ ] `Keep windows in their zones when the screen resolution changes` off, snap a window to a zone, change the screen resolution or scaling, verify window didn't change its size and position.

Enable `During zone layout changes, windows assigned to a zone will match new size/positions` and prepare layouts with 1 and 3 zones where zone size/positions are different.
- [ ] Snap a window to zone 1, change the layout, verify window changed its size/position.
- [ ] Snap a window to zone 3, change the layout, verify window didn't change its size/position because another layout doesn't have a zone with this zone number.
- [ ] Snap a window to zones 1-2, change the layout, verify window changed its size/position to fit zone 1.
- [ ] Snap a window to zones 1-2, change the layout (the window will be snapped to zone 1), then return back to the previous layout, verify the window snapped to 1-2 zones.
- [ ] Disable `During zone layout changes, windows assigned to a zone will match new size/positions`, snap window to zone 1, change layout, verify window didn't change its size/position

Enable `Move newly created windows to their last known zone`.
- [ ] Snap a window to the primary monitor, close and reopen the window. Verify it's snapped to its zone.
- [ ] Snap a window to zones on the primary and secondary monitors. Close and reopen the app. Verify it's snapped to the zone on the active monitor.
- [ ] Snap a window to the secondary monitor (use a different app or unsnap the window from the zone on the primary monitor), close and reopen the window. Verify it's snapped to its zone. 
- [ ] Snap a window, turn off FancyZones, move that window, turn FZ on. Verify window returned to its zone.
- [ ] Move unsnapped window to a secondary monitor, switch virtual desktop and return back. Verify window didn't change its position and size.
- [ ] Snap a window, then resize it (it's still snapped, but doesn't fit the zone). Switch the virtual desktop and return back, verify window didn't change its size.

Enable `Move newly created windows to the current active monitor`.
- [ ] Open a window that wasn't snapped anywhere, verify it's opened on the active monitor.
- [ ] Open a window that was snapped on the current virtual desktop and current monitor, verify it's opened in its zone.
- [ ] Open a window that was snappen on the current virtual desktop and another monitor, verify it's opened on the active monitor.
- [ ] Open a window that was snapped on another virtual desktop, verify it's opened on the active monitor.

- [ ] Enable `Allow popup windows snapping` and `Allow child windows snapping`, try to snap Notepad++ search window. Verify it can be snapped.
- [ ] Enable `Allow popup windows snapping`, snap Teams, verify a popup window appears in its usual position.
- [ ] Enable `Allow popup windows snapping`, snap Visual Studio Code to a zone, and open any menu. Verify the menu is where it's supposed to be and not on the top left corner of the zone.
- [ ] Enable `Allow child windows snapping`, drag any child window (e.g. Solution Explorer), verify it can be snapped to a zone.
- [ ] Disable `Allow child windows snapping`, drag any child window (e.g. Solution Explorer), verify it can't be snapped to a zone.

### Switch between windows in the current zone
Enable `Switch between windows in the current zone` (default shortcut is `Win + PgUp/PgDown`)
- [ ] Snap several windows to one zone, verify switching works.
- [ ] Snap several windows to one zone, switch virtual desktop, return back, verify window switching works.
- [ ] Disable `Switch between windows in the current zone`, verify switching doesn't work.
  
### Override Windows Snap
- [ ] Disable `Override Windows Snap`, verify it's disabled.

Enable `Override Windows Snap`.
Select Move windows based on `Zone index`.
- [ ] Open the previously not snapped window, press `Win`+`LeftArrow` / `Win`+`RightArrow`, verify it's snapped to a first/last zone.
- [ ] Verify `Win`+`LeftArrow` moves the window to a zone with the previous index.
- [ ] Verify `Win`+`RightArrow` moves the window to a zone with the next index.
- [ ] Verify `Win`+`ArrowUp` and `Win`+`ArrowDown` work as usual.

- [ ] `Move windows between zones across all monitors` disabled. Verify `Win`+`LeftArrow` doesn't move the window to any zone when the window is in the first zone.
- [ ] `Move windows between zones across all monitors` disabled. Verify `Win`+`RightArrow` doesn't move the window to any zone when the window is in the last zone.

One monitor:
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`LeftArrow` doesn't move the window to any zone when the window is in the first zone.
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`RightArrow` doesn't move the window to any zone when the window is in the last zone.

Two and more monitors:
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`LeftArrow` cycles window position moving it from the first zone on the current monitor to the last zone of the left (or rightmost, if the current monitor is leftmost) monitor.
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`RightArrow` cycles window position moving it from the last zone on the current monitor to the first zone of the right (or leftmost, if the current monitor is rightmost) monitor.

Select Move windows based on `Relative position`.
- [ ] Open the previously not snapped window, press `Win`+`Arrow`, verify it's snapped.
- [ ] Extend the window using `Ctrl`+`Alt`+`Win`+`Arrow`. Verify the window is snapped to all zones.
- [ ] Extend the window using `Ctrl`+`Alt`+`Win`+`Arrow` and return it back using the opposite arrow. Verify it could be reverted while you hold `Ctrl`+`Alt`+`Win`.

- [ ] `Move windows between zones across all monitors` disabled. Verify `Win`+`LeftArrow` cycles the window position to the left (from the leftmost zone moves to the rightmost in the same row) within one monitor.
- [ ] `Move windows between zones across all monitors` disabled. Verify `Win`+`RightArrow` cycles the window position to the right within one monitor.
- [ ] `Move windows between zones across all monitors` disabled. Verify `Win`+`UpArrow` cycles the window position up within one monitor.
- [ ] `Move windows between zones across all monitors` disabled. Verify `Win`+`DownArrow` cycles the window position down within one monitor.

- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`LeftArrow` cycles the window position to the left (from the leftmost zone moves to the rightmost in the same row) within all monitors.
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`RightArrow` cycles the window position to the right within all monitors.
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`UpArrow` cycles the window position up within all monitors.
- [ ] `Move windows between zones across all monitors` enabled. Verify `Win`+`DownArrow` cycles the window position down within all monitors.

### Layout apply
Enable `Enable quick layout switch`, assign numbers to custom layouts.
- [ ] Switch with `Win` + `Ctrl` + `Alt` + `key`.
- [ ] Switch with just a key while dragging a window.
- [ ] Turn `Flash zones when switching layout` on/off, verify it's flashing/not flashing after pressing the shortcut.
- [ ] Disable `Enable quick layout switch`, verify shortcuts don't work.
- [ ] Disable spacing on any grid layout, verify that there is no space between zones while dragging a window.
- [ ] Create a new virtual desktop, verify that there are the same layouts as applied to the previous virtual desktop.
- [ ] After creating a virtual desktop apply another layout or edit the applied one. Verify that the other virtual desktop layout wasn't changed.
- [ ] Delete an applied custom layout in the Editor, verify that there is no layout applied instead of it.
- [ ] Apply a grid layout, change the screen resolution or scaling, verify that the assigned layout fits the screen. NOTE: canvas layout could not fit the screen if it was created on a monitor with a different resolution.

### Layout reset
* Test layout resetting.
Before testing 
   * Remove all virtual desktops 
   * Remove `CurrentVirtualDesktop` from `\HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\1\VirtualDesktops` 
   * Remove `VirtualDesktopIDs` from `\HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops`

- [ ] Test screen locking
   * Set custom layouts on each monitor
   * Lock screen / unplug monitor / plug monitor
   * Verify that layouts weren't reset to defaults
   
- [ ] Test restart
   * Set custom layouts on each monitor
   * Restart the computer
   * Verify that layouts weren't reset to defaults

- [ ] Test applying default layouts on reset
   * Set default horizontal and vertical layouts
   * Delete `applied-layouts.json`
   * Verify that selected default layout is applied according to configuration

### Span zones across monitors
- [ ] Switch between `Allow zones to span across monitors` on and off. Verify that layouts are applied correctly in both cases.

Repeat the previous subsections steps after enabling `Allow zones to span across monitors`
- [ ] Dragging
- [ ] Snapping
- [ ] Snapped window behavior
- [ ] Switch between windows in the current zone
- [ ] Override Windows Snap
- [ ] Layout apply
- [ ] Layout reset

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [ ] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [ ] verify Preview Pane works for the SVG files
   - [ ] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-PdfPreviewHandler\HelperFiles
   - [ ] verify Preview Pane works for the PDF file
   - [ ] verify the Icon Preview works for the PDF file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-GcodePreviewHandler\HelperFiles
   - [ ] verify Preview Pane works for the gcode file
   - [ ] verify the Icon Preview works for the gcode file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-StlThumbnailProvider\HelperFiles
   - [ ] verify the Icon Preview works for the stl file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\runner
   - [ ] verify Preview Pane works for source files (shows syntax highlighting)
 * Running as admin (or user since recently):
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [ ] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [ ] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached (you can also use `cleanmgr.exe` to clean all thumbnails cached in your system). You may need to restart the machine for this setting to apply as well.
   - [ ] copy and paste one of the SVG file and verify the new file show the generic SVG icon
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-PdfPreviewHandler\HelperFiles
   - [ ] verify Preview Pane doesn't show the preview for the PDF file
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-GcodePreviewHandler\HelperFiles
   - [ ] verify Preview Pane doesn't show the preview for the gcode file
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-StlThumbnailProvider\HelperFiles
   - [ ] verify Preview Pane doesn't show the preview for the stl file (a generated thumbnail would show when there's no preview)
   * go to PowerToys repo and visit src\runner
   - [ ] verify Preview Pane doesn't show the preview for source code files or that it's a default previewer instead of Monaco

## Awake
 - [ ] Try out the features and see if they work, no list at this time.

## Quick Accent
 * Enable Quick Accent and open notepad. Then:
   - [ ] Press `a` and the left or right arrow and verify the accent menu appears and adds the accented letter you've selected. Use left and arrow keys to cycle through the options.
   - [ ] Press `a` and the space key and verify the accent menu appears and adds the accented letter you've selected. Use <kbd>Space</kbd> to navigate forward, <kbd>Space</kbd> + <kbd>Shift</kbd> to navigate backward.
   - [ ] Disable Quick Accent and verify you can no longer add accented characters through Quick Accent.
 * Test the different settings and verify they are applied:
   - [ ] Activation key
   - [ ] Language (for example, Currency has no accents for 'a' but has for 's')
   - [ ] Toolbar position (test every option, some had issues before)
   - [ ] Input delay
   - [ ] Exclude some apps. Verify that Quick Accent is not activated for them.
   - [ ] Sort characters by frequency.
   - [ ] Always start on the first character when using left/right arrows as activation method.

## Registry Preview
 * Open Registry Editor, add new registry key with 1 string value and 1 binary value in e.g. HKLM/Software/Classes/PowerToysTest. Right click new registry key->export and export it to file.
 * Launch Registry Preview by right-clicking exported .reg file->'Preview'. Then:
   - [ ] Edit file content. Ensure that visual try is being re-populated while typing. Save the file by pressing Save file button. Confirm that file is properly saved by pressing Edit file... button which will open file in Notepad. Try saving file using Save file as... button.
   - [ ] Edit file externaly (e.g. in Notepad) and save it there. Pres Reload from file button and ensure that file content and visual tree are reloaded and show new content.
   - [ ] Select some registry key with registry values in visual tree and ensure that registry values are shown properly in bottom-right area.
   - [ ] Try opening different registry file by pressing Open file button.
   - [ ] Delete newly created registry key from first step manually in Registry Editor, then try writing registry changes to registry by pressing Write to Registry button in Registry Preview. *Be careful what you are writing!* 
   
 * Open Registry Preview Settings. Then:
   - [ ] Disable Registry Preview and ensure that Preview context menu option for .reg files no longer appears.
   - [ ] Try to launch Registry Preview from it's OOBE page while Registry Preview is disabled and ensure that it does not start.
   - [ ] Enable Registry Preview again and ensure that Preview context menu option for .reg files appears and that it starts Registry Preview correctly. 
   - [ ] Try to launch Registry Preview from it's Settings page and ensure that it is launched properly.
   - [ ] Try to launch Registry Preview from it's OOBE page and ensure that it is launched properly.
   - [ ] Enable Default app setting. Verify that .reg files are opened with Registry Preview by default. Disable Default app setting. Verify that Registry Editor is now default app.

## Peek
 * Open different files to check that they're shown properly
   - [ ] Image
   - [ ] Text or dev file
   - [ ] Markdown file
   - [ ] PDF
   - [ ] HTML
   - [ ] Archive files (.zip, .tar, .rar)
   - [ ] Any other not mentioned file (.exe for example) to verify the unsupported file view is shown
   
 * Pinning/unpinning
   - [ ] Pin the window, switch between images of different size, verify the window stays at the same place and the same size.
   - [ ] Pin the window, close and reopen Peek, verify the new window is opened at the same place and the same size as before.
   - [ ] Unpin the window, switch to a different file, verify the window is moved to the default place.
   - [ ] Unpin the window, close and reopen Peek, verify the new window is opened on the default place.

* Open with a default program
   - [ ] By clicking a button.
   - [ ] By pressing enter. 
  
 - [ ] Switch between files in the folder using `LeftArrow` and `RightArrow`, verify you can switch between all files in the folder.
 - [ ] Open multiple files, verify you can switch only between selected files.
 - [ ] Change the shortcut, verify the new one works.

## Crop And Lock
 * Thumbnail mode
   - [ ] Test with win32 app
   - [ ] Test with packaged app
   
 * Reparent mode (there are known issues where reparent mode doesn't work for some apps)
   - [ ] Test with win32 app
   - [ ] Test with packaged app

## Command Not Found
 * Go to Command Not Found module settings
 - [ ] If you have PowerShell 7.4 installed, confirm that Install PowerShell 7.4 button is not visible and PowerShell 7.4 is shown as detected. If you don't have PowerShell 7.4, Install it by clicking the button and confirm that it's properly installed. Check Installation logs text box bellow and confirm there are no errors.
 - [ ] If you have Microsoft.WinGet.Client installed, confirm that Install Microsoft.WinGet.Client button is not visible and Microsoft.WinGet.Client is shown as detected. If you don't have Microsoft.WinGet.Client, Install it by clicking the button and confirm that it's properly installed. Check Installation logs text box bellow and confirm there are no errors.
 - [ ] Install the Command Not Found module. Check Installation logs text box bellow and confirm there are no errors. Check PowerShell 7 $PROFILE file and confirm Import-Module command is added there. Start new PowerShell 7.4 session and execute "powertoys" (or "atom"). Confirm that suggestion is given to install powertoys (or atom) winget package. (If suggestion is not given, try running the same command few more times, it might take some time for the first time to load the module). Check Installation logs text box bellow and confirm there are no errors.
 - [ ] Uninstall the module. Check Installation logs text box bellow and confirm there are no errors. Check PowerShell 7 $PROFILE file and confirm Import-Module command is removed. Start new PowerShell 7.4 session and confirm no errors are shown on start.
 - [ ] Install module again. Uninstall PowerToys. Check PowerShell 7 $PROFILE file and confirm Import-Module command is removed after installer is done.

## DSC
 * You need to have some prerequisites installed:
   - PowerShell >= 7.2 .
   - PSDesiredStateConfiguration 2.0.7 or higher `Install-Module -Name PSDesiredStateConfiguration`.
   - WinGet [version v1.6.2631 or later](https://github.com/microsoft/winget-cli/releases). (You'll likely have this one already)
 * Open a PowerShell 7 instance and navigate to the sample scripts from PowerToys (`src/dsc/Microsoft.PowerToys.Configure/examples/`).
   - [ ] Run `winget configure .\disableAllModules.dsc.yaml`. Open PowerToys Settings and verify all modules are disabled.
   - [ ] Run `winget configure .\enableAllModules.dsc.yaml`. Open PowerToys Settings and verify all modules are enabled.
   - [ ] Run `winget configure .\configureLauncherPlugins.dsc.yaml`. Open PowerToys Settings and verify all PowerToys Run plugins are enabled, and the Program plugin is not global and its Activation Keyword has changed to "P:".
   - [ ] Run `winget configure .\configuration.dsc.yaml`. Open PowerToys Settings the Settings have been applied. File Locksmith is disabled. Shortcut Guide is disabled with an overlay opacity set to 50. FancyZones is enabled with the Editor hotkey set to "Shift+Ctrl+Alt+F".
   - [ ] If you run a winget configure command above and PowerToys is running, it will eventually close and automatically reopen after the configuration process is done.
   - [ ] If you run a winget configure command above and PowerToys is not running, it won't automatically reopen after the configuration process is done.

## Advanced Paste
  NOTES:
    When using Advanced Paste, make sure that window focused while starting/using Advanced paste is text editor or has text input field focused (e.g. Word).
 * Paste As Plain Text
   - [ ] Copy some rich text (e.g word of the text is different color, another work is bold, underlined, etd.).
   - [ ] Paste the text using standard Windows Ctrl + V shortcut and ensure that rich text is pasted (with all colors, formatting, etc.)
   - [ ] Paste the text using Paste As Plain Text activation shortcut and ensure that plain text without any formatting is pasted.
   - [ ] Paste again the text using standard Windows Ctrl + V shortcut and ensure the text is now pasted plain without formatting as well.
   - [ ] Copy some rich text again.
   - [ ] Open Advanced Paste window using hotkey, click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
   - [ ] Copy some rich text again.
   - [ ] Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
 * Paste As Markdown
   - [ ] Open Settings and set Paste as Markdown directly hotkey
   - [ ] Copy some text (e.g. some HTML text - convertible to Markdown)
   - [ ] Paste the text using set hotkey and confirm that pasted text is converted to markdown
   - [ ] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
   - [ ] Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
   - [ ] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
   - [ ] Open Advanced Paste window using hotkey, press Ctrl + 2 and confirm that pasted text is converted to markdown
 * Paste As JSON
   - [ ] Open Settings and set Paste as JSON directly hotkey
   - [ ] Copy some XML or CSV text (or any other text, it will be converted to simple JSON object)
   - [ ] Paste the text using set hotkey and confirm that pasted text is converted to JSON
   - [ ] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
   - [ ] Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
   - [ ] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
   - [ ] Open Advanced Paste window using hotkey, press Ctrl + 3 and confirm that pasted text is converted to markdown
 * Paste as custom format using AI
   - [ ] Open Settings, navigate to Enable Paste with AI and set OpenAI key.
   - [ ] Copy some text to clipboard. Any text.
   - [ ] Open Advanced Paste window using hotkey, and confirm that Custom intput text box is now enabled. Write "Insert smiley after every word" and press Enter. Observe that result preview shows coppied text with smileys between words. Press Enter to paste the result and observe that it is pasted.
   - [ ] Open Advanced Paste window using hotkey. Input some query (any, feel free to play around) and press Enter. When result is shown, click regenerate button, to see if new result is generated. Select one of the results and paste. Observe that correct result is pasted.
   - [ ] Create few custom actions. Set up hotkey for custom actions and confirm they work. Enable/disable custom actions and confirm that the change is reflected in Advanced Paste UI - custom action is not listed. Try different ctrl + <num> in-app shortcuts for custom actions. Try moving custom actions up/down and confirm that the change is reflected in Advanced Paste UI.
   - [ ] Open Settings and disable Custom format preview. Open Advanced Paste window with hotkey, enter some query and press enter. Observe that result is now pasted right away, without showing the preview first.
   - [ ] Open Settings and Disable Enable Paste with AI. Open Advanced Paste window with hotkey and observe that Custom Input text box is now disabled.
 * Clipboard History
   - [ ] Open Settings and Enable clipboard history (if not enabled already). Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry. Check OS clipboard history (Win+V), and confirm that the same entry no longer exist.
   - [ ] Open Advanced Paste window with hotkey, click Clipboard history, and click any entry (but first). Observe that entry is put on top of clipboard history. Check OS clipboard history (Win+V), and confirm that the same entry is on top of the clipboard.
   - [ ] Open Settings and Disable clipboard history. Open Advanced Paste window with hotkey and observe that Clipboard history button is disabled.
 * Disable Advanced Paste, try different Advanced Paste hotkeys and confirm that it's disabled and nothing happens.

## New+
 * Enable New+ in Settings.
   - [ ] Verify NewPlus menu is in Explorer context menu. (Windows 11 tier 1 context menu only. May need Explorer restart.)
 * Disable New+ in Settings.
   - [ ] Verify NewPlus menu is not in Explorer context menu.
 * Choose a different path for template folder.
   - [ ] Verify the folder is created and empty.
   - [ ] Copy a file to the templates folder, verify it's added to the New+ context menu and that if you select it the file is created.
   - [ ] Copy a folder with files inside to the templates folder, verify it's added to the New+ context menu and that if you select it the folder and files inside are created.
   - [ ] Delete all files and folders from inside the templates folder. Verify that no templates are available in the context menu.
   - [ ] Disable and re-Enable New+ while the templates folder is still empty. Verify the default templates were copied over and are available in the context menu.
 * Test some Settings:
   - [ ] Test the "Hide template filename extension" option in Settings.
   - [ ] Test the "Hide template filename starting digits, spaces and dots" option in Settings.

## Telemetry Epilogue
 * After finishing your tests, go to General settings, press Diagnostic data viewer and check if you have xml files for the utilities you've tested and if it looks like the events in those xml files were generated by the actions you did with the utilities you've tested.

