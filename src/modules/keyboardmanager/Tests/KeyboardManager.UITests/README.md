# Keyboard Manager UI regression tests

This directory contains the **Windows-only, interactive** end-to-end tests for the
Keyboard Manager editor and native remapping engine. The test assembly is
`KeyboardManager.UITests` and uses the shared
`src/common/UITestAutomation.Next` helpers.

The source of truth for machine setup and UI test infrastructure is
[`doc/devdocs/development/ui-tests.md`](../../../../../doc/devdocs/development/ui-tests.md).
The repository-wide build and test rules are documented in
[`AGENTS.md`](../../../../../AGENTS.md).

## Scope

Tests cover the product behavior a user sees in PowerToys Settings and the
Keyboard Manager editor, plus the native profile written by an accepted
operation. These are different from the pure managed helper tests located in
`src/modules/keyboardmanager/KeyboardManagerEditorUI.UnitTests`.

| Test class | Main responsibility |
| --- | --- |
| `KeyboardManagerEditorTests` | Add/edit/remove, persistence, dropdowns, recording, special actions |
| `KeyboardManagerRemappingTests` | Native key and shortcut injection, press/release state, app scope |
| `KeyboardManagerSearchFilterTests` | Search, filter combinations, and bulk deletion |
| `KeyboardManagerEditorCancellationTests` | Cancel single delete, bulk delete, new/edit dialog, selection |
| `KeyboardManagerDialogValidationTests` | Save-button state for incomplete trigger and active action |
| `KeyboardManagerCrossCategorySelectionTests` | Mixed URL/program/text selection and deletion |

The tests deliberately use F13-F17 as source keys where possible. Unlike
ordinary letters, they rarely interfere with UI text entry during the test.

## Running on a Windows development machine

1. Follow the root developer setup instructions and initialize submodules.
2. Install the pinned `winappcli` runtime using the repository's
   `.pipelines/InstallWinAppCli.ps1` helper, or configure `WINAPP_CLI_PATH`
   as described in the UI testing documentation. This suite uses
   UITestAutomation.Next, not WinAppDriver or Selenium.
3. Build the PowerToys runtime and both affected test projects with the
   repository build scripts. Check the exit code of each build before
   proceeding to testing.
4. Use a dedicated Windows VM with an interactive standard-user desktop.
   Exit any existing PowerToys instance; the test harness owns the runner
   and module lifecycle.
5. Run the built UI-test executable directly with Microsoft.Testing.Platform.
   Use explicit filter properties (not a bare test display name).
6. Review the TRX results and failure artifacts. The full local validation
   matrix is documented in the repository's UI testing guide.

From the repository root, build the focused UI-test project after the runtime
and prerequisites have been built:

```powershell
tools\build\build.cmd -Path src\modules\keyboardmanager\Tests\KeyboardManager.UITests -Platform x64 -Configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Keyboard Manager UI test build failed.' }
```

On the interactive test desktop, with the built runtime and test payload staged,
run the suite executable (adjust the path to the staged payload as needed):

```powershell
$exe = 'x64\Debug\tests\KeyboardManager.UITests\net10.0-windows10.0.26100.0\KeyboardManager.UITests.exe'
& $exe --filter 'TestCategory=Keyboard Manager' --report-trx --report-trx-filename keyboard-manager.trx --results-directory .\TestResults\KeyboardManager
if ($LASTEXITCODE -ne 0) { throw 'Keyboard Manager UI tests failed.' }
```

For a focused regression run, use a filter such as
`Name=KeyboardManager.Editor.TextOutputValidation`. Confirm the TRX contains
executed tests; a zero-test result is not evidence of success.

Do not run these tests on a machine you rely on for keyboard accessibility
remaps or custom productivity shortcuts. They replace the active Keyboard
Manager profile with a deterministic test profile while running and send
synthetic input to Windows.

### Why a build is required

UI tests compile against Windows automation APIs, the PowerToys modules, and
the native keyboard mapping engine. Successful source review on another
platform is **not** evidence of a passing Windows build or end-to-end test.

The suite must not be described as passing until a real Windows build and test
execution has been completed.

## Isolation model

The shared `KeyboardManagerTestBase` and supporting scope utilities are
responsible for keeping these tests from permanently changing user settings:

- The scope snapshots managed Keyboard Manager configuration files.
- Tests apply a known baseline before interacting with the editor.
- `KeyboardInputFixtureLock` prevents tests that send physical keyboard
  input from racing each other.
- Tests are marked `DoNotParallelize`.
- Cleanup closes editor processes, resets mappings, and releases keys.
- Class-level teardown restores original settings.

If editor shutdown fails, the scope intentionally does **not** overwrite
settings over a still-running editor. Investigate the failure and restore
settings only after the relevant process has exited.

### Testing cancellation

A cancelled action should not modify the native profile:

- Escape closes new and edit dialogs without saving.
- Escape closes single- and multi-row delete confirmations.
- Exiting selection mode clears selected items and counters.
- Reopening the editor should show the same mappings after cancellation.
- A cancelled operation must not leave the next action disabled or prefilled
  with stale selection state.

A test should compare the persisted profile and the visible UI, not merely
assert that the dialog disappeared. When testing edit cancellation, change a
valid action before cancelling and verify that the original mapping, rather
than the replacement, remains after reopening.

### Testing validation

The same shortcut dialog supports several action types, and each action has
different required fields. The Save button must track the currently selected
action instead of stale values from a previous type.

| Action | Required information |
| --- | --- |
| Key/shortcut | Trigger and target |
| Open URL | Shortcut trigger with modifier and nonblank URL |
| Open app | Shortcut trigger with modifier and nonblank executable path |
| Insert text | Trigger and nonempty text |
| Disable | Trigger |

Important cases include: no trigger, an incomplete URL/program trigger,
empty text, whitespace-only program/URL fields, and correcting invalid input
without closing the dialog.

Inserting literal whitespace is a valid text action; this differs from an
empty string. The dialog's inline warning and Save enablement should agree
with the underlying `ValidationHelper` rules. Exercise the warning while
the trigger is incomplete so that complete-input validation cannot mask it.

### Testing cross-category selection

The unified editor presents different lists for ordinary mappings, disabled
keys, text mappings, programs, and URLs. Bulk selection must add up across
those sections. On confirmation:

- Only selected mappings should be removed.
- Other action types should retain their native profile entries.
- The selected counter should reset after deletion.
- Reopening the editor should not resurrect removed rows.

On cancellation, no category should change.

## Diagnostics and triage

When a test fails, preserve enough context to reproduce the issue:

1. Record which test method and scenario failed.
2. Inspect the exception, timeout, and the test's `Step(...)` messages.
3. Check whether the expected automation IDs exist in the current XAML.
4. Compare the editor's visible row count to the native profile JSON.
5. Verify keyboard focus and whether an earlier remap swallowed input.
6. Re-run the failed test in isolation before claiming an intermittent bug.

Relevant XAML and service paths:

- `KeyboardManagerEditorUI/Pages/MainPage.xaml` — list, search, selection,
  action dialogs, confirmations.
- `KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml` — trigger,
  action type, input validation and app-specific options.
- `KeyboardManagerEditorUI/Helpers/ValidationHelper.cs` — duplicate, illegal,
  incomplete and conflicting remapping checks.
- `KeyboardManagerEditorUI/Settings/SettingsManager.cs` — editor metadata.
- `KeyboardManagerSettings.cs` — UI test profile setup and JSON snapshots.

## Maintaining test stability

Prefer stable `AutomationProperties.AutomationId` values to visible text
when both are available. Use visible text only for content that is part of
the user-facing behavior. Do not add arbitrary long sleeps in lieu of waiting
for a state condition.

When extending tests:

- Keep each scenario focused on an observable user outcome.
- Use short, bounded polling to wait for UI and persistence to settle.
- Re-query elements after state transitions; `Element.Name` is cached at
  lookup time.
- Reuse the selection-mode and key recording helpers in
  `KeyboardManagerTestBase`.
- Assert both the intended change and preservation of unrelated mappings.
- Release injected keys in a `finally` block, even on failure.
- Use the existing scope instead of writing directly to a user's normal
  profile without backups.
- Keep native input and editor tests nonparallel.
- Ensure every new test has deterministic cleanup.

## Pull requests

Follow `CONTRIBUTING.md`: discuss the issue and approach with maintainers.
The contributor, not an AI agent, must write the visible PR summary,
description and validation steps in their own words. Test results must state
exactly which Windows build and scenarios were actually executed.
