## Functional tests

 Regressions:
 - [x] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038
 - [x] https://github.com/microsoft/PowerToys/issues/1524

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

- [X] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [X] Check `Use original date modified` and verify that modified date is not changed for resized images. Take into account that `Resize the original pictures(don't create copy)` should be selected
- [X] Check `Make pictures smaller but not larger` and verify that smaller pictures are not resized
- [X] Check `Resize the original pictures (don't create copies)` and verify that the original picture is resized and a copy is not created
- [X] Uncheck `Ignore the orientation of pictures` and verify that swapped width and height will actually resize a picture if the width is not equal to the height

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
    - [X] Match All Occurrences. If checked, all matches of text in the `Search` field will be replaced with the Replace text. Otherwise, only the first instance of the `Search` for text in the file name will be replaced (left to right).
    * Use regular expressions
        - [X] Search with an expression (e.g. `(.*).png`)
        - [X] Replace with an expression (e.g. `foo_$1.png`)
        - [X] Replace using file creation date and time (e.g. `$hh-$mm-$ss-$fff` `$DD_$MMMM_$YYYY`)
        - [X] Turn on `Use Boost library` and test with Perl Regular Expression Syntax (e.g. `(?<=t)est`)
    * File list filters.
        - [X] In the `preview` window uncheck some items to exclude them from renaming.
        - [X] Click on the `Renamed` column to filter results.
        - [x] Click on the `Original` column to cycle between checked and unchecked items.

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
   - [X] System - test `shutdown`.

 - [X] Disable PT Run and ensure that the hotkey doesn't bring up PT Run.

 - [X] Test tab navigation.

 * Test Plugin Manager
   - [X] Enable/disable plugins and verify changes are picked up by PT Run
   - [X] Change `Direct activation phrase` and verify changes are picked up by PT Run
   - [X] Change `Include in global result` and verify changes picked up by PT Run
   - [X] Clear `Direct activation phrase` and uncheck `Include in global result`. Verify a warning message is shown.
   - [X] Disable all plugins and verify the warning message is shown.

## Shortcut Guide
 * Run PowerToys as user:
   - [X] Verify `Win + Shift + /` opens the guide
   - [X] Change the hotkey to a different shortcut (e.g. `Win + /`) and verify it works
   * Restore the `Win + Shift + /` hotkey.
   - [X] Open the guide and close it pressing `Esc`
   - [X] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [X] Verify `Win + Shift + /` opens the guide
   - [X] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

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
