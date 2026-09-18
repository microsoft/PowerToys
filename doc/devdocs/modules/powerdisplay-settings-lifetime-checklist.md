# Power Display Settings lifetime checklist

Use an isolated settings fixture and the same ordinary build configuration for
before/after runs. Do not enable monitor-control features merely to exercise
navigation. Record the exact source revision, process IDs, navigation count,
settling interval, and memory metric for each run.

## Automated coverage

Build `src\settings-ui\Settings.UI.UnitTests` using `tools\build\build.cmd`, then
run `Settings.UI.UnitTests.dll` with `vstest.console.exe` and the filter:

```text
FullyQualifiedName~NativeEventWaiterTests|FullyQualifiedName~ViewModelTests.PowerDisplay
```

The focused cases cover cancellation while waiting, repeated delivery,
disposal after dispatch is queued, current-generation delivery, callback and
worker-context disposal, concurrent disposal, named-handle closure, callback
target collection, viewmodel subscription cleanup, and canceled profile work.
The original process-lifetime Settings termination listener remains unchanged.

## Page lifecycle and interaction

- [ ] Navigate Home -> Power Display -> Home repeatedly with the module disabled.
      The page remains functional on every visit; old viewmodels and refresh
      event handles do not accumulate after the diagnostic collection step.
- [ ] With an already enabled module and a safe fixture, repeat the navigation.
      Monitor refresh notifications update only the currently active page.
- [ ] Unload and reload the **same page instance** in a UI harness. Verify a new
      viewmodel, matching `DataContext`, working one-time/one-way/two-way compiled
      bindings, and fresh profiles. Verify a setting edit is persisted once.
- [ ] Keep references to the previous monitor collection and items in the
      harness. Changes after page unload must not save settings or send IPC.
- [ ] Navigate away during a pending profile load/reorder/edit/delete and return.
      Completion must not update the old page, report a spurious read error,
      restore old focus, or overwrite the new page's state.
      An atomic write already in progress must still notify the module if it
      commits; a committed deletion must also clear LightSwitch profile
      references. A write canceled before it starts must not commit or notify.
- [ ] Navigate away with a confirmation/profile/custom-mapping dialog pending.
      The dialog closes and cannot commit into a replacement viewmodel. Existing
      enable/deny confirmation behavior remains unchanged on an active page.
- [ ] For each Add/Edit/Delete profile and Add/Edit/Delete custom-mapping dialog,
      queue the caller's continuation after the dialog helper has returned an
      accepted result. Unload the page before running that continuation, both
      without reloading and after reloading the same page with a new viewmodel.
      The old result must neither throw nor mutate either viewmodel, persist
      settings, or send IPC. An unchanged, loaded generation must still commit.
- [ ] After reload, verify profile drag reordering, More-menu moves, and
      Alt+Shift+arrow moves. A successful move restores focus to its More button;
      newer keyboard/pointer input or navigation cancels that focus restoration.
- [ ] Close and reopen Settings; normal Runner-triggered Settings termination
      still works.

## Memory evidence

Measure normal, uninstrumented Settings runs separately from forced-GC/root
diagnostics. Match the baseline and candidate navigation sequence and settling
times, and report private resident memory separately from private bytes and
managed heap size. Attribute only confirmed retained roots to this change;
neither total process thread count nor aggregate memory growth alone establishes
the number or cost of native event listeners. Do not claim a saving percentage
until a matched before/after run has completed.
