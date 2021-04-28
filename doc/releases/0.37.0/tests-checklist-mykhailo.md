## Install tests
 * install a **previous version** on a clean machine (a clean machine doesn't have the `%localappdata%\Microsoft\PowerToys` folder)
 * open the Settings and for each module change at least one option
 * open the FancyZones editor and create two custom layouts:
    * a canvas layout with 2 zones, use unicode chars in the layout's name
    * one from grid template using 4 zones and splitting one zone
    * apply the custom canvas layout to the primary desktop
    * create a virtual desktop and apply the custom grid layout
    * if you have a second monitor apply different templates layouts for the primary desktop and for the second virtual desktop
 * install the new version (it will uninstall the old version and install the new version). In case version of PowerToys is still 0.0.1 delete old version and install new.
 - [X] verify the settings are preserved and FancyZones configuration is still the same

## Functional tests

 Regressions:  
 - [X] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038

## General Settings

**Admin mode:**
 - [X] restart as admin and verify FZ can snap an elevated window
 - [X] restart PT and verify it now runs as user
 * restart as admin and set "Always run as admin"
 - [X] restart PT and verify it still runs as admin
 * if it's not on, turn on "Run at startup"
 - [X] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [X] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [X] turn off all the modules and verify all module are off
 - [X] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [X] turn on all the module, all module are now working
 - [X] restart PT and verify that all module are still on in the settings page and they are actually working

**Elevated app notification:**
 - run PT as a user
 - open an elevated app (i.e. Task Manager)
 - shift-drag the elevated app window
 - [X] verify that a notification appears
 - restart PT as admin
 - shift-drag the elevated app window
 - [X] verify the notification doesn't appear

## Color Picker
* Enable the Color Picker in settings and ensure that the hotkey brings up Color Picker
  - [X] when PowerToys is running unelevated on start-up
  - [X] when PowerToys is running as admin on start-up
  - [X] when PowerToys is restarted as admin, by clicking the restart as admin button in the settings
- [X] Change `Activate Color Picker shortcut` and check the new shortcut is working
- [X] Try all three `Activation behavior`s(`Color Picker with editor mode enabled`, `Editor`, `Color Picker only`)
- [X] Change `Color format for clipboard` and check if the correct format is copied from the Color picker
- [X] Try to copy color formats to the clipboard from the Editor
- [X] Check `Show color name` and verify if color name is shown in the Color picker
- [X] Enable one new format, disable one existing format, reorder enabled formats and check if settings are populated to the Editor
- [X] Select a color from the history in the Editor
- [X] Remove color from the history in the Editor
- [X] Open the Color Picker from the Editor
- [X] Open Adjust color from the Editor
- [X] Check Color Picker logs for errors

## FancyZones Editor

- [X] Open editor from the settings
- [X] Open editor with a shortcut
- [X] Create a new layout (grid and canvas)
- [X] Duplicate a template and a custom layout
- [X] Delete layout
- [X] Edit templates (number of zones, spacing, distance to highlight adjacent zones). Verify after reopening the editor that saved settings are kept the same.
- [X] Edit canvas layout: zones size and position, create or delete zones. 
- [X] Edit grid layout: split, merge, resize zones.
- [X] Check `Save and apply` and `Cancel` buttons behavior after editing.
- [X] Assign a layout to each monitor.
- [X] Assign keys to quickly switch layouts (custom layouts only), `Win + Ctrl + Alt + number`.


## FancyZones
- [X] Switch between `Allow zones to span across monitors` on and off. Verify that layouts are applied correctly in both cases. 
- [X] Change zone colors and opacity.
- [X] Exclude some apps, verify that they're not applicable to a zone.
- [X] Launch PT in user mode, try to assign a window with administrator privileges to a zone. Verify the notification is shown.
- [X] Launch PT in administrator mode, assign a window with administrator privileges.
- [X] Create virtual desktop, verify that there are the same layouts as applied to the previous virtual desktop.
- [X] After creating a virtual desktop apply another layout or edit the applied one. Verify that the other virtual desktop layout wasn't changed.
- [X] Delete an applied custom layout in the Editor, verify that there is no layout applied instead of it.
* Switch between layouts with quick keys.
    - [X] Switch with `Win` + `Ctrl` + `Alt` + `key`
    - [X] Switch with just a key while dragging a window.
* Change screen resolution or scaling. 
    - [X] Assign grid layout, verify that the assigned layout fits the screen. 
      NOTE: canvas layout could not fit the screen if it was created on a monitor with a different resolution.
    - [X] Edit grid layout, verify that split, merge and resize zones works as expected.
- [X] Disable FZ
- [X] Reenable FZ, verify that everything is in the same state as it was before disabling.

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [X] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\runner\svgs
   - [X] verify Preview Pane works for the SVG files
   - [X] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
 * Running as admin:
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [X] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\runner\svgs
   - [X] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached
   - [X] copy and paste one of the SVG file and verify the new file show the generic SVG icon

## Image Resizer
- [X] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [X] Enable the Image Resizer and check that `Resize images` is present in the context menu
- [X] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [X] Resize one image
- [X] Resize multiple images

- [X] Resize images with `Fill` option
- [X] Resize images with `Fit` option
- [X] Resize images with `Stretch` option

- [X] Resize images using dimension: Centimeters
- [X] Resize images using dimension: Inches
- [X] Resize images using dimension: Percents
- [X] Resize images using dimension: Pixels

- [X] Try to resize wmf image. Resized image has to be of fallback encoder type
- [X] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [X] Check `Use original date modified` and verify that modified date is not changed for resized images
- [X] Check `Make pictures smaller but not larger` and verify that smaller pictures are not resized
- [X] Check `Resize the original pictures (don't create copies)` and verify that the original picture is resized and a copy is not created
- [X] Uncheck `Ignore the orientation of pictures` and verify that swapped width and height will actually resize a picture if the width is not equal to the height

## Keyboard Manager

UI Validation:

  - [X] In Remap keys, add and remove rows to validate those buttons. While the blank rows are present, pressing the OK button should result in a warning dialog that some mappings are invalid.
  - [X] Using only the Type buttons, for both the remap windows, try adding keys/shortcuts in all the columns. The right-side column in both windows should accept both keys and shortcuts, while the left-side column will accept only keys or only shortcuts for Remap keys and Remap shortcuts respectively. Validate that the Hold Enter and Esc accessibility features work as expected.
  - [X] Using the drop downs try to add key to key, key to shortcut, shortcut to key and shortcut to shortcut remapping and ensure that you are able to select remapping both by using mouse and by keyboard navigation.
  - [X] Validate that remapping can be saved by pressing the OK button and re-opening the windows loads existing remapping.

Remapping Validation:

For all the remapping below, try pressing and releasing the remapped key/shortcut and pressing and holding it. Try different behaviors like releasing the modifier key before the action key and vice versa.
  - [X] Test key to key remapping
    - A->B
    - Ctrl->A
    - A->Ctrl
    - Win->B (make sure Start menu doesn't appear accidentally)
    - B->Win (make sure Start menu doesn't appear accidentally)
    - A->Disable
    - Win->Disable
  - [X] Test key to shortcut remapping
    - A->Ctrl+V
    - B->Win+A
  - [X] Test shortcut to shortcut remapping
    - Ctrl+A->Ctrl+V
    - Win+A->Ctrl+V
    - Ctrl+V->Win+A
    - Win+A->Win+F
  - [X] Test shortcut to key remapping
    - Ctrl+A->B
    - Ctrl+A->Win
    - Win+A->B
  * Test app-specific remaps
    - [X] Similar remaps to above with Edge, VSCode (entered as code) and cmd. For cmd try admin and non-admin (requires PT to run as admin)
    - [X] Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - [X] Test switching between remapping while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

## PowerRename
- [X] Check if disable and enable of the module works.
- [X] Check that with the `Show icon on context menu` icon is shown and vice versa.
- [X] Check if `Appear only in extended context menu` works.
- [X] Enable/disable autocomplete.
- [X] Enable/disable `Show values from last use`.
* Select several files and folders and check PowerRename options:
    - [X] Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
    - [X] Exclude Folders/Files/Subfolder Items (could be selected several)
    - [X] Item Name/Extension Only (one at the time)
    - [X] Enumerate Items
    - [X] Case Sensitive 
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
   - [X] when PowerToys is running unelevated on start-up
   - [X] when PowerToys is running as admin on start-up
   - [X] when PowerToys is restarted as admin, by clicking the restart as admin button in settings.
 * Check that each of the plugins is working:
   - [X] Program - launch a Win32 application
   - [X] Program - launch a Win32 application as admin
   - [X] Program - launch a packaged application
   - [X] Calculator - ensure a mathematical input returns a correct response and is copied on enter.
   - [X] Windows Search - open a file on the disk.
   - [X] Windows Search - find a file and copy file path.
   - [X] Windows Search - find a file and open containing folder.
   - [X] Shell - execute a command. Enter the action keyword `>`, followed by the query, both with and without space (e.g. `> ping localhost`).
   - [X] Folder - Search and open a sub-folder on entering the path.
   - [X] Uri - launch a web page on entering the uri.
   - [X] Window walker - Switch focus to a running window.
   - [X] Service - start, stop, restart windows service. Enter the action keyword `!` to get the list of services.
   - [X] Registry - navigate through the registry tree and open registry editor. Enter the action keyword `:` to get the root keys.
   - [X] Registry - navigate through the registry tree and copy key path.
   - [X] System - test `lock`.
   - [X] System - test `empty recycle bin`.
   - [ ] System - test `shutdown`.
 
 - [X] Disable PT Run and ensure that the hotkey doesn't bring up PT Run.
 
 - [X] Test tab navigation. 

 * Test Plugin Manager
   - [X] Enable/disable plugins and verify changes are picked up by PT Run
   - [X] Change `Direct activation phrase` and verify changes are picked up by PT Run
   - [X] Change `Include in global result` and verify changes picked up by PT Run
   - [X] Clear `Direct activation phrase` and uncheck `Include in global result`. Verify a warning message is shown.
   - [X] Disable all plugins and verify the warning message is shown.

### OOBE
 * Quit PowerToys
 * Delete %localappdata%\Microsoft\PowerToys
 - [X] Start PowerToys and verify OOBE opens
 * Visit each OOBE section and for each section:
   - [X] open the Settings for that module
   - [X] verify the Settings work as expected (toggle some controls on/off etc.)
   - [X] close the Settings
   - [X] if it's available, test the `Launch module name` button
 * Close OOBE
 - [X] Open the Settings and from the General page open OOBE using the `Welcome to PowerToys` link
