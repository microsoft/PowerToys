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
 Change the Winodws language to a language different than English. Then verify if the following screens change their language:
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

## Color Picker
* Enable the Color Picker in settings and ensure that the hotkey brings up Color Picker
  - [x] when PowerToys is running unelevated on start-up
  - [x] when PowerToys is running as admin on start-up
  - [x] when PowerToys is restarted as admin, by clicking the restart as admin button in the settings
- [x] Change `Activate Color Picker shortcut` and check the new shortcut is working
- [x] Try all three `Activation behavior`s(`Color Picker with editor mode enabled`, `Editor`, `Color Picker only`)
- [x] Change `Color format for clipboard` and check if the correct format is copied from the Color picker
- [x] Try to copy color formats to the clipboard from the Editor
- [x] Check `Show color name` and verify if color name is shown in the Color picker
- [x] Enable one new format, disable one existing format, reorder enabled formats and check if settings are populated to the Editor
- [x] Select a color from the history in the Editor
- [x] Remove color from the history in the Editor
- [x] Open the Color Picker from the Editor
- [x] Open Adjust color from the Editor
- [x] Check Color Picker logs for errors

## FancyZones Editor

- [x] Open editor from the settings
- [x] Open editor with a shortcut
- [x] Create a new layout (grid and canvas)
- [x] Duplicate a template and a custom layout
- [x] Delete layout
- [X] Edit templates (number of zones, spacing, distance to highlight adjacent zones). Verify after reopening the editor that saved settings are kept the same.
- [x] Edit canvas layout: zones size and position, create or delete zones.
- [x] Edit grid layout: split, merge, resize zones.
- [x] Check `Save and apply` and `Cancel` buttons behavior after editing.
- [x] Assign a layout to each monitor.
- [x] Assign keys to quickly switch layouts (custom layouts only), `Win + Ctrl + Alt + number`.


## FancyZones
- [x] Switch between `Allow zones to span across monitors` on and off. Verify that layouts are applied correctly in both cases.
- [x] Change zone colors and opacity.
- [x] Exclude some apps, verify that they're not applicable to a zone.
- [X] Launch PT in user mode, try to assign a window with administrator privileges to a zone. Verify the notification is shown.
- [x] Launch PT in administrator mode, assign a window with administrator privileges.
- [x] Create virtual desktop, verify that there are the same layouts as applied to the previous virtual desktop.
- [x] After creating a virtual desktop apply another layout or edit the applied one. Verify that the other virtual desktop layout wasn't changed.
- [x] Delete an applied custom layout in the Editor, verify that there is no layout applied instead of it.
* Switch between layouts with quick keys.
    - [x] Switch with `Win` + `Ctrl` + `Alt` + `key`
    - [x] Switch with just a key while dragging a window.
* Change screen resolution or scaling.
    - [x] Assign grid layout, verify that the assigned layout fits the screen.
      NOTE: canvas layout could not fit the screen if it was created on a monitor with a different resolution.
- [x] Disable FZ
- [x] Re-enable FZ, verify that everything is in the same state as it was before disabling.

* Test layout resetting.
Before testing 
   * Remove all virtual desktops 
   * Remove `CurrentVirtualDesktop` from `\HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\1\VirtualDesktops` 
   * Remove `VirtualDesktopIDs` from `\HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops`


- [x] Test screen locking
   * Set custom layouts on each monitor
   * Lock screen / unplug monitor / plug monitor
   * Verify that layouts weren't reset to defaults
   
- [x] Test restart
   * Set custom layouts on each monitor
   * Restart the computer
   * Verify that layouts weren't reset to defaults

## Image Resizer
- [x] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [x] Enable the Image Resizer and check that `Resize images` is present in the context menu
- [x] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [x] Resize one image
- [x] Resize multiple images
- [x] Open the image resizer to resize a `.gif` file and verify the "Gif files with animations may not be correctly resized." warning appears.

- [x] Resize images with `Fill` option
- [x] Resize images with `Fit` option
- [x] Resize images with `Stretch` option

- [x] Resize images using dimension: Centimeters
- [x] Resize images using dimension: Inches
- [x] Resize images using dimension: Percents
- [x] Resize images using dimension: Pixels

- [x] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [x] Check `Use original date modified` and verify that modified date is not changed for resized images. Take into account that `Resize the original pictures(don't create copy)` should be selected
- [x] Check `Make pictures smaller but not larger` and verify that smaller pictures are not resized
- [x] Check `Resize the original pictures (don't create copies)` and verify that the original picture is resized and a copy is not created
- [x] Uncheck `Ignore the orientation of pictures` and verify that swapped width and height will actually resize a picture if the width is not equal to the height

## PowerRename
- [ ] Check if disable and enable of the module works.
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
   - [x] Verify `Win + Shift + /` opens the guide
   - [x] Change the hotkey to a different shortcut (e.g. `Win + /`) and verify it works
   - [x] Set Shortcut Guide to start with a Windows key press and verify it works.
 * Restore the `Win + Shift + /` hotkey.
   - [x] Open the guide and close it pressing `Esc`
   - [x] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [X] Verify `Win + Shift + /` opens the guide
   - [x] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

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

## VCM
 - [ ] Check "Hide toolbar when both camera and micrphone are unmuted" and verify that it works
 - [ ] Uncheck it, mute the microphone with the hotkey and make sure the toolbar doesn't hide after a timeout
 - [ ] Go to some video conference application settings, e.g. meet.google.com, Microsoft Teams, Skype. "Select PowerToys VideoConference Mute" camera as an active device and try to mute it with a hotkey
 - [ ] Go to Control Panel -> Sound -> Recording -> select default mic -> open its properties -> Levels.  Now mute the microphone with a corresponding hotkey and verify that mute icon on the right side of volume slider reflects its muted status.
 - [ ] Verify that changing "toolbar position" setting works 
 - [ ] Select an overlay image and verify that muting camera now shows the image instead of black screen. (Don't forget to restart the application which uses the camera).
 - [ ] Try to select an overlay image when PT process is elevated. (Currently doesn't work)

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
