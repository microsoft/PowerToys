## This is for tracking UI-Tests migration progress for Hosts File Editor Module
Refer to [release check list] (https://github.com/microsoft/PowerToys/blob/releaseChecklist/doc/releases/tests-checklist-template.md#hosts-file-editor) for all manual tests.

### Existing Manual Test-cases run by previous PowerToys owner
For existing manual test-cases, we will convert them to UI-Tests and run them in CI and Release pipeline

 * Launch Host File Editor:
   - [x] Verify the application exits if "Quit" is clicked on the initial warning. (**HostsSettingTests.TestWarningDialog**)
   - [x] Launch Host File Editor again and click "Accept". The module should not close. (**HostModuleTests.TestEmptyView**)
   - [ ] Launch Host File Editor again and click "Accept". The module should not close. Open the hosts file (`%WinDir%\System32\Drivers\Etc`) in a text editor that auto-refreshes so you can see the changes applied by the editor in real time. (VSCode is an editor like this, for example)
   - [ ] Enable and disable lines and verify they are applied to the file.
   - [ ] Add a new entry and verify it's applied.
   - [ ] Add manually an entry with more than 9 hosts in hosts file (Windows limitation) and verify it is split correctly on loading and the info bar is shown.
   - [x] Try to filter for lines and verify you can find them. (**HostModuleTests.TestFilterControl**)
   - [ ] Click the "Open hosts file" button and verify it opens in your default editor. (likely Notepad)
 * Test the different settings and verify they are applied:
   - [ ] Launch as Administrator.
   - [x] Show a warning at startup. (**HostsSettingTests.TestWarningDialog**)
   - [ ] Additional lines position.

### Additional UI-Tests cases
  - [x] Add manually an entry with more than 9 hosts and Add button should be disabled. (**HostModuleTests.TestTooManyHosts**)
  - [x] Add manually an entry with less or equal 9 hosts and Add button should be enabled. (**HostModuleTests.TestTooManyHosts**)
  - [x] Should show empty view if no entries. (**HostModuleTests.TestEmptyView**)
  - [x] Add a new entry with valid or invalid input (**HostModuleTests.TestAddHost**)
  - [x] Show save host file error if not run as Administrator. (**HostModuleTests.TestErrorMessageWithNonAdminPermission**)