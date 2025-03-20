## Telemetry Prologue
 * Before starting your tests, go to General settings, enable "Diagnostic data" and "Enable viewing" and restart PowerToys.

## Localization
 Change the Windows language to a language different than English. Then verify if the following screens change their language:
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
 - [x] File Explorer menu entries for Image Resizer, Power Rename and FileLocksmith
 - [x] Hosts File Editor
 - [x] File Locksmith
 - [x] Registry Preview
 - [x] Environment Variables

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

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [x] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [x] verify Preview Pane works for the SVG files
   - [x] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-PdfPreviewHandler\HelperFiles
   - [x] verify Preview Pane works for the PDF file
   - [x] verify the Icon Preview works for the PDF file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-GcodePreviewHandler\HelperFiles
   - [x] verify Preview Pane works for the gcode file
   - [x] verify the Icon Preview works for the gcode file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-StlThumbnailProvider\HelperFiles
   - [x] verify the Icon Preview works for the stl file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\runner
   - [x] verify Preview Pane works for source files (shows syntax highlighting)
 * Running as admin (or user since recently):
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [x] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\modules\ShortcutGuide\ShortcutGuide\svgs
   - [x] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached (you can also use `cleanmgr.exe` to clean all thumbnails cached in your system). You may need to restart the machine for this setting to apply as well.
   - [x] copy and paste one of the SVG file and verify the new file show the generic SVG icon
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-PdfPreviewHandler\HelperFiles
   - [x] verify Preview Pane doesn't show the preview for the PDF file
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-GcodePreviewHandler\HelperFiles
   - [x] verify Preview Pane doesn't show the preview for the gcode file
   * go to PowerToys repo and visit src\modules\previewpane\UnitTests-StlThumbnailProvider\HelperFiles
   - [x] verify Preview Pane doesn't show the preview for the stl file (a generated thumbnail would show when there's no preview)
   * go to PowerToys repo and visit src\runner
   - [x] verify Preview Pane doesn't show the preview for source code files or that it's a default previewer instead of Monaco

## Image Resizer
- [x] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [x] Enable the Image Resizer and check that `Resize images` is present in the context menu. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
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
    - [x] Similar remaps to above with Edge (entered as `msedge`), VSCode (entered as `code`) and cmd (entered as `windowsterminal`). For cmd try admin and non-admin (requires PT to run as admin)
    - [x] Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - [x] Test switching between remapping while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

## PowerRename
- [x] Check if disable and enable of the module works. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
- [x] Check that with the `Show icon on context menu` icon is shown and vice versa.
- [x] Check if `Appear only in extended context menu` works.
- [x] Enable/disable autocomplete.
- [x] Enable/disable `Show values from last use`.
* Select several files and folders and check PowerRename options:
    - [x] Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
    - [x] Exclude Folders/Files/Subfolder Items (could be selected several)
    - [x] Item Name/Extension Only (one at the time)
    - [x] Enumerate Items. Test advanced enumeration using different values for every field ${start=10,increment=2,padding=4}.
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
   - [x] Set Shortcut Guide to start with a Windows key press and verify it works.
 * Restore the `Win + Shift + /` hotkey.
   - [x] Open the guide and close it pressing `Esc`
   - [x] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [x] Verify `Win + Shift + /` opens the guide
   - [x] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

## OOBE
 * Quit PowerToys
 * Delete %localappdata%\Microsoft\PowerToys
 - [x] Start PowerToys and verify OOBE opens
 * Change version saved on `%localappdata%\Microsoft\PowerToys\last_version.txt`
 - [x] Start PowerToys and verify OOBE opens in the "What's New" page
 * Visit each OOBE section and for each section:
   - [x] open the Settings for that module
   - [x] verify the Settings work as expected (toggle some controls on/off etc.)
   - [x] close the Settings
   - [x] if it's available, test the `Launch module name` button
 * Close OOBE
 - [x] Open the Settings and from the General page open OOBE using the `Welcome to PowerToys` link

## Mouse Utils

Find My Mouse:
  * Enable FindMyMouse. Then, without moving your mouse:
    - [x] Press Left Ctrl twice and verify the overlay appears.
    - [x] Press any other key and verify the overlay disappears.
    - [x] Press Left Ctrl twice and verify the overlay appears.
    - [x] Press a mouse button and verify the overlay disappears.
  * Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice.
  * Enable FindMyMouse. Then, without moving your mouse:
    - [x] Press Left Ctrl twice and verify the overlay appears.
  * Enable the "Do not activate on game mode" option. Start playing a game that uses CG native full screen.
    - [x] Verify the overlay no longer appears when you press Left Ctrl twice.
  * Disable the "Do not activate on game mode" option. Start playing the same game.
    - [x] Verify the overlay appears when you press Left Ctrl twice. (though it'll likely minimize the game)
  * Test the different settings and verify they apply:
    - [x] Overlay opacity
    - [x] Background color
    - [x] Spotlight color
    - [x] Spotlight radius
    - [x] Spotlight initial zoom (1x vs 9x will show the difference)
    - [x] Animation duration
    - [x] Change activation method to shake and activate by shaking your mouse pointer
    - [x] Excluded apps

Mouse Highlighter:
  * Enable Mouse Highlighter. Then:
    - [x] Press the activation shortcut and press left and right click somewhere, verifying the hightlights are applied.
    - [x] With left mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [x] With right mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [x] Press the activation shortcut again and verify no highlights appear when the mouse buttons are clicked.
    - [x] Disable Mouse Highlighter and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [x] Change activation shortcut and test it
    - [x] Left button highlight color
    - [x] Right button highlight color
    - [x] Opacity
    - [x] Radius
    - [x] Fade delay
    - [x] Fade duration

Mouse Pointer Crosshairs:
  * Enable Mouse Pointer Crosshairs. Then:
    - [x] Press the activation shortcut and verify the crosshairs appear, and that they follow the mouse around.
    - [x] Press the activation shortcut again and verify the crosshairs disappear.
    - [x] Disable Mouse Pointer Crosshairs and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [x] Change activation shortcut and test it
    - [x] Crosshairs color
    - [x] Crosshairs opacity
    - [x] Crosshairs center radius
    - [x] Crosshairs thickness
    - [x] Crosshairs border color
    - [x] Crosshairs border size

Mouse Jump:
  * Enable Mouse Jump. Then:
    - [x] Press the activation shortcut and verify the screens preview appears.
    - [x] Change activation shortcut and verify that new shorctut triggers Mouse Jump.
    - [x] Click around the screen preview and ensure that mouse cursor jumped to clicked location.
    - [x] Reorder screens in Display settings and confirm that Mouse Jump reflects the change and still works correctly.
    - [x] Change scaling of screens and confirm that Mouse Jump still works correctly.
    - [x] Unplug additional monitors and confirm that Mouse Jump still works correctly.
    - [x] Disable Mouse Jump and verify that the module is not activated when you press the activation shortcut.

## Awake
 - [x] Try out the features and see if they work, no list at this time.

## Text Extractor
 * Enable Text Extractor. Then:
   - [x] Press the activation shortcut and verify the overlay appears.
   - [x] Press Escape and verify the overlay disappears.
   - [x] Press the activation shortcut and verify the overlay appears.
   - [x] Right-click and select Cancel. Verify the overlay disappears.
   - [x] Disable Text Extractor and verify that the activation shortuct no longer activates the utility.
 * With Text Extractor enabled and activated:
   - [x] Try to select text and verify it is copied to the clipboard.
   - [x] Try to select a different OCR language by right-clicking and verify the change is applied.
 * In a multi-monitor setup with different dpis on each monitor:
   - [x] Verify text is correctly captured on all monitors.
 * Test the different settings and verify they are applied:
   - [x] Activation shortcut
   - [x] OCR Language

## File Locksmith
 * Start the PowerToys installer executable and let it stay in the initial screen.
   - [x] Right-click the executable file, select "What's using this file?" and verify it shows up. (2 entries will show, since the installer starts two processes)
   - [x] End the tasks in File Locksmith UI and verify that closes the installer.
   - [x] Start the installer executable again and press the Refresh button in File Locksmith UI. It should find new processes using the files.
   - [x] Close the installer window and verify the processes are delisted from the File Locksmith UI. Close the window
 * Start the PowerToys installer executable again and let it stay in the initial screen.
   - [x] Right click the directory where the executable is located, select "What's using this file?" and verify it shows up. 
   - [x] Right click the drive where the executable is located, select "What's using this file?" and verify it shows up. You can close the PowerToys installer now.
 * Restart PowerToys as admin.
   - [x] Right click "Program Files", select "What's using this file?" and verify "PowerToys.exe" doesn't show up.
   - [x] Press the File Locksmith "Restart as an administrator" button and verify "PowerToys.exe" shows up.
 - [x] Right-click the drive where Windows is installed, select "What's using this file?" and scroll down and up, verify File Locksmith doesn't crash with all those entries being shown. Repeat after clicking the File Locksmith "Restart as an administrator" button.
 - [x] Disable File Locksmith in Settings and verify the context menu entry no longer appears.

## GPO
 * Copy the "PowerToys.admx" file to your Policy Definition template folder. (Example: C:\Windows\PolicyDefinitions) and copy the "PowerToys.adml" file to the matching language folder in your Policy Definition folder. (Example: C:\Windows\PolicyDefinitions\en-US)
   - [x] Open the "Local Group Policy Editor" on Windows and verify there is a "Microsoft PowerToys" folder in Administrative Templates for both Computer Configuration and User Configuration.
 * In GPO, disable a module that can run as a standalone (FancyZones sounds good for this). Restart PowerToys.
   - [x] Verify the module is not enabled.
   - [x] Open settings and verify the module is not enabled and you can't enable it.
   - [x] Try to open FancyZones Editor directly from the install folder and verify it doesn't run and adds a message to the log saying it didn't run because of GPO.
   - [x] Verify the module can't be launched from the quick launcher system tray flyout launcher screen (FancyZones editor in this case).
   - [x] Verify the module can't be enabled/disabled from the quick launcher system tray flyout.
 * In GPO, enable a module that can run as a standalone (FancyZones sounds good for this). Restart PowerToys.
   - [x] Verify the module is enabled.
   - [x] Open settings and verify the module is enabled and you can't disable it.
   - [x] Verify the module can't be enabled/disabled from the quick launcher system tray flyout.
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

## Mouse Without Borders
 * Install PowerToys on two PCs in the same local network:
   - [x] Verify that PowerToys is properly installed on both PCs.
   
 * Setup Connection:
   - [x] Open MWB's settings on the first PC and click the "New Key" button. Verify that a new security key is generated.
   - [x] Copy the generated security key and paste it in the corresponding input field in the settings of MWB on the second PC. Also enter the name of the first PC in the required field.
   - [x] Press "Connect" and verify that the machine layout now includes two PC tiles, each displaying their respective PC names.
   
 * Verify Connection Status:
   - [x] Ensure that the border of the remote PC turns green, indicating a successful connection.
   - [x] Enter an incorrect security key and verify that the border of the remote PC turns red, indicating a failed connection.
   
 * Test Remote Mouse/Keyboard Control:
   - [x] With the PCs connected, test the mouse/keyboard control from one PC to another. Verify that the mouse/keyboard inputs are correctly registered on the other PC.
   - [x] Test remote mouse/keyboard control across all four PCs, if available. Verify that inputs are correctly registered on each connected PC when the mouse is active there.
   
 * Test Remote Control with Elevated Apps:
   - [x] Open an elevated app on one of the PCs. Verify that without "Use Service" enabled, PowerToys does not control the elevated app.
   - [x] Enable "Use Service" in MWB's settings. Verify that PowerToys can now control the elevated app remotely. Verify that MWB processes are running as LocalSystem, while the MWB helper process is running non-elevated.
   - [x] Toggle "Use Service" again, verify that each time you do that, the MWB processes are restarted.
   - [x] Run PowerToys elevated on one of the machines, verify that you can control elevated apps remotely now on that machine.

* Test Module Enable Status:
   - [x] For all combinations of "Use Service"/"Run PowerToys as admin", try enabling/disabling MWB module and verify that it's indeed being toggled using task manager.
   
 * Test Disconnection/Reconnection:
   - [x] Disconnect one of the PCs from network. Verify that the machine layout updates to reflect the disconnection. 
   - [x] Do the same, but now by exiting PowerToys.
   - [x] Start PowerToys again, verify that the PCs are reconnected.
  
 * Test Various Local Network Conditions:
   - [x] Test MWB performance under various network conditions (e.g., low bandwidth, high latency). Verify that the tool maintains a stable connection and functions correctly.

 * Clipboard Sharing:
   - [x] Copy some text on one PC and verify that the same text can be pasted on another PC.
   - [x] Use the screenshot key and Win+Shift+S to take a screenshot on one PC and verify that the screenshot can be pasted on another PC.
   - [x] Copy a file in Windows Explorer and verify that the file can be pasted on another PC. Make sure the file size is below 100MB.
   - [x] Try to copy multiple files and directories and verify that it's not possible (only the first selected file is being copied).
 
 * Drag and Drop:
   - [x] Drag a file from Windows Explorer on one PC, cross the screen border onto another PC, and release it there. Verify that the file is copied to the other PC. Make sure the file size is below 100MB.
   - [x] While dragging the file, verify that a corresponding icon is displayed under the mouse cursor.
   - [x] Without moving the mouse from one PC to the target PC, press CTRL+ALT+F1/2/3/4 hotkey to switch to the target PC directly and verify that file sharing/dropping is not working.

 * Lock and Unlock with "Use Service" Enabled:
   - [x] Enable "Use Service" in MWB's settings.
   - [x] Lock a remote PC using Win+L, move the mouse to it remotely, and try to unlock it. Verify that you can unlock the remote PC.
   - [x] Disable "Use Service" in MWB's settings, lock the remote PC, move the mouse to it remotely, and try to unlock it. Verify that you can't unlock the remote PC.

 * Test Settings:
   - [x] Change the rest of available settings on MWB page and verify that each setting works as described.

## Crop And Lock
 * Thumbnail mode
   - [x] Test with win32 app
   - [x] Test with packaged app
   
 * Reparent mode (there are known issues where reparent mode doesn't work for some apps)
   - [x] Test with win32 app
   - [x] Test with packaged app

## Environment Variables
 * NOTE: Make backup of USER and SYSTEM Path and TMP variables before testing so you can revert those is something goes wrong!
 * Open Environment Variables settings
   - [x] Launch as administrator ON - Launch Environment Variables and confirm that SYSTEM variables ARE editable and Add variable button is enabled
   - [x] Launch as administrator OFF - Launch Environment Variables and confirm that SYSTEM variables ARE NOT editable and Add variable button is disabled

 * User/System variables
   - [x] Add new User variable. Open OS Environment variables window and confirm that added variable is there. Also, confirm that it's added to "Applied variables" list.
   - [x] Edit one User variable. Open OS Environment variables window and confirm that variable is changed. Also, confirm that change is applied to "Applied variables" list.
   - [x] Remove one User variable. Open OS Environment variables window and confirm that variable is removed. Also, confirm that variable is removed from "Applied variables" list.
   - Repeat the steps for System variables.

 * Profiles - Basic tests
   - [x] Add new profile with no variables and name it "Test_profile_1" (referenced below by name)
   - [x] Edit "Test_profile_1": Add one new variable to profile e.g. name: "profile_1_variable_1" value: "profile_1_value_1"
   - [x] Add new profile "Test_profile_2": From "Add profile dialog" add two new variables (profile_2_variable_1:profile_2_value_1 and profile_2_variable_2:profile_2_value_2). Set profile to enabled and click Save. Open OS Environment variables window and confirm that all variables from the profile are applied correctly. Also, confirm that "Applied variables" list contains all variables from the profile.
   - [x] Apply "Test_profile_1" while "Test_profile_2" is still aplpied. Open OS Environment variables window and confirm that all variables from Test_profile_2 are unapplied and that all variables from Test_profile_1 are applied. Also, confirm that state of "Applied variables" list is updated correctly.
   - [x] Unapply applied profile. Open OS Environment variables window and confirm that all variables from the profile are unapplied correctly. Also, confirm that "Applied variables" list does not contain variables from the profile.

 * Overriding existing variable
   - [x] To "Test_profile_1" add one existing variable from USER variables, e.g. TMP. After adding, change it's value to e.g "test_TMP" (or manually add variable named TMP with value test_TMP).
   - [x] Apply "Test_profile_1". Open OS Environment variables window and confirm that TMP variable in USER variables has value "test_TMP". Confirm that there is backup variable "TMP_PowerToys_Test_profile_1" with original value of TMP var. Also, confirm that "Applied variables" list is updated correctly - there is TMP profile variable, and backup User variable..
   - [x] Unapply "Test_profile_1". Open OS Environment variables window and confirm that TMP variable in USER variable has original value and that there is no backup variable. Also, confirm that "Applied variables" list is updated correctly.

 * PATH variable
   - [x] In "Applied variables" list confirm that PATH variable is shown properly: value of USER Path concatenated to the end of SYSTEM Path.
   - [x] To "Test_profile_1" add variable named PATH with value "path1;path2;path3" and click Save. Confirm that PATH variable in profile is shown as list (list of 3 values and not as path1;path2;path3).
   - [x] Edit PATH variable from "Test_profile_1". Try different options from ... menu (Delete, Move up, Move down, etc...). Click Save.
   - [x] Apply "Test_profile_1". Open OS Environment variables window and confirm that profile is applied correctly - Path value and backup variable. Also, in "Applied variables" list check that Path variable has correct value: value of profile PATH concatenated to the end of SYSTEM Path.

 * Loading profiles on startup
   - [x] Close the app and reopen it. Confirm that the state of the app is the same as before closing.

 - [x] "Test_profile_1" should still be applied (if not apply it). Delete "Test_profile_1". Confirm that profile is unapplied (both in OS Environment variables window and "Applied variables" list).
 - [x] Delete "Test_profile_2". Check profiles.json file and confirm that both profiles are gone.

## Advanced Paste
  NOTES:
    When using Advanced Paste, make sure that window focused while starting/using Advanced paste is text editor or has text input field focused (e.g. Word).
 * Paste As Plain Text
   - [x] Copy some rich text (e.g word of the text is different color, another work is bold, underlined, etd.).
   - [x] Paste the text using standard Windows Ctrl + V shortcut and ensure that rich text is pasted (with all colors, formatting, etc.)
   - [x] Paste the text using Paste As Plain Text activation shortcut and ensure that plain text without any formatting is pasted.
   - [x] Paste again the text using standard Windows Ctrl + V shortcut and ensure the text is now pasted plain without formatting as well.
   - [x] Copy some rich text again.
   - [x] Open Advanced Paste window using hotkey, click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
   - [x] Copy some rich text again.
   - [x] Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
 * Paste As Markdown
   - [x] Open Settings and set Paste as Markdown directly hotkey
   - [x] Copy some text (e.g. some HTML text - convertible to Markdown)
   - [x] Paste the text using set hotkey and confirm that pasted text is converted to markdown
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
   - [x] Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
   - [x] Open Advanced Paste window using hotkey, press Ctrl + 2 and confirm that pasted text is converted to markdown
 * Paste As JSON
   - [x] Open Settings and set Paste as JSON directly hotkey
   - [x] Copy some XML or CSV text (or any other text, it will be converted to simple JSON object)
   - [x] Paste the text using set hotkey and confirm that pasted text is converted to JSON
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
   - [x] Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
   - [x] Open Advanced Paste window using hotkey, press Ctrl + 3 and confirm that pasted text is converted to markdown
 * Paste as custom format using AI
   - [x] Open Settings, navigate to Enable Paste with AI and set OpenAI key.
   - [x] Copy some text to clipboard. Any text.
   - [x] Open Advanced Paste window using hotkey, and confirm that Custom intput text box is now enabled. Write "Insert smiley after every word" and press Enter. Observe that result preview shows coppied text with smileys between words. Press Enter to paste the result and observe that it is pasted.
   - [x] Open Advanced Paste window using hotkey. Input some query (any, feel free to play around) and press Enter. When result is shown, click regenerate button, to see if new result is generated. Select one of the results and paste. Observe that correct result is pasted.
   - [x] Create few custom actions. Set up hotkey for custom actions and confirm they work. Enable/disable custom actions and confirm that the change is reflected in Advanced Paste UI - custom action is not listed. Try different ctrl + <num> in-app shortcuts for custom actions. Try moving custom actions up/down and confirm that the change is reflected in Advanced Paste UI.
   - [x] Open Settings and disable Custom format preview. Open Advanced Paste window with hotkey, enter some query and press enter. Observe that result is now pasted right away, without showing the preview first.
   - [x] Open Settings and Disable Enable Paste with AI. Open Advanced Paste window with hotkey and observe that Custom Input text box is now disabled.
 * Clipboard History
   - [x] Open Settings and Enable clipboard history (if not enabled already). Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry. Check OS clipboard history (Win+V), and confirm that the same entry no longer exist.
   - [x] Open Advanced Paste window with hotkey, click Clipboard history, and click any entry (but first). Observe that entry is put on top of clipboard history. Check OS clipboard history (Win+V), and confirm that the same entry is on top of the clipboard.
   - [x] Open Settings and Disable clipboard history. Open Advanced Paste window with hotkey and observe that Clipboard history button is disabled.
 * Disable Advanced Paste, try different Advanced Paste hotkeys and confirm that it's disabled and nothing happens.

## New+
 * Enable New+ in Settings.
   - [x] Verify NewPlus menu is in Explorer context menu. (Windows 11 tier 1 context menu only. May need Explorer restart.)
 * Disable New+ in Settings.
   - [x] Verify NewPlus menu is not in Explorer context menu.
 * Choose a different path for template folder.
   - [x] Verify the folder is created and empty.
   - [x] Copy a file to the templates folder, verify it's added to the New+ context menu and that if you select it the file is created.
   - [x] Copy a folder with files inside to the templates folder, verify it's added to the New+ context menu and that if you select it the folder and files inside are created.
   - [x] Delete all files and folders from inside the templates folder. Verify that no templates are available in the context menu.
   - [x] Disable and re-Enable New+ while the templates folder is still empty. Verify the default templates were copied over and are available in the context menu.
 * Test some Settings:
   - [x] Test the "Hide template filename extension" option in Settings.
   - [x] Test the "Hide template filename starting digits, spaces and dots" option in Settings.

## Telemetry Epilogue
 * After finishing your tests, go to General settings, press Diagnostic data viewer and check if you have xml files for the utilities you've tested and if it looks like the events in those xml files were generated by the actions you did with the utilities you've tested.
