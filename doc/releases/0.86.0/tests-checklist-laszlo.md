## Telemetry Prologue
 * Before starting your tests, go to General settings, enable "Diagnostic data" and "Enable viewing" and restart PowerToys.

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

## PowerRename
- [ ] Check if disable and enable of the module works. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
- [ ] Check that with the `Show icon on context menu` icon is shown and vice versa.
- [ ] Check if `Appear only in extended context menu` works.
- [ ] Enable/disable autocomplete.
- [ ] Enable/disable `Show values from last use`.
* Select several files and folders and check PowerRename options:
    - [ ] Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
    - [ ] Exclude Folders/Files/Subfolder Items (could be selected several)
    - [ ] Item Name/Extension Only (one at the time)
    - [ ] Enumerate Items. Test advanced enumeration using different values for every field ${start=10,increment=2,padding=4}.
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
   - [ ] Verify `Win + Shift + /` opens the guide
   - [ ] Change the hotkey to a different shortcut (e.g. `Win + /`) and verify it works
   - [ ] Set Shortcut Guide to start with a Windows key press and verify it works.
 * Restore the `Win + Shift + /` hotkey.
   - [ ] Open the guide and close it pressing `Esc`
   - [ ] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [ ] Verify `Win + Shift + /` opens the guide
   - [ ] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

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
    - [ ] Press Left Ctrl twice and verify the overlay appears.
    - [ ] Press any other key and verify the overlay disappears.
    - [ ] Press Left Ctrl twice and verify the overlay appears.
    - [ ] Press a mouse button and verify the overlay disappears.
  * Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice.
  * Enable FindMyMouse. Then, without moving your mouse:
    - [ ] Press Left Ctrl twice and verify the overlay appears.
  * Enable the "Do not activate on game mode" option. Start playing a game that uses CG native full screen.
    - [ ] Verify the overlay no longer appears when you press Left Ctrl twice.
  * Disable the "Do not activate on game mode" option. Start playing the same game.
    - [ ] Verify the overlay appears when you press Left Ctrl twice. (though it'll likely minimize the game)
  * Test the different settings and verify they apply:
    - [ ] Overlay opacity
    - [ ] Background color
    - [ ] Spotlight color
    - [ ] Spotlight radius
    - [ ] Spotlight initial zoom (1x vs 9x will show the difference)
    - [ ] Animation duration
    - [ ] Change activation method to shake and activate by shaking your mouse pointer
    - [ ] Excluded apps

Mouse Highlighter:
  * Enable Mouse Highlighter. Then:
    - [ ] Press the activation shortcut and press left and right click somewhere, verifying the hightlights are applied.
    - [ ] With left mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [ ] With right mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [ ] Press the activation shortcut again and verify no highlights appear when the mouse buttons are clicked.
    - [ ] Disable Mouse Highlighter and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [ ] Change activation shortcut and test it
    - [ ] Left button highlight color
    - [ ] Right button highlight color
    - [ ] Opacity
    - [ ] Radius
    - [ ] Fade delay
    - [ ] Fade duration

Mouse Pointer Crosshairs:
  * Enable Mouse Pointer Crosshairs. Then:
    - [ ] Press the activation shortcut and verify the crosshairs appear, and that they follow the mouse around.
    - [ ] Press the activation shortcut again and verify the crosshairs disappear.
    - [ ] Disable Mouse Pointer Crosshairs and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [ ] Change activation shortcut and test it
    - [ ] Crosshairs color
    - [ ] Crosshairs opacity
    - [ ] Crosshairs center radius
    - [ ] Crosshairs thickness
    - [ ] Crosshairs border color
    - [ ] Crosshairs border size

Mouse Jump:
  * Enable Mouse Jump. Then:
    - [ ] Press the activation shortcut and verify the screens preview appears.
    - [ ] Change activation shortcut and verify that new shorctut triggers Mouse Jump.
    - [ ] Click around the screen preview and ensure that mouse cursor jumped to clicked location.
    - [ ] Reorder screens in Display settings and confirm that Mouse Jump reflects the change and still works correctly.
    - [ ] Change scaling of screens and confirm that Mouse Jump still works correctly.
    - [ ] Unplug additional monitors and confirm that Mouse Jump still works correctly.
    - [ ] Disable Mouse Jump and verify that the module is not activated when you press the activation shortcut.

## VCM
 - [ ] Check "Hide toolbar when both camera and microphone are unmuted" and verify that it works
 - [ ] Uncheck it, mute the microphone with the hotkey and make sure the toolbar doesn't hide after a timeout
 - [ ] Go to some video conference application settings, e.g. meet.google.com, Microsoft Teams, Skype. "Select PowerToys VideoConference Mute" camera as an active device and try to mute it with a hotkey
 - [ ] Go to Control Panel -> Sound -> Recording -> select default mic -> open its properties -> Levels.  Now mute the microphone with a corresponding hotkey and verify that mute icon on the right side of volume slider reflects its muted status.
 - [ ] Go to Control Panel -> Sound -> Recording -> select default mic -> open its properties -> Levels.  Now press and release push-to-talk hotkey and verify that mute icon on the right side of volume slider reflects the actions.
 - [ ] Verify that changing "toolbar position" setting works 
 - [ ] Select an overlay image and verify that muting camera now shows the image instead of black screen. (Don't forget to restart the application which uses the camera).
 - [ ] Try to select an overlay image when PT process is elevated.

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

## Hosts File Editor
 * Launch Host File Editor:
   - [ ] Verify the application exits if "Quit" is clicked on the initial warning.
   - [ ] Launch Host File Editor again and click "Accept". The module should not close. Open the hosts file (`%WinDir%\System32\Drivers\Etc`) in a text editor that auto-refreshes so you can see the changes applied by the editor in real time. (VSCode is an editor like this, for example)
   - [ ] Enable and disable lines and verify they are applied to the file.
   - [ ] Add a new entry and verify it's applied.
   - [ ] Add manually an entry with more than 9 hosts in hosts file (Windows limitation) and verify it is split correctly on loading and the info bar is shown.
   - [ ] Try to filter for lines and verify you can find them.
   - [ ] Click the "Open hosts file" button and verify it opens in your default editor. (likely Notepad)
 * Test the different settings and verify they are applied:
   - [ ] Launch as Administrator.
   - [ ] Show a warning at startup.
   - [ ] Additional lines position.

## File Locksmith
 * Start the PowerToys installer executable and let it stay in the initial screen.
   - [ ] Right-click the executable file, select "What's using this file?" and verify it shows up. (2 entries will show, since the installer starts two processes)
   - [ ] End the tasks in File Locksmith UI and verify that closes the installer.
   - [ ] Start the installer executable again and press the Refresh button in File Locksmith UI. It should find new processes using the files.
   - [ ] Close the installer window and verify the processes are delisted from the File Locksmith UI. Close the window
 * Start the PowerToys installer executable again and let it stay in the initial screen.
   - [ ] Right click the directory where the executable is located, select "What's using this file?" and verify it shows up. 
   - [ ] Right click the drive where the executable is located, select "What's using this file?" and verify it shows up. You can close the PowerToys installer now.
 * Restart PowerToys as admin.
   - [ ] Right click "Program Files", select "What's using this file?" and verify "PowerToys.exe" doesn't show up.
   - [ ] Press the File Locksmith "Restart as an administrator" button and verify "PowerToys.exe" shows up.
 - [ ] Right-click the drive where Windows is installed, select "What's using this file?" and scroll down and up, verify File Locksmith doesn't crash with all those entries being shown. Repeat after clicking the File Locksmith "Restart as an administrator" button.
 - [ ] Disable File Locksmith in Settings and verify the context menu entry no longer appears.

## Registry Preview
 * Open Registry Editor, add new registry key with 1 string value and 1 binary value in e.g. HKLM/Software/Classes/PowerToysTest. Right click new registry key->export and export it to file.
 * Launch Registry Preview by right-clicking exported .reg file->'Preview'. Then:
   - [ ] Edit file content. Ensure that visual try is being re-populated while typing. Save the file by pressing Save file button. Confirm that file is properly saved by pressing Edit file... button which will open file in Notepad. Try saving file using Save file as... button.
   - [ ] Edit file externaly (e.g. in Notepad) and save it there. Pres Reload from file button and ensure that file content and visual tree are reloaded and show new content.
   - [ ] Select some registry key with registry values in visual tree and ensure that registry values are shown properly in bottom-right area.
   - [ ] Try opening different registry file by pressing Open file button.
   - [ ] Delete newly created registry key from first step manually in Registry Editor, then try writing registry changes to registry by pressing Write to Registry button in Registry Preview. *Be careful what you are writing!* 
   
 * Open Registry Preview Settings. Then:
   - [ ] Disable Registry Preview and ensure that Preview context menu option for .reg files no longer appears.
   - [ ] Try to launch Registry Preview from it's OOBE page while Registry Preview is disabled and ensure that it does not start.
   - [ ] Enable Registry Preview again and ensure that Preview context menu option for .reg files appears and that it starts Registry Preview correctly. 
   - [ ] Try to launch Registry Preview from it's Settings page and ensure that it is launched properly.
   - [ ] Try to launch Registry Preview from it's OOBE page and ensure that it is launched properly.
   - [ ] Enable Default app setting. Verify that .reg files are opened with Registry Preview by default. Disable Default app setting. Verify that Registry Editor is now default app.

## Peek
 * Open different files to check that they're shown properly
   - [ ] Image
   - [ ] Text or dev file
   - [ ] Markdown file
   - [ ] PDF
   - [ ] HTML
   - [ ] Archive files (.zip, .tar, .rar)
   - [ ] Any other not mentioned file (.exe for example) to verify the unsupported file view is shown
   
 * Pinning/unpinning
   - [ ] Pin the window, switch between images of different size, verify the window stays at the same place and the same size.
   - [ ] Pin the window, close and reopen Peek, verify the new window is opened at the same place and the same size as before.
   - [ ] Unpin the window, switch to a different file, verify the window is moved to the default place.
   - [ ] Unpin the window, close and reopen Peek, verify the new window is opened on the default place.

* Open with a default program
   - [ ] By clicking a button.
   - [ ] By pressing enter. 
  
 - [ ] Switch between files in the folder using `LeftArrow` and `RightArrow`, verify you can switch between all files in the folder.
 - [ ] Open multiple files, verify you can switch only between selected files.
 - [ ] Change the shortcut, verify the new one works.

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

## Environment Variables
 * NOTE: Make backup of USER and SYSTEM Path and TMP variables before testing so you can revert those is something goes wrong!
 * Open Environment Variables settings
   - [ ] Launch as administrator ON - Launch Environment Variables and confirm that SYSTEM variables ARE editable and Add variable button is enabled
   - [ ] Launch as administrator OFF - Launch Environment Variables and confirm that SYSTEM variables ARE NOT editable and Add variable button is disabled

 * User/System variables
   - [ ] Add new User variable. Open OS Environment variables window and confirm that added variable is there. Also, confirm that it's added to "Applied variables" list.
   - [ ] Edit one User variable. Open OS Environment variables window and confirm that variable is changed. Also, confirm that change is applied to "Applied variables" list.
   - [ ] Remove one User variable. Open OS Environment variables window and confirm that variable is removed. Also, confirm that variable is removed from "Applied variables" list.
   - Repeat the steps for System variables.

 * Profiles - Basic tests
   - [ ] Add new profile with no variables and name it "Test_profile_1" (referenced below by name)
   - [ ] Edit "Test_profile_1": Add one new variable to profile e.g. name: "profile_1_variable_1" value: "profile_1_value_1"
   - [ ] Add new profile "Test_profile_2": From "Add profile dialog" add two new variables (profile_2_variable_1:profile_2_value_1 and profile_2_variable_2:profile_2_value_2). Set profile to enabled and click Save. Open OS Environment variables window and confirm that all variables from the profile are applied correctly. Also, confirm that "Applied variables" list contains all variables from the profile.
   - [ ] Apply "Test_profile_1" while "Test_profile_2" is still aplpied. Open OS Environment variables window and confirm that all variables from Test_profile_2 are unapplied and that all variables from Test_profile_1 are applied. Also, confirm that state of "Applied variables" list is updated correctly.
   - [ ] Unapply applied profile. Open OS Environment variables window and confirm that all variables from the profile are unapplied correctly. Also, confirm that "Applied variables" list does not contain variables from the profile.

 * Overriding existing variable
   - [ ] To "Test_profile_1" add one existing variable from USER variables, e.g. TMP. After adding, change it's value to e.g "test_TMP" (or manually add variable named TMP with value test_TMP).
   - [ ] Apply "Test_profile_1". Open OS Environment variables window and confirm that TMP variable in USER variables has value "test_TMP". Confirm that there is backup variable "TMP_PowerToys_Test_profile_1" with original value of TMP var. Also, confirm that "Applied variables" list is updated correctly - there is TMP profile variable, and backup User variable..
   - [ ] Unapply "Test_profile_1". Open OS Environment variables window and confirm that TMP variable in USER variable has original value and that there is no backup variable. Also, confirm that "Applied variables" list is updated correctly.

 * PATH variable
   - [ ] In "Applied variables" list confirm that PATH variable is shown properly: value of USER Path concatenated to the end of SYSTEM Path.
   - [ ] To "Test_profile_1" add variable named PATH with value "path1;path2;path3" and click Save. Confirm that PATH variable in profile is shown as list (list of 3 values and not as path1;path2;path3).
   - [ ] Edit PATH variable from "Test_profile_1". Try different options from ... menu (Delete, Move up, Move down, etc...). Click Save.
   - [ ] Apply "Test_profile_1". Open OS Environment variables window and confirm that profile is applied correctly - Path value and backup variable. Also, in "Applied variables" list check that Path variable has correct value: value of profile PATH concatenated to the end of SYSTEM Path.

 * Loading profiles on startup
   - [ ] Close the app and reopen it. Confirm that the state of the app is the same as before closing.

 - [ ] "Test_profile_1" should still be applied (if not apply it). Delete "Test_profile_1". Confirm that profile is unapplied (both in OS Environment variables window and "Applied variables" list).
 - [ ] Delete "Test_profile_2". Check profiles.json file and confirm that both profiles are gone.

## DSC
 * You need to have some prerequisites installed:
   - PowerShell >= 7.2 .
   - PSDesiredStateConfiguration 2.0.7 or higher `Install-Module -Name PSDesiredStateConfiguration`.
   - WinGet [version v1.6.2631 or later](https://github.com/microsoft/winget-cli/releases). (You'll likely have this one already)
 * Open a PowerShell 7 instance and navigate to the sample scripts from PowerToys (`src/dsc/Microsoft.PowerToys.Configure/examples/`).
   - [ ] Run `winget configure .\disableAllModules.dsc.yaml`. Open PowerToys Settings and verify all modules are disabled.
   - [ ] Run `winget configure .\enableAllModules.dsc.yaml`. Open PowerToys Settings and verify all modules are enabled.
   - [ ] Run `winget configure .\configureLauncherPlugins.dsc.yaml`. Open PowerToys Settings and verify all PowerToys Run plugins are enabled, and the Program plugin is not global and its Activation Keyword has changed to "P:".
   - [ ] Run `winget configure .\configuration.dsc.yaml`. Open PowerToys Settings the Settings have been applied. File Locksmith is disabled. Shortcut Guide is disabled with an overlay opacity set to 50. FancyZones is enabled with the Editor hotkey set to "Shift+Ctrl+Alt+F".
   - [ ] If you run a winget configure command above and PowerToys is running, it will eventually close and automatically reopen after the configuration process is done.
   - [ ] If you run a winget configure command above and PowerToys is not running, it won't automatically reopen after the configuration process is done.

## Telemetry Epilogue
 * After finishing your tests, go to General settings, press Diagnostic data viewer and check if you have xml files for the utilities you've tested and if it looks like the events in those xml files were generated by the actions you did with the utilities you've tested.

