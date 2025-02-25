## Telemetry Prologue
 * Before starting your tests, go to General settings, enable "Diagnostic data" and "Enable viewing" and restart PowerToys.

## Localization
 Change the Windows language to a language different than English. Then verify if the following screens change their language:
 - [ ] System tray menu items
 - [ ] Settings
 - [ ] OOBE (What's new)
 - [ ] Keyboard Manager Editor
 - [ ] Color Picker (check the tooltips)
 - [ ] FancyZones Editor
 - [ ] Power Rename (new WinUI 3 may not be localized)
 - [ ] PowerToys Run ("Start typing" string is localized, for example)
 - [ ] Image Resizer
 - [ ] Shortcut Guide (Windows controls are localized)
 - [ ] File Explorer menu entries for Image Resizer, Power Rename and FileLocksmith
 - [ ] Hosts File Editor
 - [ ] File Locksmith
 - [ ] Registry Preview
 - [ ] Environment Variables

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

## Image Resizer
- [ ] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [ ] Enable the Image Resizer and check that `Resize images` is present in the context menu. (On Win11) Check if both old context menu and Win11 tier1 context menu items are present when module is enabled.
- [ ] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [ ] Resize one image
- [ ] Resize multiple images
- [ ] Open the image resizer to resize a `.gif` file and verify the "Gif files with animations may not be correctly resized." warning appears.

- [ ] Resize images with `Fill` option
- [ ] Resize images with `Fit` option
- [ ] Resize images with `Stretch` option

- [ ] Resize images using dimension: Centimeters
- [ ] Resize images using dimension: Inches
- [ ] Resize images using dimension: Percents
- [ ] Resize images using dimension: Pixels

- [ ] Change `Filename format` to `%1 - %2 - %3 - %4 - %5 - %6` and check if the new format is applied to resized images
- [ ] Check `Use original date modified` and verify that modified date is not changed for resized images. Take into account that `Resize the original pictures(don't create copy)` should be selected
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
    - [ ] Similar remaps to above with Edge (entered as `msedge`), VSCode (entered as `code`) and cmd. For cmd try admin and non-admin (requires PT to run as admin)
    - [ ] Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - [ ] Test switching between remapping while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

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

## Screen Ruler
 * Enable Screen Ruler. Then:
   - [ ] Press the activation shortcut and verify the toolbar appears.
   - [ ] Press the activation shortcut again and verify the toolbar disappears.
   - [ ] Disable Screen Ruler and verify that the activation shortuct no longer activates the utility.
   - [ ] Enable Screen Ruler and press the activation shortcut and verify the toolbar appears.
   - [ ] Select the close button in the toolbar and verify it closes the utility.
 * With Screen Ruler enabled and activated:
   - [ ] Use the Bounds utility to measure a zone by dragging with left-click. Verify right click dismisses the utility and that the measurement was copied into the clipboard.
   - [ ] Use the Spacing utility to measure something and verify that left-click copies the measurement to the clipboard. Verify that right-click dismisses the utility.
   - [ ] Use the Horizontal Spacing utility to measure something and verify that left-click copies the measurement to the clipboard. Verify that right-click dismisses the utility.
   - [ ] Use the Vertical Spacing utility to measure something and verify that left-click copies the measurement to the clipboard. Verify that right-click dismisses the utility.
   - [ ] While using a Spacing utility, verify that using the mouse scroll wheel will adjust pixel color tolerance while measuring.
   - [ ] Open mspaint and draw 1px-thick straight line, also click with a pencil to draw a single pixel. In any Spacing mode, verify that one of line's dimension is 1, and pixel's dimensions are 1x1.
 * In a multi-monitor setup with different dpis on each monitor:
   - [ ] Verify that the utilities work well on each monitor, with continuous mode on and off.
   - [ ] Without any window opened and a solid color as your background, verify the horizontal spacing matches the monitor's pixel width.
   - [ ] Move your mouse back and forth around the edge of two monitors really quickly in each mode - verify nothing is broken.
   
 * Test the different settings and verify they are applied:
   - [ ] Activation shortcut
   - [ ] Continous mode
   - [ ] Per color channel edge detection
   - [ ] Pixel tolerance for edge detection
   - [ ] Draw feet on cross
   - [ ] Line color

## Quick Accent
 * Enable Quick Accent and open notepad. Then:
   - [ ] Press `a` and the left or right arrow and verify the accent menu appears and adds the accented letter you've selected. Use left and arrow keys to cycle through the options.
   - [ ] Press `a` and the space key and verify the accent menu appears and adds the accented letter you've selected. Use <kbd>Space</kbd> to navigate forward, <kbd>Space</kbd> + <kbd>Shift</kbd> to navigate backward.
   - [ ] Disable Quick Accent and verify you can no longer add accented characters through Quick Accent.
 * Test the different settings and verify they are applied:
   - [ ] Activation key
   - [ ] Language (for example, Currency has no accents for 'a' but has for 's')
   - [ ] Toolbar position (test every option, some had issues before)
   - [ ] Input delay
   - [ ] Exclude some apps. Verify that Quick Accent is not activated for them.
   - [ ] Sort characters by frequency.
   - [ ] Always start on the first character when using left/right arrows as activation method.

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

## Command Not Found
 * Go to Command Not Found module settings
 - [ ] If you have PowerShell 7.4 installed, confirm that Install PowerShell 7.4 button is not visible and PowerShell 7.4 is shown as detected. If you don't have PowerShell 7.4, Install it by clicking the button and confirm that it's properly installed. Check Installation logs text box bellow and confirm there are no errors.
 - [ ] If you have Microsoft.WinGet.Client installed, confirm that Install Microsoft.WinGet.Client button is not visible and Microsoft.WinGet.Client is shown as detected. If you don't have Microsoft.WinGet.Client, Install it by clicking the button and confirm that it's properly installed. Check Installation logs text box bellow and confirm there are no errors.
 - [ ] Install the Command Not Found module. Check Installation logs text box bellow and confirm there are no errors. Check PowerShell 7 $PROFILE file and confirm Import-Module command is added there. Start new PowerShell 7.4 session and execute "powertoys" (or "atom"). Confirm that suggestion is given to install powertoys (or atom) winget package. (If suggestion is not given, try running the same command few more times, it might take some time for the first time to load the module). Check Installation logs text box bellow and confirm there are no errors.
 - [ ] Uninstall the module. Check Installation logs text box bellow and confirm there are no errors. Check PowerShell 7 $PROFILE file and confirm Import-Module command is removed. Start new PowerShell 7.4 session and confirm no errors are shown on start.
 - [ ] Install module again. Uninstall PowerToys. Check PowerShell 7 $PROFILE file and confirm Import-Module command is removed after installer is done.

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
