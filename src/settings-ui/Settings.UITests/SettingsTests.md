## [General Settings](tests-checklist-template-settings-section.md)

**Admin mode:**
 - [] restart PT and verify it runs as user
 - [] restart as admin and set "Always run as admin"
 - [] restart PT and verify it  runs as admin
 * if it's not on, turn on "Run at startup"
 - [] reboot the machine and verify PT runs as admin (it should not prompt the UAC dialog)
 * turn Always run as admin" off
 - [] reboot the machine and verify it now runs as user

**Modules on/off:**
 - [x] turn off all the modules and verify all module are off
 - [] restart PT and verify that all module are still off in the settings page and they are actually inactive
 - [x] turn on all the module, all module are now working
 - [] restart PT and verify that all module are still on in the settings page and they are actually working

**Checkbox rendering after enabling a module ([#50623](https://github.com/microsoft/PowerToys/issues/50623)):**

 - [ ] On FancyZones, Always On Top, Advanced Paste, and PowerDisplay, leave at least one checkbox unchecked and one checked. Disable and re-enable the module repeatedly. Unchecked boxes must stay empty; checked boxes must retain their checkmarks and values. Include FancyZones' descriptive "Allow zones to span across monitors" checkbox.
 - [ ] Hover over an unchecked box and move away, then check and uncheck using both the mouse and Space key. Verify the native check/uncheck animations, focus indicator, and input still work before and after the module cycle.
 - [ ] Disable during interaction, rapidly repeat the module cycle, and open a page whose module is initially disabled. Verify there is no persistent glyph or unexpected setting change.
 - [ ] In Quick Accent, verify the language select-all checkbox has distinct checked, unchecked, and mixed appearances, and selection remains correct after disabling and re-enabling the module.
 - [ ] Repeat in light, dark, system-selected, and high-contrast themes, including theme changes while enabled and disabled. Borders, labels, and checked/mixed glyphs must remain readable.
 - [ ] Navigate away and back, reopen Settings, and repeat with system animations disabled and representative display scaling. The result must not depend on refreshing the page.
 - [ ] Record the tested revision/configuration and capture a short recording of the module cycle and the enabled check/uncheck animation. Inspect rendered glyphs; checking the logical checkbox value alone does not detect this defect. Restore any settings changed during testing.

**Quick access tray icon flyout:**
 - [] Use left click on the system tray icon and verify the flyout appears.
 - [] Try to launch a module from the launch screen in the flyout.
 - [] Try disabling a module in the all apps screen in the flyout, make it a module that's launchable from the launch screen. Verify that the module is disabled and that it also disappeared from the launch screen in the flyout.
 - [] Open the main settings screen on a module page. Verify that when you disable/enable the module on the flyout, that the Settings page is updated too.

**Settings backup/restore:**
 - [] In the General tab, create a backup of the settings.
 - [] Change some settings in some PowerToys.
 - [] Restore the settings in the General tab and verify the Settings you've applied were reset.

## OOBE
 * Quit PowerToys
 * Delete %localappdata%\Microsoft\PowerToys
 - [] Start PowerToys and verify OOBE opens
 * Change version saved on `%localappdata%\Microsoft\PowerToys\last_version.txt`
 - [] Start PowerToys and verify OOBE opens in the "What's New" page
 * Visit each OOBE section and for each section:
   - [] open the Settings for that module
   - [] verify the Settings work as expected (toggle some controls on/off etc.)
   - [] close the Settings
   - [] if it's available, test the `Launch module name` button
 * Close OOBE
 - [x] Open the Settings and from the General page open OOBE using the `Welcome to PowerToys` link
