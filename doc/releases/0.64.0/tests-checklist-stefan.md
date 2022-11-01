## Functional tests

 Regressions:
 - [x] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038
 - [ ] https://github.com/microsoft/PowerToys/issues/1524

## Localization
 Change the Winodws language to a language different than English. Then verify if the following screens change their language:
 - [x] System tray menu items
 - [x] Settings
 - [x] OOBE (What's new)
 - [x] Keyboard Manager Editor
 - [x] Color Picker (check the tooltips)
 - [x] FancyZones Editor
 - [x] Power Rename (new WinUI 3 may not be localized)
 - [x] PowerToys Run ("Start typing" string is localized, for example)
 - [x] Image Resizer
 - [x] Shortcut Guide (Windows controls are localized)
 - [x] File Explorer menu entries for Image Resizer and Power Rename
 - [x] Hosts File Editor
 - [x] File Locksmith

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
   - [ ] when PowerToys is running unelevated on start-up
   - [ ] when PowerToys is running as admin on start-up
   - [ ] when PowerToys is restarted as admin, by clicking the restart as admin button in settings.
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

 - [X] Disable PT Run and ensure that the hotkey doesn't bring up PT Run.

 - [X] Test tab navigation.

 * Test Plugin Manager
   - [x] Enable/disable plugins and verify changes are picked up by PT Run
   - [x] Change `Direct activation phrase` and verify changes are picked up by PT Run
   - [x] Change `Include in global result` and verify changes picked up by PT Run
   - [x] Clear `Direct activation phrase` and uncheck `Include in global result`. Verify a warning message is shown.
   - [x] Disable all plugins and verify the warning message is shown.

## Always on Top
 - [x] Pin/unpin a window, verify it's topmost/not topmost.
 - [x] Pin/unpin a window, verify the border appeared/disappeared.
 - [x] Switch virtual desktop, verify border doesn't show up on another desktop.
 - [x] Minimize and maximize pinned window, verify the border looks as usual.
 - [x] Change border color and thickness.
 - [x] Verify if sound is played according to the sound setting.
 - [x] Exclude app, try to pin it.
 - [x] Exclude already pinned app, verify it was unpinned.
 - [x] Try to pin the app in the Game Mode.

### Quick Accent
 * Enable Quick Accent and open notepad. Then:
   - [x] Press `a` and the left or right arrow and verify the accent menu appears and adds the accented letter you've selected. Use left and arrow keys to cycle through the options.
   - [x] Press `a` and the space key and verify the accent menu appears and adds the accented letter you've selected. Use the space key to cycle through the options.
   - [x] Disable Quick Accent and verify you can no longer add accented characters through Quick Accent.
 * Test the different settings and verify they are applied:
   - [x] Activation key
   - [x] Language (for example, Currency has no accents for 'a' but has for 's')
   - [x] Toolbar position (test every option, some had issues before)
   - [x] Input delay

### GPO
 * Copy the "PowerToys.admx" file to your Policy Definition template folder. (Example: C:\Windows\PolicyDefinitions) and copy the "PowerToys.adml" file to the matching language folder in your Policy Definition folder. (Example: C:\Windows\PolicyDefinitions\en-US)
   - [x] Open the "Local Group Policy Editor" on Windows and verify there is a "Microsoft PowerToys" folder in Administrative Templates for both Computer Configuration and User Configuration.
 * In GPO, disable a module that can run as a standalone (FancyZones sounds good for this). Restart PowerToys.
   - [x] Verify the module is not enabled.
   - [x] Open settings and verify the module is not enabled and you can't enable it.
   - [x] Try to open FancyZones Editor directly from the install folder and verify it doesn't run and adds a message to the log saying it didn't run because of GPO.
 * In GPO, enable a module that can run as a standalone (FancyZones sounds good for this). Restart PowerToys.
   - [x] Verify the module is enabled.
   - [x] Open settings and verify the module is enabled and you can't disable it.
 * In GPO, try to set different settings in the Computer and User Configurations for a PowerToy. Restart PowerToys.
   - [x] Verify that the setting in Computer Configuration has priority over the setting in User Configuration.
 * In GPO, disable a module that has a context menu entry (File Locksmith sounds good for this). Restart PowerToys.
   - [x] Verify the module is not enabled. (No context menu entry)
   - [x] Open settings and verify the module is not enabled and you can't enable it.
   - [x] Try to open File Locksmith directly from the install folder and verify it doesn't run and adds a message to the log saying it didn't run because of GPO.
 * In GPO, disable a module that is a Preview Handler (Markdown Preview is good for this). Restart PowerToys.
   - [x] Verify the module is not enabled. (Markdown files won't appear in the preview pane)
   - [x] Open settings and verify the module is not enabled and you can't enable it.
 * Remember to reset all you Settings to Not Configured after the tests, both in Conputer and User Configurations.
