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
 - [x] restart PT and verify it runs as user
 - [x] restart as admin and set "Always run as admin"
 - [x] restart PT and verify it  runs as admin
 * if it's not on, turn on "Run at startup"
 - [x] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [x] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [x] turn off all the modules and verify all module are off
 - [x] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [x] turn on all the module, all module are now working
 - [x] restart PT and verify that all module are still on in the settings page and they are actually working

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
- [x] Disable FZ
- [x] Re-enable FZ, verify that everything is in the same state as it was before disabling.

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [x] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [x] verify Preview Pane works for the SVG files
   - [x] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
 * Running as admin:
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [x] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [x] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached
   - [x] copy and paste one of the SVG file and verify the new file show the generic SVG icon

## Image Resizer
- [x] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [x] Enable the Image Resizer and check that `Resize images` is present in the context menu
- [x] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [x] Resize one image
- [x] Resize multiple images

- [x] Resize images with `Fill` option
- [x] Resize images with `Fit` option
- [x] Resize images with `Stretch` option

- [x] Resize images using dimension: Centimeters
- [x] Resize images using dimension: Inches
- [x] Resize images using dimension: Percents
- [x] Resize images using dimension: Pixels

- [x] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [x] Check `Use original date modified` and verify that modified date is not changed for resized images
- [x] Check `Make pictures smaller but not larger` and verify that smaller pictures are not resized
- [x] Check `Resize the original pictures (don't create copies)` and verify that the original picture is resized and a copy is not created
- [x] Uncheck `Ignore the orientation of pictures` and verify that swapped width and height will actually resize a picture if the width is not equal to the height

## Keyboard Manager

UI Validation:

  - [x] In Remap keys, add and remove rows to validate those buttons. While the blank rows are present, pressing the OK button should result in a warning dialog that some mappings are invalid.
  - [x] Using only the Type buttons, for both the remap windows, try adding keys/shortcuts in all the columns. The right-side column in both windows should accept both keys and shortcuts, while the left-side column will accept only keys or only shortcuts for Remap keys and Remap shortcuts respectively. Validate that the Hold Enter and Esc accessibility features work as expected.
  - [x] Using the drop downs try to add key to key, key to shortcut, shortcut to key and shortcut to shortcut remapping and ensure that you are able to select remapping both by using mouse and by keyboard navigation.
  - [x] Validate that remapping can be saved by pressing the OK button and re-opening the windows loads existing remapping.

Remapping Validation:

For all the remapping below, try pressing and releasing the remapped key/shortcut and pressing and holding it. Try different behaviors like releasing the modifier key before the action key and vice versa.
  - [x] Test key to key remapping
    - A->B
    - Ctrl->A
    - A->Ctrl
    - Win->B (make sure Start menu doesn't appear accidentally)
    - B->Win (make sure Start menu doesn't appear accidentally)
    - A->Disable
    - Win->Disable
  - [x] Test key to shortcut remapping
    - A->Ctrl+V
    - B->Win+A
  - [x] Test shortcut to shortcut remapping
    - Ctrl+A->Ctrl+V
    - Win+A->Ctrl+V
    - Ctrl+V->Win+A
    - Win+A->Win+F
  - [x] Test shortcut to key remapping
    - Ctrl+A->B
    - Ctrl+A->Win
    - Win+A->B
  * Test app-specific remaps
    - [x] Similar remaps to above with Edge, VSCode (entered as code) and cmd. For cmd try admin and non-admin (requires PT to run as admin)
    - [x] Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - [x] Test switching between remapping while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

## PowerRename
- [x] Check if disable and enable of the module works.
- [x] Check that with the `Show icon on context menu` icon is shown and vice versa.
- [x] Check if `Appear only in extended context menu` works.
- [x] Enable/disable autocomplete.
- [x] Enable/disable `Show values from last use`.
* Select several files and folders and check PowerRename options:
    - [x] Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
    - [x] Exclude Folders/Files/Subfolder Items (could be selected several)
    - [x] Item Name/Extension Only (one at the time)
    - [x] Enumerate Items
    - [x] Case Sensitive 
    - [x] Match All Occurrences. If checked, all matches of text in the `Search` field will be replaced with the Replace text. Otherwise, only the first instance of the `Search` for text in the file name will be replaced (left to right).
    * Use regular expressions
        - [x] Search with an expression (e.g. `(.*).png`)
        - [x] Replace with an expression (e.g. `foo_$1.png`)
        - [x] Replace using file creation date and time (e.g. `$hh-$mm-$ss-$fff` `$DD_$MMMM_$YYYY`)
        - [x] Turn on `Use Boost library` and test with Perl Regular Expression Syntax (e.g. `(?<=t)est`)
    * File list filters. 
        - [x] In the `preview` window uncheck some items to exclude them from renaming. 
        - [x] Click on the `Renamed` column to filter results. 
        - [x] Click on the `Original` column to cycle between checked and unchecked items.

## PowerToys Run

 * Enable PT Run in settings and ensure that the hotkey brings up PT Run 
   - [x] when PowerToys is running unelevated on start-up
   - [x] when PowerToys is running as admin on start-up
   - [x] when PowerToys is restarted as admin, by clicking the restart as admin button in settings.
 * Check that each of the plugins is working:
   - [x] Program - launch a Win32 application
   - [x] Program - launch a Win32 application as admin
   - [x] Program - launch a packaged application
   - [x] Calculator - ensure a mathematical input returns a correct response and is copied on enter.
   - [x] Windows Search - open a file on the disk.
   - [x] Windows Search - find a file and copy file path.
   - [x] Windows Search - find a file and open containing folder.
   - [x] Shell - execute a command. Enter the action keyword `>`, followed by the query, both with and without space (e.g. `> ping localhost`).
   - [x] Folder - Search and open a sub-folder on entering the path.
   - [x] Uri - launch a web page on entering the uri.
   - [x] Window walker - Switch focus to a running window.
   - [x] Service - start, stop, restart windows service. Enter the action keyword `!` to get the list of services.
   - [x] Registry - navigate through the registry tree and open registry editor. Enter the action keyword `:` to get the root keys.
   - [x] Registry - navigate through the registry tree and copy key path.
   - [x] System - test `lock`.
   - [x] System - test `empty recycle bin`.
   - [x] System - test `shutdown`.
 
 - [x] Disable PT Run and ensure that the hotkey doesn't bring up PT Run.
 
 - [x] Test tab navigation. 

 * Test Plugin Manager
   - [x] Enable/disable plugins and verify changes are picked up by PT Run
   - [x] Change `Direct activation phrase` and verify changes are picked up by PT Run
   - [x] Change `Include in global result` and verify changes picked up by PT Run
   - [x] Clear `Direct activation phrase` and uncheck `Include in global result`. Verify a warning message is shown.
   - [x] Disable all plugins and verify the warning message is shown.
  
## Shortcut Guide
 * Run PowerToys as user:
   - [x] Verify `Win + Shift + /` opens the guide
   - [x] Change the hotkey to a different shortcut (e.g. `Win + /`) and verify it works
   * Restore the `Win + Shift + /` hotkey.
   - [x] Open the guide and close it pressing `Esc`
   - [x] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [x] Verify `Win + Shift + /` opens the guide
   - [x] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

