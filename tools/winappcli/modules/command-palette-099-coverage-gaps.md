# CmdPal — Tests planned but not yet implemented (v0.96–0.99 gap)

This document tracks tests **intentionally deferred** when adding CmdPal coverage
for the v0.96 → v0.99.1 feature window. The active tests live in
`command-palette-checklist.ps1`; the entries below explain what would still be
worth adding, with an honest assessment of cost and value.

The deferral categories follow the project convention:

| Category | Meaning |
|---|---|
| `PER-EXT-FILE` | Per-extension settings file is lazily created; only exists after the user opens that extension's settings page. Test would either spuriously skip or require driving the Extensions Settings UI first. |
| `UI-DRIVING` | Needs `winapp ui invoke` / `winapp ui click` against a sub-page, popup, or context menu. Doable but each new pattern is 30 min – 2 h of UIA probing. |
| `NEEDS-FIXTURE` | Requires authoring a custom extension binary that emits a specific content type or behavior. Hard to do without adding the fixture to the repo. |

---

## Per-extension settings (PER-EXT-FILE)

PR #46685 (Gave each built-in extension its own settings file with transparent
one-time migration from the legacy shared settings.json) split per-extension
prefs out of the shared `settings.json`. Empirical finding: those per-extension
files **don't exist until the user has opened that extension's settings page**
(or set a non-default value via the UI). On a fresh CmdPal install, none of
them are present.

That makes the following tests environment-dependent — they would all silently
skip on a clean machine and only run after the user has manually touched each
extension's settings page once. Not worth shipping until that bootstrap is
automated.

### `CmdPal_WebSearch_CustomSearchEngineSettingRoundTrips`
- **Tracks**: 0.97.0 — WebSearch extension custom search engine setting.
- **Why deferred**: The CustomSearchEngine URL is stored in the WebSearch per-
  extension settings file, not in the shared `ProviderSettings.com.microsoft.cmdpal.builtin.websearch`
  (which only contains `IsEnabled`/`FallbackCommands`/`PinnedCommandIds`).
- **To unblock**: drive the Extensions Settings UI page once to force the
  per-ext file to materialize, then write+restart+verify.

### `CmdPal_PerformanceMonitor_NetworkSpeedUnitSettingRoundTrips`
- **Tracks**: 0.99.0 PR #46320 — Performance Monitor extension NetworkSpeedUnit choice setting.
- **Why deferred**: Same PER-EXT-FILE issue. The NetworkSpeedUnit enum
  (`bits/s` / `decimal bytes/s` / `IEC binary bytes/s`) lives in the
  PerformanceMonitor per-extension settings file.
- **To unblock**: same approach as WebSearch.

### `CmdPal_WindowWalker_KeepOpenAfterClosingWindowSettingRoundTrips`
- **Tracks**: 0.99.0 PR #45721 — Window Walker "Keep open after closing window" setting.
- **Why deferred**: Same PER-EXT-FILE issue.

---

## Tests that need actual UI driving (UI-DRIVING)

These need a real `winapp ui invoke` / `click` against a sub-page or popup
that the existing port hasn't probed yet. **Round 3 update:** the "More
context menu" mechanism IS reachable via UIA in 0.99 (`MoreContextMenuButton`
→ `PopupHost` window with `CommandsDropdown` List of menu items), so two
of the previously-deferred entries were promoted to active.

### ~~`CmdPal_PinToDockDialog_HasTitleSubtitleToggles`~~ → **NOW ACTIVE**
Implemented as `CmdPal_Pin_PinToDockDialogAppearsAfterMoreMenuClick`
(round 3). The test enables Dock in settings, opens the More menu, and
verifies the "Pin to dock" ListItem appears.

### ~~`CmdPal_Navigation_PgUpPgDownSkipsSeparatorsAndHeaders`~~ → **NOW ACTIVE**
Implemented as `CmdPal_Navigation_SeparatorListItemsAreMarkedDisabled`
(round 3, narrowed scope). Rather than driving PgUp/PgDown via Send-PtKey
(fiddly across the AppX/UIA boundary), the test verifies the structural
invariant the fix relies on: separator/header ListItems are marked
`[disabled]` in the UIA tree, so the navigation logic can skip them.

## Context-menu deferred tests — additional barrier discovered

The list of CmdPal tests that previously cited "CommandsDropdown is UIA-
virtualised + offscreen" as a blocker is:

  - `CmdPal_Files_ContextMenu_OpenAndCopyPathAndShowInFolder`
  - `CmdPal_Pin_PinCommandToDockViaContextMenu`
  - `CmdPal_Registry_NavigateAndCopyKeyPath`
  - `CmdPal_Calculator_HistoryClearAndDeleteViaUI`
  - `CmdPal_TerminalProfiles_PinningWithPerProfileIcons`

In 0.99, the menu mechanism IS now UIA-reachable (`MoreContextMenuButton`
→ `PopupHost` → `CommandsDropdown` List). 2 of these were promoted to
active in round 3 because they don't depend on which specific item is
highlighted (PinToDockDialog and separator schema).

But the remaining **4 specific-item context-menu tests** still have a
deeper blocker discovered in round 4 probing:

**Discovery**: the `MoreContextMenuButton` opens a context menu reflecting
the CURRENTLY-HIGHLIGHTED list item — NOT the item set via `winapp ui focus`.
UIA `SetFocus` against a ListItem in WinUI 3 changes accessibility focus
but does NOT change list-selection highlighting, which is what populates
the More menu's contents. So tests that need a specific item's context
menu (e.g. file's "Copy path", registry key's "Copy path", calculator
result's "Clear history") need to drive REAL keyboard navigation (Down
arrow) to move the highlight first.

We have `Send-PtKey 'Down'` available, but additional issues:

1. **Window foreground**: `Send-PtKey` requires the CmdPal window to be
   in the foreground at SendInput time. CmdPal AppX often loses
   foreground after `winapp ui inspect` or `winapp ui search` calls,
   so each test needs explicit `Set-WindowForeground` before each
   keypress.

2. **Popup stability**: the PopupHost window's UIA tree is unstable
   to repeated `winapp ui inspect` calls — observed cases where the
   second inspect of the same popup returned empty. Probably the
   inspect briefly transfers focus, which collapses the popup.
   Workaround: open the menu, do one inspect, close, repeat for each
   assertion.

3. **List highlight is non-observable**: there's no `IsSelected=True`
   on the highlighted ListItem in WinUI 3 — selection state is
   tracked by the WinUI ListBox's internal `SelectedIndex` which
   isn't exposed via UIA. So we can't verify "the right item is
   highlighted before opening the menu" via UIA alone. Tests have to
   trust that N Down arrows moves N items.

### Estimated effort to unlock the remaining 4

Each test needs:
- `Set-WindowForeground -Hwnd $cpHwnd` before each Down arrow
- N x `Send-PtKey 'Down'` (calibrated per test — depends on how the
  query results are sorted)
- `winapp ui invoke 'MoreContextMenuButton'`
- Find popup HWND
- Single inspect for the desired menu item
- Invoke that item
- Verify outcome (clipboard, file deletion, dock band added)

Each test is ~1 h of probing + flakiness mitigation. Total for 4: ~4 h
of careful work, likely with some tests still ending up flaky.

The most valuable ones to unblock next, in priority order:

1. **`CmdPal_Files_ContextMenu_CopyPathToClipboard`** — clearest signal
   (clipboard content). Low coupling to other features.
2. **`CmdPal_Calculator_HistoryClearViaUIDeletesHistoryFile`** — calc
   history sub-page only has 2-3 commands so highlight management is
   simpler than Files. Strong outcome (file size goes to ~empty).
3. **`CmdPal_Pin_ActuallyPinsCommandToDockBand`** — extends round-3 Pin
   dialog test by actually clicking the dialog's Pin button. Need to
   probe the dialog's UIA tree (separate Popup or inline?).
4. **`CmdPal_Registry_DeepWalkAndCopyKeyPath`** — needs registry
   sub-page deep navigation first; most complex.

### `CmdPal_ClipboardHistory_DragDropCapabilityExposed`
- **Tracks**: 0.97.0 — ClipboardHistory drag-drop support added in 0.97.
- **Why deferred**: UIA doesn't directly expose "this element supports
  drag-drop"; would need a custom IDataObject probe via Win32
  OleGetClipboard after starting a drag. Not impossible but not 30 min.
- **Effort**: ~2-3 h.

### `CmdPal_Indexer_WindowsSearchAvailabilityIndicatorShown`
- **Tracks**: 0.99.0 PR #46907 — Indexer search shows Windows Search availability indicator.
- **Why deferred**: Round 3 probing was inconclusive — the indicator may
  only render when Windows Search is actually unavailable (which is
  rare on dev machines, so test would trivially skip). To test it
  meaningfully, we'd need to temporarily stop the WSearch service
  (admin + side-effects on the host).
- **Effort**: ~2 h + requires admin.

---

## Tests that need an authored extension fixture (NEEDS-FIXTURE)

### `CmdPal_Extensions_PlainTextContentTypeRendersInDetailsPanel`
- **Tracks**: 0.99.0 PR #43964 — plain text viewer IContent added to extension SDK.
- **Why deferred**: There is no built-in extension that emits plain text
  content. We'd need to author and install a tiny test extension whose
  command exposes a plain-text content panel, then query for it.

### `CmdPal_Extensions_ImageContentTypeRendersZoomableImage`
- **Tracks**: 0.99.0 PR #43964 — image viewer IContent added to extension SDK.
- **Why deferred**: Same as plain text — no built-in extension produces an
  image-typed content panel.

### `CmdPal_Extensions_BadExtensionDoesNotBreakOthers`
- **Tracks**: 0.99.0 PR #47032 — one bad extension no longer kills the loop.
- **Why deferred**: Already documented in the .ps1 skip list as `NEEDS-FIXTURE`.
  Same root cause — need a deliberately-broken extension binary.

---

## Implemented in this round

For reference, the **13 active tests** added to track 0.96 → 0.99 features
(13 = 9 from round 1 + 4 from round 2):

**Round 1 (commit `546c0f1030`)**:
- `CmdPal_DockSchema_NullDockSettingsDoesNotCrashOnStartup` (★ 0.99.1 PR #47296)
- `CmdPal_DockSchema_ShowLabelsPersistsAcrossSessions` (★ 0.99.1 PR #47317)
- `CmdPal_DockSchema_BandsHavePerCommandShowTitleAndSubtitleFields` (★ 0.99.0 PR #46436)
- `CmdPal_DockSchema_BackdropFieldHasValidValue` (★ 0.99.0)
- `CmdPal_DockSchema_DockSizeFieldIsKnownEnum` (★ 0.99.0 PR #46699)
- `CmdPal_State_LocalStateFilesPresentAndValid` (★ 0.99.0 PR #46685)
- `CmdPal_Settings_PersonalizationFieldsExistInSchema` (★ 0.97.0)
- `CmdPal_Providers_NewBuiltinProvidersFor097And099Present` (★ 0.97-0.99 PR #46198)
- `CmdPal_Stability_TypingDoesNotCrashWithProviderSettingsIntact` (★ 0.99.0 PRs #47148+#47186)

**Round 2 (this round)**:
- `CmdPal_Settings_FallbackRanksFieldIsValidArray` (★ 0.97.0)
- `CmdPal_PowerToysExtension_FancyZonesLayoutsListedViaSearch` (★ 0.97-0.99 PR #46198)
- `CmdPal_PowerToysExtension_ColorPickerListedViaSearch` (★ 0.97.0)
- `CmdPal_TerminalProfiles_BadGuidInWtSettingsDoesNotBreakListing` (★ 0.99.0 PR #46372)

---

**Round 3 (this round)** — implemented 2 of 5 UI-driving deferrals after probing:
- `CmdPal_Pin_PinToDockDialogAppearsAfterMoreMenuClick` (★ 0.99.0 PR #46436)
- `CmdPal_Navigation_SeparatorListItemsAreMarkedDisabled` (★ 0.99.0 PR #46439, narrowed scope)

Empirical finding (round 3): the `MoreContextMenuButton` IS UIA-reachable
in 0.99 (was previously assumed virtualised+offscreen). Clicking it
opens a separate `PopupHost` window containing a `CommandsDropdown`
List with addressable menu items. This unblocks the entire family of
"context menu → invoke item" tests for free; the remaining ones are
deferred only because their downstream UI (dialogs, dock window) needs
more probing.

---

## Source release notes

- v0.97.0: https://github.com/microsoft/PowerToys/releases/tag/v0.97.0
- v0.98.x: minor CmdPal changes only (consult upstream notes)
- v0.99.0: https://github.com/microsoft/PowerToys/releases/tag/v0.99.0
- v0.99.1: https://github.com/microsoft/PowerToys/releases/tag/v0.99.1
