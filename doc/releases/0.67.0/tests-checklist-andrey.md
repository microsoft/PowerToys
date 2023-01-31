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
 - [ ] File Explorer menu entries for Image Resizer and Power Rename
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
- [ ] Switch between `Allow zones to span across monitors` on and off. Verify that layouts are applied correctly in both cases.
- [ ] Change zone colors and opacity.
- [ ] Exclude some apps, verify that they're not applicable to a zone.
- [ ] Disable spacing on any grid layout, verify that there is no space between zones while dragging a window.
- [ ] Launch PT in user mode, try to assign a window with administrator privileges to a zone. Verify the notification is shown.
- [ ] Launch PT in administrator mode, assign a window with administrator privileges.
- [ ] Create virtual desktop, verify that there are the same layouts as applied to the previous virtual desktop.
- [ ] After creating a virtual desktop apply another layout or edit the applied one. Verify that the other virtual desktop layout wasn't changed.
- [ ] Delete an applied custom layout in the Editor, verify that there is no layout applied instead of it.
* Open `Task view` , right-click on the window, check the `Show this window on all desktops` or the `Show windows from this app on all desktops` option to turn it on.
    - [ ] Turn Show this window on all desktops on, verify you can snap this window to a zone.
    - [ ] Turn Show windows from this app on all desktops on, verify you can snap this window to a zone.
* Switch between layouts with quick keys.
    - [ ] Switch with `Win` + `Ctrl` + `Alt` + `key`
    - [ ] Switch with just a key while dragging a window.
* Change screen resolution or scaling.
    - [ ] Assign grid layout, verify that the assigned layout fits the screen.
      NOTE: canvas layout could not fit the screen if it was created on a monitor with a different resolution.
- [ ] Apply 2 windows to the same zone, verify that window swithing works (`Win + PgUp/PgDown`)
- [ ] Verify that window switching still works after switching to another virtual desktop and back. 
- [ ] Disable FZ
- [ ] Re-enable FZ, verify that everything is in the same state as it was before disabling.

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

## VCM
 - [ ] Check "Hide toolbar when both camera and microphone are unmuted" and verify that it works
 - [ ] Uncheck it, mute the microphone with the hotkey and make sure the toolbar doesn't hide after a timeout
 - [ ] Go to some video conference application settings, e.g. meet.google.com, Microsoft Teams, Skype. "Select PowerToys VideoConference Mute" camera as an active device and try to mute it with a hotkey
 - [ ] Go to Control Panel -> Sound -> Recording -> select default mic -> open its properties -> Levels.  Now mute the microphone with a corresponding hotkey and verify that mute icon on the right side of volume slider reflects its muted status.
 - [ ] Verify that changing "toolbar position" setting works 
 - [ ] Select an overlay image and verify that muting camera now shows the image instead of black screen. (Don't forget to restart the application which uses the camera).
 - [ ] Try to select an overlay image when PT process is elevated.
