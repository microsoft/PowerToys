## Install tests
 * install a **previous version** on a clean machine (a clean machine doesn't have the `%localappdata%\Microsoft\PowerToys` folder)
 * open the Settings and for each module change at least one option
 * open the FancyZones editor and create two custom layouts:
    * a canvas layout with 2 zones, use unicode chars in the layout's name
    * one from grid template using 4 zones and splitting one zone
    * apply the custom canvas layout to the primary desktop
    * create a virtual desktop and apply the custom grid layout
    * if you have a second monitor apply different templates layouts for the primary desktop and for the second virtual desktop
 * install the new version (it will uninstall the old version and install the new version)
 - [x] verify the settings are preserved and FancyZones configuration is still the same

## General Settings

**Admin mode:**
 - [x] restart PT and verify it runs as user
 - [x] restart as admin and set "Always run as admin"
 - [x] restart PT and verify it  runs as admin
 * if it's not on, turn on "Run at startup"
 - [x] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [x] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [x] turn off all the modules and verify all module are off
 - [x] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [x] turn on all the module, all module are now working
 - [x] restart PT and verify that all module are still on in the settings page and they are actually working

## Shortcut Guide
 * Run PowerToys as user:
   - [ ] Verify `Win + Shift + /` opens the guide
   - [ ] Change the hotkey to a different shortcut (e.g. `Win + /`) and verify it works
   * Restore the `Win + Shift + /` hotkey.
   - [ ] Open the guide and close it pressing `Esc`
   - [ ] Open the guide and close it pressing and releasing the `Win` key
 * With PowerToys running as a user, open an elevated app and keep it on foreground:
   - [ ] Verify `Win + Shift + /` opens the guide
   - [ ] Verify some of the shortcuts shown in the guide work and the guide is closed when pressed

### OOBE
 * Quit PowerToys
 * Delete %localappdata%\Microsoft\PowerToys
 - [ ] Start PowerToys and verify OOBE opens
 * Visit each OOBE section and for each section:
   - [ ] open the Settings for that module
   - [ ] verify the Settings work as expected (toggle some controls on/off etc.)
   - [ ] close the Settings
   - [ ] if it's available, test the `Launch module name` button
 * Close OOBE
 - [ ] Open the Settings and from the General page open OOBE using the `Welcome to PowerToys` link

## PowerRename
- [ ] Check if disable and enable of the module works.
- [ ] Check that with the `Show icon on context menu` icon is shown and vice versa.
- [ ] Check if `Appear only in extended context menu` works.
- [ ] Enable/disable autocomplete.
- [ ] Enable/disable `Show values from last use`.
* Select several files and folders and check PowerRename options:
    - [ ] Make Uppercase/Lowercase/Titlecase (could be selected only one at the time)
    - [ ] Exclude Folders/Files/Subfolder Items (could be selected several)
    - [ ] Item Name/Extension Only (one at the time)
    - [ ] Enumerate Items
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

## Image Resizer
- [ ] Disable the Image Resizer and check that `Resize images` is absent in the context menu
- [ ] Enable the Image Resizer and check that `Resize images` is present in the context menu
- [ ] Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify that changes are populated
- [ ] Resize one image
- [ ] Resize multiple images

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

