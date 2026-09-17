# Settings page ownership regression checklist

Dashboard and General own their view models for one loaded page lifetime. Unload
disposes the model; reloading the same page creates a replacement, updates
`DataContext`, and refreshes compiled bindings. This is not page caching.

The shared Quick Access model owns its General and Keyboard Manager repository
subscriptions and its exact enabled-module callback. The callback API remains a
single replaceable callback, not a multicast event. Teardown must not clear or
restore another owner's callback, and an older queued snapshot refresh must not
take ownership from a newer model.

## Focused unit coverage

Build `src\settings-ui\Settings.UI.UnitTests` with `tools\build\build.ps1` before
running `vstest.console.exe` against `Settings.UI.UnitTests.dll`. Include these
test classes in one filtered invocation:

- `QuickAccessLifetimeTests`
- `GeneralLifetimeTests`
- `PageViewModelBaseLifetimeTests`
- `DashboardShortcutProjectionTests` (the stable shortcut projection baseline)
- `General` (existing General settings behavior)

Coverage includes exact subscriber removal, old/current snapshot ownership,
queued and in-flight notification suppression, reentrant disposal, collectability
with repositories kept alive, page-load cancellation, model replacement, unchanged
shortcut conflict metadata, and no-op collection stability.

## UI integration checklist

Use a disposable test profile/build, not another user's active Runner or settings.
These checks require the actual XAML/window integration, beyond the unit fixtures.

- Navigate Home -> General -> Home repeatedly. Each active page remains responsive;
  returning Home shows current quick-access visibility and module shortcuts.
- Unload and reload the **same** Dashboard page instance. Its model and
  `QuickAccessItems` collection are replaced; DataContext-bound and compiled-bound
  controls both follow the replacement, including sort selection and launch buttons.
- Unload and reload the **same** General page instance. Shortcut and enable toggles,
  update state, and backup status bind to the replacement. Bug-report notifications
  are registered once for the new load and still update its status.
- Navigate away with a delayed backup-status refresh, message-hide delay, or
  bug-report response queued; reload before it executes. Old work cannot update the
  replacement model or persist stale settings. A backup dry run already in progress
  may finish, but its canceled page receives no result.
- Change General settings externally while General is open. The shortcut follows
  the new snapshot, unchanged shortcuts retain conflict warnings, and the old
  shortcut no longer retains or notifies the model.
- Hide the standalone Quick Access window, change enabled-module or Keyboard
  Manager editor settings, then show it. The existing model remains subscribed and
  reflects the changes. Only actual window close/disposal releases those subscriptions.

## Measurement boundaries

Compare fresh, ordinary builds with identical configuration, profile, navigation,
settle times, and process-family accounting. Keep forced-GC diagnostics separate
from uninstrumented working-set/private-resident measurements. Collectability proves
managed ownership cleanup; it does not establish that all native/XAML growth is
recovered or attribute a combined fix's memory delta to one component.
