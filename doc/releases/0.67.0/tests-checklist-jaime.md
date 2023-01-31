## Image Resizer
- [ ] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [ ] Enable the Image Resizer and check that `Resize images` is present in the context menu
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

## Awake
 - [ ] Try out the features and see if they work, no list at this time.

### Text Extractor
 * Enable Text Extractor. Then:
   - [ ] Press the activation shortcut and verify the overlay appears.
   - [ ] Press Escape and verify the overlay disappears.
   - [ ] Press the activation shortcut and verify the overlay appears.
   - [ ] Right-click and select Cancel. Verify the overlay disappears.
   - [ ] Disable Text Extractor and verify that the activation shortuct no longer activates the utility.
 * With Text Extractor enabled and activated:
   - [ ] Try to select text and verify it is copied to the clipboard.
   - [ ] Try to select a different OCR language by right-clicking and verify the change is applied.
 * In a multi-monitor setup with different dpis on each monitor:
   - [ ] Verify text is correctly captured on all monitors.
 * Test the different settings and verify they are applied:
   - [ ] Activation shortcut
   - [ ] OCR Language

### File Locksmith
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

### Hosts File Editor
 * Launch Host File Editor:
   - [ ] Verify the application exits if "Quit" is clicked on the initial warning.
   - [ ] Launch Host File Editor again and click "Accept". The module should not close. Open the hosts file (`%WinDir%\System32\Drivers\Etc`) in a text editor that auto-refreshes so you can see the changes applied by the editor in real time. (VSCode is an editor like this, for example)
   - [ ] Enable and disable lines and verify they are applied to the file.
   - [ ] Add a new entry and verify it's applied.
   - [ ] Try to filter for lines and verify you can find them.
   - [ ] Click the "Open hosts file" button and verify it opens in your default editor. (likely Notepad)
 * Test the different settings and verify they are applied:
   - [ ] Launch as Administrator.
   - [ ] Show a warning at startup.
   - [ ] Additional lines position.

### GPO
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
