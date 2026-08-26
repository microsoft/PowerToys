# Explorer and Shell-extension UI tests

Use this reference for tests that open File Explorer, depend on Shell selection/focus, register a
preview or thumbnail handler, change Explorer's view, or restart the Explorer shell. The canonical
implementation is
[FileExplorerAddonsTests.cs](../../../../src/modules/previewpane/PreviewPane.UITests/FileExplorerAddonsTests.cs).

## Model three independent lifetimes

Do not treat "Explorer test" as one process lifecycle:

| Lifetime | Typical policy | Why |
|---|---|---|
| PowerToys runner + Settings | One launch per test class (`ReuseScopeAcrossTests`) | Keeps module registration alive and avoids repeated cold startup |
| Explorer shell/taskbar | Restart at most once after registration changes | Shell caches associations; repeated restarts are expensive and disruptive |
| Explorer file window | Fresh window per test | Isolates folder, selection, view, and temporary files |

```csharp
protected override bool ReuseScopeAcrossTests => true;

[TestInitialize]
public void PrepareTest() => CloseExplorerFileWindows();

[TestCleanup]
public async Task CleanupTest()
{
    await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
    CloseExplorerFileWindows();
}
```

The base rebinds a lightweight `Session` on every test but does not relaunch a healthy shared scope.
If that scope dies, the next test relaunches it. The inherited class cleanup stops what the class
launched after the final test.

## Restart Explorer without killing descendants

If a Shell restart is required, terminate only `explorer.exe`, not its process tree:

```csharp
foreach (var process in Process.GetProcessesByName("explorer"))
{
    process.Kill();
    process.WaitForExit(10_000);
}
```

`Kill(entireProcessTree: true)` also terminates processes launched from Explorer. In a validation VM
that included `msvsmon`; the apparent remote-debugger "auto stop" was caused by the test itself.
Guard the restart with a class-wide flag so it occurs once.

## Use the Shell view as the selection authority

A highlighted UIA row does not prove the selected path set or focused item that Shell extensions
consume. Establish and verify both through `ExplorerShell`:

```csharp
var selection = ExplorerShell.SetSelectionAndWaitForStable(
    new IntPtr(explorer.WindowHandle),
    new[] { filePath },
    focusedPath: filePath,
    timeoutMS: 30_000,
    requiredConsecutiveMatches: 4);

Assert.IsTrue(selection.Succeeded,
    $"Selection did not settle; focus={selection.LastObservation?.FocusedPath ?? "<none>"}.");
```

Shell automation may transiently return a null `FolderItem` while a copied file is entering the
view. The framework treats that as not-ready and retries; module tests should not enumerate Shell COM
items themselves.

## Harden menu- and selection-driving steps for slow agents

CI agents are far slower than a local VM and ARM64 timing differs from x64, so races that never appear
locally - even on a 1-core guest - surface on CI, and you often cannot reproduce them. Reason from the
failure video/screenshot and make each step self-correcting instead of one-shot.

**Re-establish the selection before every attempt.** A slow agent re-renders the Explorer view
asynchronously after a module toggles or the shell restarts and drops the selection, so the
right-click targets an unready view and no menu appears. Re-run `SetSelectionAndWaitForStable` inside
the retry loop, not once before it, and reopen a fresh window if it keeps failing:

```csharp
while (DateTime.UtcNow < deadline)
{
    if (!TrySelectStable(explorer, filePaths))          // non-throwing SetSelectionAndWaitForStable
    {
        if (++failures >= 2) { explorer = OpenExplorer(folder); failures = 0; }  // stale/empty window
        continue;
    }

    var menu = OpenContextMenu(explorer);
    if (menu is not null && HasCommand(menu)) { break; }
}
```

**Treat transient popups as retryable.** A menu popup (for example "Show more options") can vanish
between finding it and invoking it, so a raw `Invoke()` throws. Catch it, return null, and let the
caller reopen the menu rather than failing the test.

**Verify fixtures actually reached disk.** `Bitmap.Save` can lag on a slow/ARM64 agent; a genuinely
empty folder is not a slow-to-render view. Assert `File.Exists` (retry the save once) immediately
after creating a fixture, and prefer committed test assets (Peek's `TestAssets`) over runtime-generated
images when arch-portability matters.

**Size timeouts for the slow path.** A tier-2 ("Show more options") menu can take ~15s to render under
CI load; use surface waits of >=25s and retry-loop deadlines of >=90s. On a fast agent these return
immediately, so there is no happy-path cost.

## Set view mode and icon size directly

Do not rely on `Ctrl+Shift+1/2/3` for thumbnail tests. Under CI load Explorer can drop the shortcut
while remaining foreground and stay in Details view. Set the authoritative Shell state:

```csharp
var view = ExplorerShell.SetViewModeAndIconSizeAndWait(
    new IntPtr(explorer.WindowHandle),
    ExplorerShell.ViewMode.Icons,
    iconSize: 256,
    timeoutMS: 5_000);

Assert.IsTrue(view.Succeeded,
    $"View did not settle; mode={view.LastObservation?.Mode}, size={view.LastObservation?.IconSize}.");
```

Still assert on the visible layout. A Shell state match proves the setting, while tile geometry proves
Explorer laid it out. For example, extra-large tiles should be materially taller than a Details row;
then large and medium captures should have strictly descending heights.

## Drive Shell extensions through Explorer

Exercise the user workflow, not a shortcut from the test host. A direct
`IShellItemImageFactory.GetImage` call can run under a different integrity/registration context and
return `REGDB_E_CLASSNOTREG` even though non-elevated Explorer can activate the per-user handler.

### Preview handler sequence

1. Open a fresh Explorer file window.
2. Detect whether the Preview pane is already visible; its state persists across windows/runs.
3. If absent, foreground Explorer, send `Alt+P`, and require the empty-pane marker to appear. Retry
   the guarded toggle only while the pane remains absent.
4. Capture an empty-pane baseline.
5. Set exact Shell selection/focus for the file.
6. Require the PowerToys provider log with no launch failure.
7. Require a visible pixel change in the preview region.

### Thumbnail provider sequence

1. Open an empty temporary folder in Explorer.
2. Clear the provider's old log and copy the fixture into the folder.
3. Refresh Explorer and establish exact Shell selection/focus.
4. Set `ViewMode.Icons` and the desired icon size (for example 256 px).
5. Require the provider log before accepting the rendered tile.
6. Capture the file item at each required size; assert non-generic visual detail and descending tile
   geometry.

Do not confuse the Preview pane with thumbnail output. Thumbnail tests assert the file tile/icon in
the main folder view; the Preview pane may remain empty and is irrelevant.

## Use layered evidence

One signal is not enough for Shell rendering:

| Evidence | Proves |
|---|---|
| Effective extension association | Shell points the extension at the expected CLSID |
| Provider process log | PowerToys' handler was actually activated |
| Exact Shell selection/view state | Explorer consumed the intended file in the intended layout |
| Visible pixels/item capture | The user-visible output rendered and is not generic/blank |

Capture both the empty/before state and rendered/after state for previews. For thumbnails, capture
the item rectangle at each requested size. A passing assertion should survive inspection of those
artifacts by a human.

## Preserve failure evidence before cleanup

Derived MSTest cleanup runs before the base cleanup. If it closes Explorer first, the recording ends
on Settings or the desktop and hides the failure. Begin derived cleanup with:

```csharp
await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
```

The method is a no-op for passing tests, holds failed UI briefly when requested, captures a terminal
desktop PNG, finalizes the recording, and is idempotent with the base cleanup.

## Failure classification

| Symptom | Likely boundary | Response |
|---|---|---|
| Highlighted row but wrong file opens | Shell selection/focus | Use `SetSelectionAndWaitForStable`; inspect last snapshot |
| Details row instead of thumbnail tile | Shell view mode/icon size | Use `SetViewModeAndIconSizeAndWait`; verify tile geometry |
| `REGDB_E_CLASSNOTREG` only from test-host COM | Activation context | Remove direct COM probe; let Explorer activate the provider |
| Provider log exists, pixels unchanged | Renderer/compositor | Wait for visible output; attach before/after captures |
| Remote debugger dies during Shell restart | Process-tree teardown | Kill only Explorer, never its descendants |
| Video ends after Explorer disappears | Cleanup ordering | Capture failure artifacts before closing Explorer |
| No context menu after a module toggle on a slow agent | Dropped selection / async view render | Re-select before every attempt; widen surface waits; reopen a stale window |
| Fixture folder is genuinely empty (0 items) | Fixture not flushed to disk | Verify `File.Exists` + retry the save; prefer committed assets |

## Pre-flight checklist

- [ ] Runner lifetime, Shell restart count, and file-window lifetime are explicit.
- [ ] Exact selected paths and focused path come from `ExplorerShell`.
- [ ] View mode/icon size comes from `ExplorerShell`, not keyboard shortcuts.
- [ ] Shell extensions are activated by Explorer in the user context.
- [ ] Provider logs and visible output are both asserted.
- [ ] Preview-pane toggles are state-guarded and verified.
- [ ] Explorer restarts kill only Explorer, not the process tree.
- [ ] Failure artifacts are finalized before derived cleanup closes Explorer.
- [ ] Focused scenario passes first, then all scenarios pass on x64 and ARM64 CI-equivalent runs.
