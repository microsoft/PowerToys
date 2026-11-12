## Hosts File Editor UI-test migration progress

Refer to the [release checklist](https://github.com/microsoft/PowerToys/blob/releaseChecklist/doc/releases/tests-checklist-template.md#hosts-file-editor) for all manual tests.

### Existing Manual Test-cases run by previous PowerToys owner
For existing manual test-cases, we will convert them to UI-Tests and run them in CI and Release pipeline

- Launch Host File Editor:
  - [x] Verify the application exits if "Quit" is clicked on the initial warning. (**HostsSettingTests.TestWarningDialog**)
  - [x] Launch Host File Editor again and click "Accept". The module should not close. (**HostsSettingTests.TestWarningDialog**)
  - [x] Open the hosts file in an auto-refreshing editor and verify editor changes reach the file. (**HostModuleTests.TestEntryTogglesAreAppliedToHostsFile**)
  - [x] Enable and disable lines and verify they are applied to the file. (**HostModuleTests.TestEntryTogglesAreAppliedToHostsFile**)
  - [x] Add a new entry and verify it is applied. (**HostModuleTests.TestEntryTogglesAreAppliedToHostsFile**)
  - [x] Add an entry with more than 9 hosts directly to the hosts file and verify it is split on loading and the teaching tip is shown. (**HostModuleTests.TestTooManyHosts**)
  - [x] Try to filter for lines and verify you can find them. (**HostModuleTests.TestFilterControl**)
  - [x] Click the "Open hosts file" button and verify it opens the hosts document in Notepad. (**HostModuleTests.TestOpenHostsFileButtonOpensNotepad**)
- Test the different settings and verify they are applied:
  - [ ] Launch as Administrator. The automated test verifies the admin shared-event route and elevated-agent save behavior, but a real non-elevated launch through UAC remains manual. (**HostsSettingTests.TestOpenAsAdministrator**)
  - [x] Show a warning at startup. (**HostsSettingTests.TestWarningDialog**)
  - [x] Additional lines position. (**HostsSettingTests.TestAdditionalLinesPosition**)

### Additional UI-Tests cases
- [x] Add manually an entry with more than 9 hosts and Add button should be disabled. (**HostModuleTests.TestTooManyHosts**)
- [x] Add manually an entry with less or equal 9 hosts and Add button should be enabled. (**HostModuleTests.TestTooManyHosts**)
- [x] Should show empty view if no entries. (**HostModuleTests.TestEntryButtonsAndEmptyView**)
- [x] Add a new entry from both the empty-view link and toolbar button. (**HostModuleTests.TestEntryButtonsAndEmptyView**)
- [x] Show save host file error if not run as Administrator. (**HostModuleTests.TestErrorMessageWithNonAdminPermission**)