[Go back](tests-checklist-template.md)

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
 - [ ] verify the settings are preserved and FancyZones configuration is still the same
 - [ ] test installing as SYSTEM (LocalSystem account)
   * Download PsTools from https://learn.microsoft.com/en-us/sysinternals/downloads/psexec
   * Run PowerToys installer with psexec tool `psexec.exe -sid <path_to_installer_exe`
   * Brief check if all modules are working

 * PER-USER and PER-MACHINE TESTS:
   * Install **previous version** on a clean machine and update with new per-machine version. Ensure that it is installed in Program files and that registry entries are under **HKLM**/Software/Classes/PowerToys. Go trhough different modules and ensure that they are working correctly.
   * Try installing per-user version over already installed per-machine version and ensure that proper error message is shown.
   * Remove PowerToys and install per-user version. Ensure that it is installed in <APPDATA>/Local/PowerToys and that registry entries are under **HKCU**/Software/Classes/PowerToys. Go trhough different modules and ensure that they are working correctly.
   * Create a new user and install per-user version there as well. Go trhough different modules and ensure that they are working correctly. Ensure that changing settings for one user does not change settings of other user.
