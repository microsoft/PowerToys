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
 - [x] verify the settings are preserved and FancyZones configuration is still the same

## Functional tests

 Regressions:  
 - [x] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038
 - [x] https://github.com/microsoft/PowerToys/issues/1524

## General Settings

**Admin mode:**
 - [x] restart as admin and verify FZ can snap an elevated window
 - [x] restart PT and verify it now runs as user
 * restart as admin and set "Always run as admin"
 - [x] restart PT and verify it still runs as admin
 * if it's not on, turn on "Run at startup"
 - [x] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [x] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [x] turn off all the modules and verify all module are off
 - [x] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [x] turn on all the module, all module are now working
 - [x] restart PT and verify that all module are still on in the settings page and they are actually working

**Elevated app notification:**
 - run PT as a user
 - open an elevated app (i.e. Task Manager)
 - shift-drag the elevated app window
 - [x] verify that a notification appears
 - restart PT as admin
 - shift-drag the elevated app window
 - [x] verify the notification doesn't appear

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

- [x] Open editor from the settings
- [x] Open editor with a shortcut
- [x] Create a new layout (grid and canvas)
- [x] Duplicate a template and a custom layout
- [x] Delete layout
- [x] Edit templates (number of zones, spacing, distance to highlight adjacent zones). Verify after reopening the editor that saved settings are kept the same.
- [x] Edit canvas layout: zones size and position, create or delete zones. 
- [x] Edit grid layout: split, merge, resize zones.
- [x] Check `Save and apply` and `Cancel` buttons behavior after editing.
- [x] Assign a layout to each monitor.
- [x] Assign keys to quickly switch layouts (custom layouts only), `Win + Ctrl + Alt + number`.


## FancyZones
- [x] Switch between `Allow zones to span across monitors` on and off. Verify that layouts are applied correctly in both cases. 
- [x] Change zone colors and opacity.
- [x] Exclude some apps, verify that they're not applicable to a zone.
- [x] Launch PT in user mode, try to assign a window with administrator privileges to a zone. Verify the notification is shown.
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
    - [x] Edit grid layout, verify that split, merge and resize zones works as expected.
- [x] Disable FZ
- [x] Reenable FZ, verify that everything is in the same state as it was before disabling.

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [ ] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\runner\svgs
   - [ ] verify Preview Pane works for the SVG files
   - [ ] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
 * Running as admin:
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [ ] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\runner\svgs
   - [ ] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached
   - [ ] copy and paste one of the SVG file and verify the new file show the generic SVG icon

## Image Resizer
- [ ] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [ ] Enable the Image Resizer and check that `Resize images` is present in the context menu
- [ ] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [ ] Resize one image
- [ ] Resize multiple images

- [ ] Resize images with `Fill` option
- [ ] Resize images with `Fit` option
- [ ] Resize images with `Stretch` option

- [ ] Resize images using dimension: Centimeters
- [ ] Resize images using dimension: Inches
- [ ] Resize images using dimension: Percents
- [ ] Resize images using dimension: Pixels

- [ ] Try to resize wmf image. Resized image has to be of fallback encoder type
- [ ] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [ ] Check `Use original date modified` and verify that modified date is not changed for resized images
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
    - [ ] Similar remaps to above with Edge, VSCode (entered as code) and cmd. For cmd try admin and non-admin (requires PT to run as admin)
    - [ ] Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - [ ] Test switching between remapping while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

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
        - [ ] Replace using file creation date and time (e.g. $hh-$mm-$ss-$fff $DD_$MMMM_$YYYY)
        - [ ] Turn on `Use Boost library` and test with Perl Regular Expression Syntax.
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
   - [ ] Program - launch a Win32 application and a packaged application
   - [ ] Calculator - ensure a mathematical input returns a correct response and is copied on enter.
   - [ ] Indexer - open a file on the disk.
   - [ ] Shell - execute a command. Enter the action keyword `>`, followed by the query, both with and without space.
   - [ ] Folder - Search and open a sub-folder on entering the path.
   - [ ] Uri - launch a web page on entering the uri.
   - [ ] Window walker - Switch focus to a running window.
   - [ ] Service - start, stop, restart windows service. Enter the action keyword `!` to get the list of services.
   - [ ] Registry - navigate through the registry tree, copy key path, open registry editor. Enter the action keyword `:` to get the root keys.
   - [ ] System - test shutdown, sleep, lock, sign out, restart, hibernate, empty recycle bin.
 
 * Validate that the context menu items and corresponding shortcuts work as expected for each of the plugins
   - [ ] Run as admin
   - [ ] Copy file path
   - [ ] Open in console
   - [ ] Open containing folder
   - [ ] Start, Stop, Restart service
   - [ ] Open Services
   - [ ] Copy key name
   - [ ] Open key in registry editor

 - [ ] Disable PT Run and ensure that the hotkey doesn't bring up PT Run.
 
 - [ ] Test tab navigation. 

 * Test Plugin Manager
   - [ ] Enable/disable plugins and verify changes are populated to PT Run
   - [ ] Change `Direct activation phrase` and verify changes are populated to PT Run
   - [ ] Change `Include in global result` and verify changes are populated to PT Run
   - [ ] Clear `Direct activation phrase` and uncheck `Include in global result`. Verify a warning message
   - [ ] Disable all plugins and verify the warning message
  
## Shortcut Guide
 * Run PowerToys as user:
   - [ ] Verify holding the `Win` key opens the guide
   - [ ] Verify `Win + ?` opens the guide
   * In the Settings change the duration from 900ms to 200ms
   - [ ] Verify the guide open after quicker when holding the `Win` key
   * Restore the 900ms duration
 * Run PowerToys as admin:
   - [ ] Open an elevated app and keep it on foreground
   - [ ] Verify holding the `Win` key opens the guide
   - [ ] Verify `Win + ?` opens the guide
 * Run PowerToys as user
    - [ ] Verify the taskbar shortcut `Win + digit` works
    - [ ] Verify the first four shortcuts work

### OOBE
 * Quit PowerToys
 * Delete %localappdata%\Microsoft\PowerToys
 - [ ] Start PowerToys and verify OOBE opens
 * Visit each OOBE section and for each section:
   - [ ] open the Settings for that module
   - [ ] verify the Settings work as expected (toggle some controls on/off etc.)
   - [ ] close the Settings
   - [ ] if it's available, test the `Launch module name` button
 * Close OOBE
 - [ ] Open the Settings and from the General page open OOBE using the `Welcome to PowerToys` link
