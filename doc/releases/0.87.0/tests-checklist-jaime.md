## Telemetry Prologue
 * Before starting your tests, go to General settings, enable "Diagnostic data" and "Enable viewing" and restart PowerToys.

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

## Mouse Without Borders
 * Install PowerToys on two PCs in the same local network:
   - [ ] Verify that PowerToys is properly installed on both PCs.
   
 * Setup Connection:
   - [ ] Open MWB's settings on the first PC and click the "New Key" button. Verify that a new security key is generated.
   - [ ] Copy the generated security key and paste it in the corresponding input field in the settings of MWB on the second PC. Also enter the name of the first PC in the required field.
   - [ ] Press "Connect" and verify that the machine layout now includes two PC tiles, each displaying their respective PC names.
   
 * Verify Connection Status:
   - [ ] Ensure that the border of the remote PC turns green, indicating a successful connection.
   - [ ] Enter an incorrect security key and verify that the border of the remote PC turns red, indicating a failed connection.
   
 * Test Remote Mouse/Keyboard Control:
   - [ ] With the PCs connected, test the mouse/keyboard control from one PC to another. Verify that the mouse/keyboard inputs are correctly registered on the other PC.
   - [ ] Test remote mouse/keyboard control across all four PCs, if available. Verify that inputs are correctly registered on each connected PC when the mouse is active there.
   
 * Test Remote Control with Elevated Apps:
   - [ ] Open an elevated app on one of the PCs. Verify that without "Use Service" enabled, PowerToys does not control the elevated app.
   - [ ] Enable "Use Service" in MWB's settings. Verify that PowerToys can now control the elevated app remotely. Verify that MWB processes are running as LocalSystem, while the MWB helper process is running non-elevated.
   - [ ] Toggle "Use Service" again, verify that each time you do that, the MWB processes are restarted.
   - [ ] Run PowerToys elevated on one of the machines, verify that you can control elevated apps remotely now on that machine.

* Test Module Enable Status:
   - [ ] For all combinations of "Use Service"/"Run PowerToys as admin", try enabling/disabling MWB module and verify that it's indeed being toggled using task manager.
   
 * Test Disconnection/Reconnection:
   - [ ] Disconnect one of the PCs from network. Verify that the machine layout updates to reflect the disconnection. 
   - [ ] Do the same, but now by exiting PowerToys.
   - [ ] Start PowerToys again, verify that the PCs are reconnected.
  
 * Test Various Local Network Conditions:
   - [ ] Test MWB performance under various network conditions (e.g., low bandwidth, high latency). Verify that the tool maintains a stable connection and functions correctly.

 * Clipboard Sharing:
   - [ ] Copy some text on one PC and verify that the same text can be pasted on another PC.
   - [ ] Use the screenshot key and Win+Shift+S to take a screenshot on one PC and verify that the screenshot can be pasted on another PC.
   - [ ] Copy a file in Windows Explorer and verify that the file can be pasted on another PC. Make sure the file size is below 100MB.
   - [ ] Try to copy multiple files and directories and verify that it's not possible (only the first selected file is being copied).
 
 * Drag and Drop:
   - [ ] Drag a file from Windows Explorer on one PC, cross the screen border onto another PC, and release it there. Verify that the file is copied to the other PC. Make sure the file size is below 100MB.
   - [ ] While dragging the file, verify that a corresponding icon is displayed under the mouse cursor.
   - [ ] Without moving the mouse from one PC to the target PC, press CTRL+ALT+F1/2/3/4 hotkey to switch to the target PC directly and verify that file sharing/dropping is not working.

 * Lock and Unlock with "Use Service" Enabled:
   - [ ] Enable "Use Service" in MWB's settings.
   - [ ] Lock a remote PC using Win+L, move the mouse to it remotely, and try to unlock it. Verify that you can unlock the remote PC.
   - [ ] Disable "Use Service" in MWB's settings, lock the remote PC, move the mouse to it remotely, and try to unlock it. Verify that you can't unlock the remote PC.

 * Test Settings:
   - [ ] Change the rest of available settings on MWB page and verify that each setting works as described.

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

## Command Not Found
 * Go to Command Not Found module settings
 - [x] If you have PowerShell 7.4 installed, confirm that Install PowerShell 7.4 button is not visible and PowerShell 7.4 is shown as detected. If you don't have PowerShell 7.4, Install it by clicking the button and confirm that it's properly installed. Check Installation logs text box bellow and confirm there are no errors.
 - [x] If you have Microsoft.WinGet.Client installed, confirm that Install Microsoft.WinGet.Client button is not visible and Microsoft.WinGet.Client is shown as detected. If you don't have Microsoft.WinGet.Client, Install it by clicking the button and confirm that it's properly installed. Check Installation logs text box bellow and confirm there are no errors.
 - [x] Install the Command Not Found module. Check Installation logs text box bellow and confirm there are no errors. Check PowerShell 7 $PROFILE file and confirm Import-Module command is added there. Start new PowerShell 7.4 session and execute "powertoys" (or "atom"). Confirm that suggestion is given to install powertoys (or atom) winget package. (If suggestion is not given, try running the same command few more times, it might take some time for the first time to load the module). Check Installation logs text box bellow and confirm there are no errors.
 - [x] Uninstall the module. Check Installation logs text box bellow and confirm there are no errors. Check PowerShell 7 $PROFILE file and confirm Import-Module command is removed. Start new PowerShell 7.4 session and confirm no errors are shown on start.
 - [x] Install module again. Uninstall PowerToys. Check PowerShell 7 $PROFILE file and confirm Import-Module command is removed after installer is done.

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

