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

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [X] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [X] verify Preview Pane works for the SVG files
   - [X] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
 * Running as admin:
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [X] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [X] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached
   - [X] copy and paste one of the SVG file and verify the new file show the generic SVG icon

## Expresso
 - [X] Try out the features and see if they work, no list at this time.

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
    - [X] Match All Occurrences. If checked, all matches of text in the `Search` field will be replaced with the Replace text. Otherwise, only the first instance of the `Search` for text in the file name will be replaced (left to right).
    * Use regular expressions
        - [X] Search with an expression (e.g. `(.*).png`)
        - [X] Replace with an expression (e.g. `foo_$1.png`)
        - [X] Replace using file creation date and time (e.g. `$hh-$mm-$ss-$fff` `$DD_$MMMM_$YYYY`)
        - [X] Turn on `Use Boost library` and test with Perl Regular Expression Syntax (e.g. `(?<=t)est`)
    * File list filters. 
        - [X] In the `preview` window uncheck some items to exclude them from renaming. 
        - [X] Click on the `Renamed` column to filter results. 
        - [X] Click on the `Original` column to cycle between checked and unchecked items.

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
  




