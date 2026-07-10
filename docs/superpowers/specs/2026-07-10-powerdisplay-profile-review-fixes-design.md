# PowerDisplay Profile Review Fixes Design

## Summary

This change fixes three issues found in PR #49175:

1. A stale LightSwitch profile reference is not cleared when the last profile is deleted.
2. PowerDisplay writes the complete LightSwitch settings file from a second process, which can overwrite concurrent Settings UI changes and cannot report save failure.
3. Profile-store mutex waits and durable file writes run synchronously on UI threads.

The implementation keeps the existing cross-process profile transaction semantics, moves blocking profile operations to worker threads, makes Settings UI the sole writer for LightSwitch profile-reference migration, and shows an explicit loading state at every profile UI entry point.

## Goals

- Clear stale LightSwitch references after any successful profile load, including an empty collection.
- Never persist LightSwitch settings directly from the PowerDisplay process.
- Preserve name-based LightSwitch resolution until Settings UI completes the id migration.
- Keep the named mutex around each complete profile transaction.
- Ensure UI threads do not wait for the profile mutex or execute profile file I/O.
- Show a localized loading state in the PowerDisplay flyout, PowerDisplay Settings page, and LightSwitch Settings page.
- Preserve current failure behavior: log the error, finish loading, and leave the profile UI empty without rewriting LightSwitch references.

## Non-goals

- Changing the profile id schema or allocation algorithm.
- Replacing the named mutex or atomic file-replacement strategy.
- Adding retry UI or a user-facing profile-load error.
- Adding reverse IPC from PowerDisplay to the runner.
- Addressing the remaining low-priority review suggestions outside these three issues.

## Architecture

### Asynchronous profile transactions

`ProfileStore` remains the owner of synchronous mutex acquisition and file transactions. It gains internal asynchronous entry points that schedule the complete synchronous operation on a worker thread. The worker thread both acquires and releases the named mutex, preserving the mutex thread-affinity requirement.

`ProfileHelper` and `ProfileService` expose asynchronous counterparts for UI-originated operations:

- Load profiles.
- Load profiles and ensure ids.
- Add or update a profile.
- Remove a profile by id.
- Run a conditional profile update.

Cancellation can prevent queued work from starting. Once a transaction starts, it runs to completion so cancellation cannot leave an in-memory mutation partially persisted.

Synchronous APIs remain available for tests and background-only callers. All UI-originated call sites move to the asynchronous APIs, including initial loads, settings refreshes, profile CRUD, profile lookup before apply, and legacy monitor-id profile migration.

### LightSwitch settings ownership

The PowerDisplay process stops reading, mutating, and saving `LightSwitchSettings`. It only ensures that `profiles.json` contains stable ids.

Settings UI becomes the sole initiator of LightSwitch profile-reference persistence:

- `PowerDisplayViewModel` loads and reconciles LightSwitch references before enabling profile edit or rename actions.
- `LightSwitchViewModel` reconciles its current module settings after profiles load.
- Both paths serialize the same settings envelope and send it through the existing runner IPC callback.
- No PowerDisplay app code calls `SettingsUtils.SaveSettings` for LightSwitch.

The PowerDisplay runtime keeps name fallback in `LightSwitchProfileResolver.Resolve`. Theme changes therefore continue to work before either Settings page has migrated legacy name-only references.

### Reference reconciliation

`LightSwitchProfileResolver` gains one pure reconciliation operation for each stored `(id, name)` pair:

- A valid positive id remains selected.
- A missing positive id is stale and clears both id and name.
- An unset id with a resolvable legacy name stores the matching id and canonical name.
- An unset id with an unresolved real name clears both fields.
- Empty and `(None)` references remain empty.

The reconciliation method returns whether settings changed. Callers persist only when it returns `true`.

Reconciliation runs only after a successful profile load. A successful empty collection is valid input and clears stale positive ids. A load exception skips reconciliation entirely, so transient storage failure cannot erase a valid reference.

`LightSwitchViewModel.SelectByStoredReference` becomes selection-only. It no longer owns stale-reference persistence or uses profile count as a proxy for load success.

## UI State

Each profile-owning ViewModel exposes `IsProfilesLoading` and serializes overlapping load requests so an older request cannot replace newer results.

### PowerDisplay flyout

- The profile button remains available while profiles are loading when the profile switcher setting is enabled.
- The profile flyout shows a `ProgressRing` and localized loading text while `IsProfilesLoading` is true.
- The profile list replaces the loading state after completion.

### PowerDisplay Settings page

- The profiles group shows a loading card while profiles are loading.
- Apply, create, edit, and delete actions are unavailable during loading.
- CRUD methods are asynchronous and keep the loading state active through persistence and list refresh.

### LightSwitch Settings page

- The two profile selectors are replaced by one loading card while profiles are loading.
- Selection persistence is suppressed while the collection is being refreshed, preventing TwoWay binding from clearing settings when items are temporarily removed.
- After a successful load, reconciliation runs first and the selected profile objects are then synchronized from the reconciled settings.

The PowerDisplay app and Settings UI each add a localized "Loading profiles" resource. The loading indicator exposes an accessible name through the localized resource.

## Data Flows

### PowerDisplay startup

1. Construct `MainViewModel` with an empty profile collection and `IsProfilesLoading = true`.
2. Start monitor discovery and profile loading independently.
3. Run `LoadProfilesEnsuringIdsAsync` on a worker thread.
4. Populate the observable profile collection on the UI thread.
5. Set `IsProfilesLoading = false` in `finally`.
6. Do not read or save LightSwitch settings from the PowerDisplay process.

### PowerDisplay Settings page startup

1. Set `IsProfilesLoading = true`.
2. Load and ensure profile ids on a worker thread.
3. Read the latest LightSwitch settings and reconcile references.
4. If reconciliation changed settings, send one runner IPC settings message.
5. Populate the profile collection.
6. Set `IsProfilesLoading = false` and enable profile actions.

This sequence completes before the user can rename a profile, preserving a legacy name-only LightSwitch reference.

### LightSwitch Settings page startup

1. Set `IsProfilesLoading = true` and suppress selection persistence.
2. Load and ensure profile ids on a worker thread.
3. Reconcile the current LightSwitch settings against the loaded collection.
4. Send one runner IPC settings message if reconciliation changed settings.
5. Replace the available profiles and selected profile objects.
6. Re-enable selection persistence and set `IsProfilesLoading = false`.

### Profile CRUD

1. Enter the loading state and disable profile actions.
2. Execute the complete add/update/remove transaction on a worker thread.
3. Reload the committed collection asynchronously.
4. Replace the UI collection once.
5. Signal PowerDisplay after successful persistence.
6. On failure, log, clear the UI collection, and leave LightSwitch settings unchanged.

## Error Handling and Concurrency

- Profile load, parse, mutex timeout, and save failures continue to propagate from `ProfileStore`.
- ViewModels catch and log failures at their existing boundaries.
- `IsProfilesLoading` is reset in `finally`.
- Failed loads clear only the UI collection; they do not run reference reconciliation.
- A successful empty load runs reconciliation and clears stale references.
- One in-flight profile operation per ViewModel prevents out-of-order UI replacement.
- Runner IPC remains the only path used by this feature to persist migrated LightSwitch settings.
- Fire-and-forget event entry points call helpers that catch and log internally so task exceptions are observed.

## Testing

Tests are written before implementation.

### Resolver tests

- A stale positive id is cleared when the successfully loaded profile collection is empty.
- A stale positive id is cleared when other profiles exist.
- A legacy name resolves to an id and canonical name.
- Empty references remain unchanged.
- Reconciliation reports `false` when no persistence is needed.

### Profile store async tests

- An async load returns an incomplete `Task` while another store owns the named mutex, allowing the caller thread to continue.
- The async operation completes after the mutex is released.
- Concurrent async add/update operations preserve both profiles and unique ids.
- Existing corrupt-read, failed-save, rollback, and atomic replacement tests remain green.

### Settings migration tests

- Changed references produce exactly one runner IPC settings message.
- Unchanged references produce no message.
- An empty successful profile collection clears a stale reference and sends one message.
- A profile load exception does not reconcile or send settings.
- PowerDisplay startup no longer writes LightSwitch settings.

### ViewModel state tests

- `IsProfilesLoading` transitions through start, success, and failure.
- Failed loads leave profile collections empty.
- LightSwitch selection persistence is suppressed during collection replacement.
- A successful empty load clears stale LightSwitch settings.

If constructing full WinUI ViewModels makes a test depend on a UI runtime, the loading and migration coordination is extracted into small injectable helpers and tested without adding a new UI test framework.

### Validation

- Build `PowerDisplay.Lib.UnitTests`, PowerDisplay, `Settings.UI.Library`, and Settings UI for x64 Debug.
- Run the complete affected test assemblies with `vstest.console.exe`.
- Confirm XAML compilation for all three loading surfaces.

## Acceptance Criteria

- Deleting the final referenced profile clears its LightSwitch id and name after the next successful profile load.
- A profile load failure never clears LightSwitch references.
- PowerDisplay contains no direct LightSwitch settings save.
- Migrated LightSwitch references are persisted through runner IPC only.
- The three profile UI entry points display a localized loading state.
- No profile mutex wait or profile file transaction runs on a UI thread.
- Existing profile atomicity, duplicate-name, rename, and id migration behavior remains unchanged.
