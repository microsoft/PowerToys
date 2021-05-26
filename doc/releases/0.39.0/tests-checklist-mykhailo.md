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
 - [X] verify the settings are preserved and FancyZones configuration is still the same

## General Settings

**Admin mode:**
 - [X] restart as admin and verify FZ can snap an elevated window
 - [X] restart PT and verify it now runs as user
 * restart as admin and set "Always run as admin"
 - [X] restart PT and verify it still runs as admin
 * if it's not on, turn on "Run at startup"
 - [X] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [X] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [X] turn off all the modules and verify all module are off
 - [X] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [X] turn on all the module, all module are now working
 - [X] restart PT and verify that all module are still on in the settings page and they are actually working

**Elevated app notification:**
 - run PT as a user
 - open an elevated app (i.e. Task Manager)
 - shift-drag the elevated app window
 - [X] verify that a notification appears
 - restart PT as admin
 - shift-drag the elevated app window
 - [X] verify the notification doesn't appear

## FancyZones Editor

- [X] Open editor from the settings
- [X] Open editor with a shortcut
- [X] Create a new layout (grid and canvas)
- [X] Duplicate a template and a custom layout
- [X] Delete layout
- [X] Edit templates (number of zones, spacing, distance to highlight adjacent zones). Verify after reopening the editor that saved settings are kept the same.
- [X] Edit canvas layout: zones size and position, create or delete zones. 
- [X] Edit grid layout: split, merge, resize zones.
- [X] Check `Save and apply` and `Cancel` buttons behavior after editing.
- [X] Assign a layout to each monitor.
- [X] Assign keys to quickly switch layouts (custom layouts only), `Win + Ctrl + Alt + number`.


## FancyZones
- [x] Switch between `Allow zones to span across monitors` on and off. Verify that layouts are applied correctly in both cases. 
- [x] Change zone colors and opacity.
- [x] Exclude some apps, verify that they're not applicable to a zone.
- [x] Launch PT in user mode, try to assign a window with administrator privileges to a zone. Verify the notification is shown.
- [x] Launch PT in administrator mode, assign a window with administrator privileges.
- [x] Create virtual desktop, verify that there are the same layouts as applied to the previous virtual desktop.
- [x] After creating a virtual desktop apply another layout or edit the applied one. Verify that the other virtual desktop layout wasn't changed.
- [x] Delete an applied custom layout in the Editor, verify that there is no layout applied instead of it.
* Switch between layouts with quick keys.
    - [x] Switch with `Win` + `Ctrl` + `Alt` + `key`
    - [x] Switch with just a key while dragging a window.
* Change screen resolution or scaling. 
    - [x] Assign grid layout, verify that the assigned layout fits the screen. 
      NOTE: canvas layout could not fit the screen if it was created on a monitor with a different resolution.
    - [x] Edit grid layout, verify that split, merge and resize zones works as expected.
- [x] Disable FZ
- [x] Reenable FZ, verify that everything is in the same state as it was before disabling.

## File Explorer Add-ons
 * Running as user:
   * go to PowerToys repo root
   - [X] verify the README.md Preview Pane shows the correct content
   * go to PowerToys repo and visit src\runner\svgs
   - [X] verify Preview Pane works for the SVG files
   - [X] verify the Icon Preview works for the SVG file (loop through different icon preview sizes)
 * Running as admin:
   * open the Settings and turn off the Preview Pane and Icon Previous toggles
   * go to PowerToys repo root
   - [X] verify the README.md Preview Pane doesn't show any content
   * go to PowerToys repo and visit src\runner\svgs
   - [X] verify Preview Pane doesn't show the preview for the SVG files
   * the Icon Preview for the existing SVG will still show since the icons are cached
   - [X] copy and paste one of the SVG file and verify the new file show the generic SVG icon

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
