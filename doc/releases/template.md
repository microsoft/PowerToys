# Release Checklist - v0.XX

## Prep

- [ ] Readme Update created
- [ ] Release template updated with any new features / updates for testing

## Testing 

Based on [wiki](https://github.com/microsoft/PowerToys/wiki/Release-check-list)

### Install tests
- [ ] install a **previous version** on a clean machine (a clean machine doesn't the `%localappdata%\Microsoft\PowerToys` folder)
- [ ] create a few FZ custom layouts:
    - [ ] one from scratch with several zones
    - [ ] one from columns template adding a splitter to a column
    - [ ] one from grid template using 4 zones and splitting one zone
    - [ ] use unicode chars in some of the layout names
    - [ ] apply the custom layouts to all monitors and at least one virtual desktop
- [ ] use PowerRename with history ON
- [ ] install the new version (it will uninstall the old version and install the new version)
- [ ] verify the settings are preserved and FZ configuration is still the same
- [ ] verify the PowerRename history has been preserved

### Functional tests

#### General Settings

- [ ] Admin mode:
   1. restart as admin and verify FZ can snap an elevated window
   1.  restart PT and verify it now runs as user
   1.  restart as admin and set "Always run as admin"
   1.  restart PT and verify it still runs as admin
   1. if it's not on, turn on "Run at startup"
   1. reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
   1. turn Always run as admin" off
   1. reboot the machine and verify it now runs as user
- [ ] Modules on/off:
   1. turn off all the modules and verify all module are off
   1. restart PT and verify that all module are still off in the settings page and they are actually inactive
   1. turn on all the module, all module are now working
   1. restart PT and verify that all module are still on in the settings page and they are actually working
- [ ] Elevated app notification:
   1. run PT as a user
   1. open an elevated app (i.e. Task Manager)
   1. shift-drag the elevated app window
   1.verify that a notification appears
   1.restart PT as admin
   1.shift-drag the elevated app window
   1. verify the notification doesn't appear

**PowerToys Run**

 - Enable PT Run in settings and ensure that the hotkey brings up PT Run 
   - when PowerToys is running unelevated on start-up
   - when PowerToys is running as admin on start-up
   - when PowerToys is restarted as admin, by clicking the restart as admin button in settings.
 - Check that each of the plugins is working:
   - Program - launch a Win32 application and a packaged application
   - Calculator - ensure a mathematical input returns a correct response and is copied on enter.
   - Indexer - open a file on the disk.
   - Shell - execute a command. Enter the action keyword `>`, followed by the query, both with and without space.
   - Folder - Search and open a sub-folder on entering the path.
   - Uri - launch a web page on entering the uri.
   - Window walker - Switch focus to a running window.
 - Validate that the context menu items work as expected for each of the plugins
   - Run as admin
   - Copy file path
   - Open in console
   - Open containing folder
 - Disable PT Run and ensure that the hotkey doesn't bring up PT Run.

**Keyboard Manager**

UI Validation:

  - In Remap keys, add and remove rows to validate those buttons. While the blank rows are present, pressing the OK button should result in a warning dialog that some mappings are invalid.
  - Using only the Type buttons, for both the remap windows, try adding keys/shortcuts in all the columns. The right-side column in both windows should accept both keys and shortcuts, while the left-side column will accept only keys or only shortcuts for Remap keys and Remap shortcuts respectively. Validate that the Hold Enter and Esc accessibility features work as expected.
  - Using the drop downs try to add key to key, key to shortcut, shortcut to key and shortcut to shortcut remappings and ensure that you are able to select remappings both by using mouse and by keyboard navigation.
  - Validate that remappings can be saved by pressing the OK button and re-opening the windows loads existing remappings.

Remapping Validation:

For all the remappings below, try pressing and releasing the remapped key/shortcut and pressing and holding it. Try different behaviors like releasing the modifier key before the action key and vice versa.
  - Test key to key remappings
    - A->B
    - Ctrl->A
    - A->Ctrl
    - Win->B (make sure Start menu doesn't appear accidentally)
    - B->Win (make sure Start menu doesn't appear accidentally)
    - A->Disable
    - Win->Disable
  - Test key to shortcut remappings
    - A->Ctrl+V
    - B->Win+A
  - Test shortcut to shortcut remappings
    - Ctrl+A->Ctrl+V
    - Win+A->Ctrl+V
    - Ctrl+V->Win+A
    - Win+A->Win+F
  - Test shortcut to key remappings
    - Ctrl+A->B
    - Ctrl+A->Win
    - Win+A->B
  - Test app-specific remaps
    - Similar remaps to above with Edge, VSCode (entered as code) and cmd. For cmd try admin and non-admin (requires PT to run as admin)
    - Try some cases where focus is lost due to the shortcut. Example remapping to Alt+Tab or Alt+F4
  - Test switching between remappings while holding down modifiers - Eg. Ctrl+D->Ctrl+A and Ctrl+E->Ctrl+V, hold Ctrl and press D followed by E. Should select all and paste over it in a text editor. Similar steps for Windows key shortcuts.

## Staging release

- [ ] Release template updated with any new features / updates for testing
- [ ] Create Release and base off Readme Update PR
- [ ] Upload exe
- [ ] Upload symbols
- [ ] Create YAML for (winget-pkgs)[https://github.com/microsoft/winget-pkgs]

## Releasing
- [ ] Push live
- [ ] Merge Readme PR live
- [ ] Merge Docs.MSFT live
- [ ] Submit PR to (winget-pkgs)[https://github.com/microsoft/winget-pkgs]
