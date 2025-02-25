## Telemetry Prologue
 * Before starting your tests, go to General settings, enable "Diagnostic data" and "Enable viewing" and restart PowerToys.

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

## Workspaces
* Settings
   - [ ] Launch the Editor by clicking the button on the settings page.
   - [ ] Launch the Editor from quick access.
   - [ ] Launch the Editor by the Activation shortcut.
   - [ ] Disable the module and and verify it won't launch by the shortcut.

* Snapshot tool: try with elevated and non-elevated PT
   * Open non-packaged apps, e.g., VisualStudio, VisualStudioCode.
   * Open packaged apps, e.g., Notepad, Settings.
   * Run any app as an administrator.
   * Minimize any app.
   * Click `Create Workspace`.
   * Open any other window.
   * Click `Capture`
   - [ ] Verify Editor shows all opened windows (the elevated window will be captured if PT is also elevated).
   - [ ] Verify windows are in the correct positions.
   - [ ] Verify elevated app has the `Admin` box checked (if captured).

* Editor
   - [ ] Verify that the new Workspace appears in the list after capturing.
   - [ ] Verify that the new Workspace doesn't appear after canceling the Capture.
   - [ ] Verify `Search` filters Workspaces (by workspace name or app name).
   - [ ] Verify `SortBy` works.
   - [ ] Verify `SortBy` keeps its value when you close and open the editor again.
   - [ ] Verify `Remove` removes the Workspace from the list.
   - [ ] Verify `Edit` opens the Workspace editing page.
   - [ ] Verify clicking at the Workspace opens the Workspace editing page.
   
   * Editing page
   - [ ] `Remove` an app and verify it disappeared on the preview.
   - [ ] `Remove` and `Add back` an app, verify it's returned back to the preview.
   - [ ] Set an app minimized, check the preview.
   - [ ] Set an app maximized, check the preview.
   - [ ] Check `Launch as admin` for the app where it's available.
   - [ ] Add CLI args, e.g. path to the PowerToys.sln file for VisualStudio.
   - [ ] Manually change the position for the app, check the preview.
   - [ ] Change the Workspace name.
   - [ ] Verify `Save` and `Cancel` work as expected. 
   - [ ] Change anything in the project, click at the `Workspaces` on the top of the page, and verify you returned to the main page without saving any changes.
   - [ ] Check `Create desktop shortcut`, save the project, verify the shortcut appears on the desktop. 
   - [ ] Verify that `Create desktop shortcut` is checked when the shortcut is on the desktop and unchecked when there is no shortcut on the desktop. 
   - [ ] Click `Launch and Edit`, wait for the apps to launch, click `Capture`, verify opened apps are added to the project.

* Launcher
   - [ ] Click `Launch` in the editor, verify the Workspace apps launching.
   - [ ] Launch Workspace by a shortcut, verify the Workspace apps launching.
   - [ ] Verify a window with launching progress is shown while apps are launching and presents the correct launching state (launching, launched, not launched) for every app.
   - [ ] Click `Cancel launch`, verify launching is stopped at the current state (opened apps will stay opened), and the window is closed.
   - [ ] Click `Dismiss` and verify apps keep launching, but the LauncherUI window is closed.
   
* To verify that the launcher works correctly with different apps, try to capture and launch:   
   - [ ] Non-packaged app, e.g., VisualStudio code
      - [ ] As admin
      - [ ] With CLI args 
    - [ ] Packaged app, e.g. Terminal
      - [ ] As admin
      - [ ] With CLI args

* Try to launch the Workspace with a different setup
   - [ ] Create a Workspace with one monitor connected, connect the second monitor, launch the Workspace, verify apps are opened on the first one, as captured.
   - [ ] Create a Workspace with two monitors connected, disconnect a monitor, verify apps are opened on the remaining one.

## ZoomIt

 * Enable ZoomIt in Settings.
   - [ ] Verify ZoomIt tray icon appears in the tray icons, and that when you left-click or right-click, it just shows the 4 action entries: "Break Timer", "Draw", "Zoom" and "Record".
   - [ ] Turn the "Show tray icon" option off and verify the tray icon is gone.
   - [ ] Turn the "Show tray icon" option on and verify the tray icon is back.
 * Test the base modes through a shortcuts:
   - [ ] Press the Zoom Toggle Hotkey and verify ZoomIt zooms in on the mouse. You can exit Zoom by pressing Escape or the Hotkey again.
   - [ ] Press the Live Zoom Toggle Hotkey and verify ZoomIt zooms in on the mouse, while the screen still updates instead of showing a still image. You can exit Live Zoom by pressing the Hotkey again.
   - [ ] Press the Draw without Zoom Hotkey and verify you can draw. You can leave this mode by pressing the Escape.
   - [ ] Select a text file as the Input file for Demo Type, focus notepad and press the Demo Type hotkey. It should start typing the text file. You can exit Demo Type by pressing Escape.
   - [ ] Press the Start Break Timer Hotkey and verify it starts the Timer. You can exit by pressing Escape.
   - [ ] Press the Record Toggle Hotkey to start recording a screen. Press the Record Toggle Hotkey again to exit the mode and save the recording to a file.
   - [ ] Press the Snip Toggle Hotkey to take a snip of the screen. Paste it to Paint to verify a snip was taken.
 * Test some Settings to verify the types are being passed correctly to ZoomIt:
   - [ ] Change the "Animate zoom in and zoom out" setting and activate Zoom mode to verify it applies.
   - [ ] Change the "Specify the initial level of magnification when zooming in" and activate Zoom mode to verify it applies.
   - [ ] Change the Type Font to another font. Enter Break mode to quickly verify the font changed.
   - [ ] Change the Demo Type typing speed and verify the change applies.
   - [ ] Change the timer Opacity for Break mode and verify that the change applies.
   - [ ] Change the timer Position for Break mode and verify that the change applies.
   - [ ] Select a Background Image file as background for Break mode and verify that the change applies.
   - [ ] Turn on "Play Sound on Expiration", select a sound file, aset the timer to 1 minute, activate the Break Mode and verify the sound plays after 1 minute. (Alarm1.wav from "C:\Windows\Media" should be long enough to notice)
   - [ ] Open the Microphone combo box in the Record section and verify it lists your microphones.
 * Test the tray icon actions:
   - [ ] Verify pressing "Break Timer" enters Break mode.
   - [ ] Verify pressing "Draw" enters Draw mode.
   - [ ] Verify pressing "Zoom" enters Zoom mode.
   - [ ] Verify pressing "Record" enters Record mode.

## Telemetry Epilogue
 * After finishing your tests, go to General settings, press Diagnostic data viewer and check if you have xml files for the utilities you've tested and if it looks like the events in those xml files were generated by the actions you did with the utilities you've tested.
