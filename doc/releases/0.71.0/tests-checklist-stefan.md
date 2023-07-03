## Functional tests

 Regressions:
 - [x] https://github.com/microsoft/PowerToys/issues/1414#issuecomment-593529038
 - [x] https://github.com/microsoft/PowerToys/issues/1524

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
   - [X] verify the Icon Preview works for the stl file (loop through different icon preview sizes)
   * go to PowerToys repo and visit src\runner
   - [X] verify Preview Pane works for source files (shows syntax highlighting)
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
   - [X] verify Preview Pane doesn't show the preview for source code files or that it's a default previewer instead of Monaco

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
    - [X] Item Name/Extension Only (one at the time)
    - [X] Enumerate Items
    - [x] Case Sensitive
    - [x] Match All Occurrences. If checked, all matches of text in the `Search` field will be replaced with the Replace text. Otherwise, only the first instance of the `Search` for text in the file name will be replaced (left to right).
    * Use regular expressions
        - [X] Search with an expression (e.g. `(.*).png`)
        - [x] Replace with an expression (e.g. `foo_$1.png`)
        - [x] Replace using file creation date and time (e.g. `$hh-$mm-$ss-$fff` `$DD_$MMMM_$YYYY`)
        - [x] Turn on `Use Boost library` and test with Perl Regular Expression Syntax (e.g. `(?<=t)est`)
    * File list filters.
        - [x] In the `preview` window uncheck some items to exclude them from renaming.
        - [x] Click on the `Renamed` column to filter results.
        - [x] Click on the `Original` column to cycle between checked and unchecked items.

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

## Always on Top
 - [x] Pin/unpin a window, verify it's topmost/not topmost.
 - [x] Pin/unpin a window, verify the border appeared/disappeared.
 - [x] Switch virtual desktop, verify border doesn't show up on another desktop.
 - [x] Minimize and maximize pinned window, verify the border looks as usual.
 - [x] Change border color and thickness.
 - [x] Verify if sound is played according to the sound setting.
 - [X] Exclude app, try to pin it.
 - [x] Exclude already pinned app, verify it was unpinned.
 - [x] Try to pin the app in the Game Mode.

### Quick Accent
 * Enable Quick Accent and open notepad. Then:
   - [x] Press `a` and the left or right arrow and verify the accent menu appears and adds the accented letter you've selected. Use left and arrow keys to cycle through the options.
   - [x] Press `a` and the space key and verify the accent menu appears and adds the accented letter you've selected. Use <kbd>Space</kbd> to navigate forward, <kbd>Space</kbd> + <kbd>Shift</kbd> to navigate backward.
   - [x] Disable Quick Accent and verify you can no longer add accented characters through Quick Accent.
 * Test the different settings and verify they are applied:
   - [x] Activation key
   - [x] Language (for example, Currency has no accents for 'a' but has for 's')
   - [x] Toolbar position (test every option, some had issues before)
   - [x] Input delay
   - [x] Exclude some apps. Verify that Quick Accent is not activated for them.
   - [x] Sort characters by frequency.
   - [x] Always start on the first character when using left/right arrows as activation method.

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

### Registry Preview
 * Open Registry Editor, add new registry key with 1 string value and 1 binary value in e.g. HKLM/Software/Classes/PowerToysTest. Right click new registry key->export and export it to file.
 * Launch Registry Preview by right-clicking exported .reg file->'Preview'. Then:
   - [x] Uncheck ReadOnly checkbox and edit file content. Ensure that visual try is being re-populated while typing. Save the file by pressing Save file button. Confirm that file is properly saved by pressing Edit file... button which will open file in Notepad. Try saving file using Save file as... button.
   - [x] Edit file externaly (e.g. in Notepad) and save it there. Pres Reload from file button and ensure that file content and visual tree are reloaded and show new content.
   - [x] Select some registry key with registry values in visual tree and ensure that registry values are shown properly in bottom-right area.
   - [x] Try opening different registry file by pressing Open file button.
   - [x] Delete newly created registry key from first step manually in Registry Editor, then try writing registry changes to registry by pressing Write to Registry button in Registry Preview. *Be careful what you are writing!* 
   
 * Open Registry Preview Settings. Then:
   - [x] Disable Registry Preview and ensure that Preview context menu option for .reg files no longer appears.
   - [x] Try to launch Registry Preview from it's OOBE page while Registry Preview is disabled and ensure that it does not start.
   - [x] Enable Registry Preview again and ensure that Preview context menu option for .reg files appears and that it starts Registry Preview correctly. 
   - [x] Try to launch Registry Preview from it's Settings page and ensure that it is launched properly.
   - [x] Try to launch Registry Preview from it's OOBE page and ensure that it is launched properly.
   - [x] Enable Default app setting. Verify that .reg files are opened with Registry Preview by default. Disable Default app setting. Verify that Registry Editor is now default app.
   
### Peek   
 * Open different files to check that they're shown properly
   - [x] Image
   - [x] Text or dev file
   - [x] Markdown file
   - [x] PDF
   - [X] HTML
   - [x] Any other not mentioned file (.zip for example) to verify the unsupported file view is shown
   
 * Pinning/unpinning
   - [x] Pin the window, switch between images of different size, verify the window stays at the same place and the same size.
   - [x] Pin the window, close and reopen Peek, verify the new window is opened at the same place and the same size as before.
   - [x] Unpin the window, switch to a different file, verify the window is moved to the default place.
   - [X] Unpin the window, close and reopen Peek, verify the new window is opened on the default place.

* Open with a default program
   - [x] By clicking a button.
   - [x] By pressing enter. 
  
 - [x] Switch between files in the folder using `LeftArrow` and `RightArrow`, verify you can switch between all files in the folder.
 - [x] Open multiple files, verify you can switch only between selected files.
 - [x] Change the shortcut, verify the new one works.

