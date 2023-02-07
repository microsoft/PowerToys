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

## Functional tests

 Regressions:
 - [ ] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038
 - [ ] https://github.com/microsoft/PowerToys/issues/1524

## Localization
 Change the Windows language to a language different than English. Then verify if the following screens change their language:
 - [ ] System tray menu items
 - [ ] Settings
 - [ ] OOBE (What's new)
 - [ ] Keyboard Manager Editor
 - [ ] Color Picker (check the tooltips)
 - [ ] FancyZones Editor
 - [ ] Power Rename (new WinUI 3 may not be localized)
 - [ ] PowerToys Run ("Start typing" string is localized, for example)
 - [ ] Image Resizer
 - [ ] Shortcut Guide (Windows controls are localized)
 - [ ] File Explorer menu entries for Image Resizer, Power Rename and FileLocksmith
 - [ ] Hosts File Editor
 - [ ] File Locksmith

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

## Color Picker
* Enable the Color Picker in settings and ensure that the hotkey brings up Color Picker
  - [ ] when PowerToys is running unelevated on start-up
  - [ ] when PowerToys is running as admin on start-up
  - [ ] when PowerToys is restarted as admin, by clicking the restart as admin button in the settings
- [ ] Change `Activate Color Picker shortcut` and check the new shortcut is working
- [ ] Try all three `Activation behavior`s(`Color Picker with editor mode enabled`, `Editor`, `Color Picker only`)
- [ ] Change `Color format for clipboard` and check if the correct format is copied from the Color picker
- [ ] Try to copy color formats to the clipboard from the Editor
- [ ] Check `Show color name` and verify if color name is shown in the Color picker
- [ ] Enable one new format, disable one existing format, reorder enabled formats and check if settings are populated to the Editor
- [ ] Select a color from the history in the Editor
- [ ] Remove color from the history in the Editor
- [ ] Open the Color Picker from the Editor
- [ ] Open Adjust color from the Editor
- [ ] Check Color Picker logs for errors

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
- [ ] `Keep windows in their zones when the screen resolution changes` off, snap a window to a zone, change the screen resolution or scaling, verify window didn't change its size and position.

Enable `During zone layout changes, windows assigned to a zone will match new size/positions` and prepare layouts with 1 and 3 zones where zone size/positions are different.
- [ ] Snap a window to zone 1, change the layout, verify window changed its size/position.
- [ ] Snap a window to zone 3, change the layout, verify window didn't change its size/position because another layout doesn't have a zone with this zone number.
- [ ] Snap a window to zones 1-2, change the layout, verify window changed its size/position to fit zone 1.
- [ ] Snap a window to zones 1-2, change the layout (the window will be snapped to zone 1), then return back to the previous layout, verify the window snapped to 1-2 zones.
- [ ] Disable `During zone layout changes, windows assigned to a zone will match new size/positions`, snap window to zone 1, change layout, verify window didn't change its size/position

Enable `Move newly created windows to their last known zone`, snap a window to a zone, close this window.
- [ ] Reopen the window on the same monitor, verify it snapped to its zone.
- [ ] Reopen the window on another monitor, verify it snapped to its zone on the monitor where it was snapped.
- [ ] Snap the window of the same app to a zone on another monitor, close and reopen the window, verify it snapped to its zone on the active monitor.
- [ ] Disable `Move newly created windows to their last known zone`, reopen the snapped window, verify it's opened in its default position or in the position where it was closed after unsnapping.

- [ ] Snap a window, turn off FancyZones, move that window, turn FZ on. Verify window returned to its zone.
- [ ] Move unsnapped window to a secondary monitor, switch virtual desktop and return back. Verify window didn't change its position and size.
- [ ] Snap a window, then resize it (it's still snapped, but doesn't fit the zone). Switch the virtual desktop and return back, verify window didn't change its size.

Enable `Move newly created windows to the current active monitor`.
- [ ] Open a window that wasn't snapped anywhere, verify it's opened on the active monitor.
- [ ] Open a window that was snapped on the current virtual desktop, verify it's opened in its zone.
- [ ] Open a window that was snapped on another virtual desktop, verify it's opened on the active monitor.

- [ ] Enable `Allow popup windows snapping`, snap Teams, verify a popup message window appears in the zone.
- [ ] Disable `Allow popup windows snapping`, snap Teams, verify a popup window appears in its usual position.
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

## Image Resizer
- [ ] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [ ] Enable the Image Resizer and check that `Resize images` is present in the context menu. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
- [ ] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [ ] Resize one image
- [ ] Resize multiple images
- [ ] Open the image resizer to resize a `.gif` file and verify the "Gif files with animations may not be correctly resized." warning appears.

- [ ] Resize images with `Fill` option
- [ ] Resize images with `Fit` option
- [ ] Resize images with `Stretch` option

- [ ] Resize images using dimension: Centimeters
- [ ] Resize images using dimension: Inches
- [ ] Resize images using dimension: Percents
- [ ] Resize images using dimension: Pixels

- [ ] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [ ] Check `Use original date modified` and verify that modified date is not changed for resized images. Take into account that `Resize the original pictures(don't create copy)` should be selected
- [ ] Check `Make pictures smaller but not larger` and verify that smaller pictures are not resized
- [ ] Check `Resize the original pictures (don't create copies)` and verify that the original picture is resized and a copy is not created
- [ ] Uncheck `Ignore the orientation of pictures` and verify that swapped width and height will actually resize a picture if the width is not equal to the height

## Keyboard Manager

UI Validation:

  - [ ] In Remap keys, add and remove rows to validate those buttons. While the blank rows are present, pressing the OK button should result in a warning dialog that some mappings are invalid.
  - [ ] Using only the Type buttons, for both the remap windows, try adding keys/shortcuts in all the columns. The right-side column in both windows should accept both keys and shortcuts, while the left-side column will accept only keys or only shortcuts for Remap keys and Remap shortcuts respectively. Validate that the Hold Enter and Esc accessibility features work as expected.
  - [ ] Using the drop downs try to add key to key, key to shortcut, shortcut to key and shortcut to shortcut remapping and ensure that you are able to select remapping both by using mouse and by keyboard navigation.
  - [ ] Validate that remapping can be saved by pressing the OK button and re-opening the windows loads existing remapping.

Remapping Validation:

For all the remapping below, try pressing and releasing the remapped key/shortcut and pressing and holding it. Try different behaviors like releasing the modifier key before the action key and vice versa.
  - [ ] Test key to key remapping
    - A->B
    - Ctrl->A
    - A->Ctrl
    - Win->B (make sure Start menu doesn't appear accidentally)
    - B->Win (make sure Start menu doesn't appear accidentally)
    - A->Disable
    - Win->Disable
  - [ ] Test key to shortcut remapping
    - A->Ctrl+V
    - B->Win+A
  - [ ] Test shortcut to shortcut remapping
    - Ctrl+A->Ctrl+V
    - Win+A->Ctrl+V
    - Ctrl+V->Win+A
    - Win+A->Win+F
  - [ ] Test shortcut to key remapping
    - Ctrl+A->B
    - Ctrl+A->Win
    - Win+A->B
  * Test app-specific remaps
    - [ ] Similar remaps to above with Edge (entered as `msedge`), VSCode (entered as `code`) and cmd. For cmd try admin and non-admin (requires PT to run as admin)
    - [ ] Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - [ ] Test switching between remapping while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

## PowerRename
- [ ] Check if disable and enable of the module works. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
- [ ] Check that with the `Show icon on context menu` icon is shown and vice versa.
- [ ] Check if `Appear only in extended context menu` works.
- [ ] Enable/disable autocomplete.
- [ ] Enable/disable `Show values from last use`.
* Select several files and folders and check PowerRename options:
    - [ ] Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
    - [ ] Exclude Folders/Files/Subfolder Items (could be selected several)
    - [ ] Item Name/Extension Only (one at the time)
    - [ ] Enumerate Items
    - [ ] Case Sensitive
    - [ ] Match All Occurrences. If checked, all matches of text in the `Search` field will be replaced with the Replace text. Otherwise, only the first instance of the `Search` for text in the file name will be replaced (left to right).
    * Use regular expressions
        - [ ] Search with an expression (e.g. `(.*).png`)
        - [ ] Replace with an expression (e.g. `foo_$1.png`)
        - [ ] Replace using file creation date and time (e.g. `$hh-$mm-$ss-$fff` `$DD_$MMMM_$YYYY`)
        - [ ] Turn on `Use Boost library` and test with Perl Regular Expression Syntax (e.g. `(?<=t)est`)
    * File list filters.
        - [ ] In the `preview` window uncheck some items to exclude them from renaming.
        - [ ] Click on the `Renamed` column to filter results.
        - [ ] Click on the `Original` column to cycle between checked and unchecked items.

## PowerToys Run

 * Enable PT Run in settings and ensure that the hotkey brings up PT Run
   - [ ] when PowerToys is running unelevated on start-up
   - [ ] when PowerToys is running as admin on start-up
   - [ ] when PowerToys is restarted as admin, by clicking the restart as admin button in settings.
 * Check that each of the plugins is working:
   - [ ] Program - launch a Win32 application
   - [ ] Program - launch a Win32 application as admin
   - [ ] Program - launch a packaged application
   - [ ] Calculator - ensure a mathematical input returns a correct response and is copied on enter.
   - [ ] Windows Search - open a file on the disk.
   - [ ] Windows Search - find a file and copy file path.
   - [ ] Windows Search - find a file and open containing folder.
   - [ ] Shell - execute a command. Enter the action keyword `>`, followed by the query, both with and without space (e.g. `> ping localhost`).
   - [ ] Folder - Search and open a sub-folder on entering the path.
   - [ ] Uri - launch a web page on entering the uri.
   - [ ] Window walker - Switch focus to a running window.
   - [ ] Service - start, stop, restart windows service. Enter the action keyword `!` to get the list of services.
   - [ ] Registry - navigate through the registry tree and open registry editor. Enter the action keyword `:` to get the root keys.
   - [ ] Registry - navigate through the registry tree and copy key path.
   - [ ] System - test `lock`.
   - [ ] System - test `empty recycle bin`.
   - [ ] System - test `shutdown`.

 - [ ] Disable PT Run and ensure that the hotkey doesn't bring up PT Run.

 - [ ] Test tab navigation.

 * Test Plugin Manager
   - [ ] Enable/disable plugins and verify changes are picked up by PT Run
   - [ ] Change `Direct activation phrase` and verify changes are picked up by PT Run
   - [ ] Change `Include in global result` and verify changes picked up by PT Run
   - [ ] Clear `Direct activation phrase` and uncheck `Include in global result`. Verify a warning message is shown.
   - [ ] Disable all plugins and verify the warning message is shown.

## Shortcut Guide
 * Run PowerToys as user:
   - [ ] Verify `Win + Shift + /` opens the guide
   - [ ] Change the hotkey to a different shortcut (e.g. `Win + /`) and verify it works
   - [ ] Set Shortcut Guide to start with a Windows key press and verify it works.
 * Restore the `Win + Shift + /` hotkey.
   - [ ] Open the guide and close it pressing `Esc`
   - [ ] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [ ] Verify `Win + Shift + /` opens the guide
   - [ ] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

## OOBE
 * Quit PowerToys
 * Delete %localappdata%\Microsoft\PowerToys
 - [ ] Start PowerToys and verify OOBE opens
 * Change version saved on `%localappdata%\Microsoft\PowerToys\last_version.txt`
 - [ ] Start PowerToys and verify OOBE opens in the "What's New" page
 * Visit each OOBE section and for each section:
   - [ ] open the Settings for that module
   - [ ] verify the Settings work as expected (toggle some controls on/off etc.)
   - [ ] close the Settings
   - [ ] if it's available, test the `Launch module name` button
 * Close OOBE
 - [ ] Open the Settings and from the General page open OOBE using the `Welcome to PowerToys` link

## Mouse Utils

Find My Mouse:
  * Enable FindMyMouse. Then, without moving your mouse:
    - [ ] Press Left Ctrl twice and verify the overlay appears.
    - [ ] Press any other key and verify the overlay disappears.
    - [ ] Press Left Ctrl twice and verify the overlay appears.
    - [ ] Press a mouse button and verify the overlay disappears.
  * Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice.
  * Enable FindMyMouse. Then, without moving your mouse:
    - [ ] Press Left Ctrl twice and verify the overlay appears.
  * Enable the "Do not activate on game mode" option. Start playing a game that uses CG native full screen.
    - [ ] Verify the overlay no longer appears when you press Left Ctrl twice.
  * Disable the "Do not activate on game mode" option. Start playing the same game.
    - [ ] Verify the overlay appears when you press Left Ctrl twice. (though it'll likely minimize the game)
  * Test the different settings and verify they apply:
    - [ ] Overlay opacity
    - [ ] Background color
    - [ ] Spotlight color
    - [ ] Spotlight radius
    - [ ] Spotlight initial zoom (1x vs 9x will show the difference)
    - [ ] Animation duration
    - [ ] Change activation method to shake and activate by shaking your mouse pointer
    - [ ] Excluded apps

Mouse Highlighter:
  * Enable Mouse Highlighter. Then:
    - [ ] Press the activation shortcut and press left and right click somewhere, verifying the hightlights are applied.
    - [ ] With left mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [ ] With right mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [ ] Press the activation shortcut again and verify no highlights appear when the mouse buttons are clicked.
    - [ ] Disable Mouse Highlighter and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [ ] Change activation shortcut and test it
    - [ ] Left button highlight color
    - [ ] Right button highlight color
    - [ ] Opacity
    - [ ] Radius
    - [ ] Fade delay
    - [ ] Fade duration

Mouse Pointer Crosshairs:
  * Enable Mouse Pointer Crosshairs. Then:
    - [ ] Press the activation shortcut and verify the crosshairs appear, and that they follow the mouse around.
    - [ ] Press the activation shortcut again and verify the crosshairs disappear.
    - [ ] Disable Mouse Pointer Crosshairs and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [ ] Change activation shortcut and test it
    - [ ] Crosshairs color
    - [ ] Crosshairs opacity
    - [ ] Crosshairs center radius
    - [ ] Crosshairs thickness
    - [ ] Crosshairs border color
    - [ ] Crosshairs border size

## VCM
 - [ ] Check "Hide toolbar when both camera and microphone are unmuted" and verify that it works
 - [ ] Uncheck it, mute the microphone with the hotkey and make sure the toolbar doesn't hide after a timeout
 - [ ] Go to some video conference application settings, e.g. meet.google.com, Microsoft Teams, Skype. "Select PowerToys VideoConference Mute" camera as an active device and try to mute it with a hotkey
 - [ ] Go to Control Panel -> Sound -> Recording -> select default mic -> open its properties -> Levels.  Now mute the microphone with a corresponding hotkey and verify that mute icon on the right side of volume slider reflects its muted status.
 - [ ] Verify that changing "toolbar position" setting works 
 - [ ] Select an overlay image and verify that muting camera now shows the image instead of black screen. (Don't forget to restart the application which uses the camera).
 - [ ] Try to select an overlay image when PT process is elevated.

## Awake
 - [ ] Try out the features and see if they work, no list at this time.

## Always on Top
 - [ ] Pin/unpin a window, verify it's topmost/not topmost.
 - [ ] Pin/unpin a window, verify the border appeared/disappeared.
 - [ ] Switch virtual desktop, verify border doesn't show up on another desktop.
 - [ ] Minimize and maximize pinned window, verify the border looks as usual.
 - [ ] Change border color and thickness.
 - [ ] Verify if sound is played according to the sound setting.
 - [ ] Exclude app, try to pin it.
 - [ ] Exclude already pinned app, verify it was unpinned.
 - [ ] Try to pin the app in the Game Mode.

### Screen Ruler
 * Enable Screen Ruler. Then:
   - [ ] Press the activation shortcut and verify the toolbar appears.
   - [ ] Press the activation shortcut again and verify the toolbar disappears.
   - [ ] Disable Screen Ruler and verify that the activation shortuct no longer activates the utility.
   - [ ] Enable Screen Ruler and press the activation shortcut and verify the toolbar appears.
   - [ ] Select the close button in the toolbar and verify it closes the utility.
 * With Screen Ruler enabled and activated:
   - [ ] Use the Bounds utility to measure a zone by dragging with left-click. Verify right click dismisses the utility and that the measurement was copied into the clipboard.
   - [ ] Use the Spacing utility to measure something and verify that left-click copies the measurement to the clipboard. Verify that right-click dismisses the utility.
   - [ ] Use the Horizontal Spacing utility to measure something and verify that left-click copies the measurement to the clipboard. Verify that right-click dismisses the utility.
   - [ ] Use the Vertical Spacing utility to measure something and verify that left-click copies the measurement to the clipboard. Verify that right-click dismisses the utility.
   - [ ] While using a Spacing utility, verify that using the mouse scroll wheel will adjust pixel color tolerance while measuring.
   - [ ] Open mspaint and draw 1px-thick straight line, also click with a pencil to draw a single pixel. In any Spacing mode, verify that one of line's dimension is 1, and pixel's dimensions are 1x1.
 * In a multi-monitor setup with different dpis on each monitor:
   - [ ] Verify that the utilities work well on each monitor, with continuous mode on and off.
   - [ ] Without any window opened and a solid color as your background, verify the horizontal spacing matches the monitor's pixel width.
   - [ ] Move your mouse back and forth around the edge of two monitors really quickly in each mode - verify nothing is broken.
   
 * Test the different settings and verify they are applied:
   - [ ] Activation shortcut
   - [ ] Continous mode
   - [ ] Per color channel edge detection
   - [ ] Pixel tolerance for edge detection
   - [ ] Draw feet on cross
   - [ ] Line color

### Quick Accent
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

### Text Extractor
 * Enable Text Extractor. Then:
   - [ ] Press the activation shortcut and verify the overlay appears.
   - [ ] Press Escape and verify the overlay disappears.
   - [ ] Press the activation shortcut and verify the overlay appears.
   - [ ] Right-click and select Cancel. Verify the overlay disappears.
   - [ ] Disable Text Extractor and verify that the activation shortuct no longer activates the utility.
 * With Text Extractor enabled and activated:
   - [ ] Try to select text and verify it is copied to the clipboard.
   - [ ] Try to select a different OCR language by right-clicking and verify the change is applied.
 * In a multi-monitor setup with different dpis on each monitor:
   - [ ] Verify text is correctly captured on all monitors.
 * Test the different settings and verify they are applied:
   - [ ] Activation shortcut
   - [ ] OCR Language

### Hosts File Editor
 * Launch Host File Editor:
   - [ ] Verify the application exits if "Quit" is clicked on the initial warning.
   - [ ] Launch Host File Editor again and click "Accept". The module should not close. Open the hosts file (`%WinDir%\System32\Drivers\Etc`) in a text editor that auto-refreshes so you can see the changes applied by the editor in real time. (VSCode is an editor like this, for example)
   - [ ] Enable and disable lines and verify they are applied to the file.
   - [ ] Add a new entry and verify it's applied.
   - [ ] Try to filter for lines and verify you can find them.
   - [ ] Click the "Open hosts file" button and verify it opens in your default editor. (likely Notepad)
 * Test the different settings and verify they are applied:
   - [ ] Launch as Administrator.
   - [ ] Show a warning at startup.
   - [ ] Additional lines position.

### File Locksmith
 * Start the PowerToys installer executable and let it stay in the initial screen.
   - [ ] Right-click the executable file, select "What's using this file?" and verify it shows up. (2 entries will show, since the installer starts two processes)
   - [ ] End the tasks in File Locksmith UI and verify that closes the installer.
   - [ ] Start the installer executable again and press the Refresh button in File Locksmith UI. It should find new processes using the files.
   - [ ] Close the installer window and verify the processes are delisted from the File Locksmith UI. Close the window
 * Start the PowerToys installer executable again and let it stay in the initial screen.
   - [ ] Right click the directory where the executable is located, select "What's using this file?" and verify it shows up. 
   - [ ] Right click the drive where the executable is located, select "What's using this file?" and verify it shows up. You can close the PowerToys installer now.
 * Restart PowerToys as admin.
   - [ ] Right click "Program Files", select "What's using this file?" and verify "PowerToys.exe" doesn't show up.
   - [ ] Press the File Locksmith "Restart as an administrator" button and verify "PowerToys.exe" shows up.
 - [ ] Right-click the drive where Windows is installed, select "What's using this file?" and scroll down and up, verify File Locksmith doesn't crash with all those entries being shown. Repeat after clicking the File Locksmith "Restart as an administrator" button.
 - [ ] Disable File Locksmith in Settings and verify the context menu entry no longer appears.

### GPO
 * Copy the "PowerToys.admx" file to your Policy Definition template folder. (Example: C:\Windows\PolicyDefinitions) and copy the "PowerToys.adml" file to the matching language folder in your Policy Definition folder. (Example: C:\Windows\PolicyDefinitions\en-US)
   - [ ] Open the "Local Group Policy Editor" on Windows and verify there is a "Microsoft PowerToys" folder in Administrative Templates for both Computer Configuration and User Configuration.
 * In GPO, disable a module that can run as a standalone (FancyZones sounds good for this). Restart PowerToys.
   - [ ] Verify the module is not enabled.
   - [ ] Open settings and verify the module is not enabled and you can't enable it.
   - [ ] Try to open FancyZones Editor directly from the install folder and verify it doesn't run and adds a message to the log saying it didn't run because of GPO.
   - [ ] Verify the module can't be launched from the quick launcher system tray flyout launcher screen (FancyZones editor in this case).
   - [ ] Verify the module can't be enabled/disabled from the quick launcher system tray flyout.
 * In GPO, enable a module that can run as a standalone (FancyZones sounds good for this). Restart PowerToys.
   - [ ] Verify the module is enabled.
   - [ ] Open settings and verify the module is enabled and you can't disable it.
   - [ ] Verify the module can't be enabled/disabled from the quick launcher system tray flyout.
 * In GPO, try to set different settings in the Computer and User Configurations for a PowerToy. Restart PowerToys.
   - [ ] Verify that the setting in Computer Configuration has priority over the setting in User Configuration.
 * In GPO, disable a module that has a context menu entry (File Locksmith sounds good for this). Restart PowerToys.
   - [ ] Verify the module is not enabled. (No context menu entry)
   - [ ] Open settings and verify the module is not enabled and you can't enable it.
   - [ ] Try to open File Locksmith directly from the install folder and verify it doesn't run and adds a message to the log saying it didn't run because of GPO.
 * In GPO, disable a module that is a Preview Handler (Markdown Preview is good for this). Restart PowerToys.
   - [ ] Verify the module is not enabled. (Markdown files won't appear in the preview pane)
   - [ ] Open settings and verify the module is not enabled and you can't enable it.
 * Remember to reset all you Settings to Not Configured after the tests, both in Conputer and User Configurations.
