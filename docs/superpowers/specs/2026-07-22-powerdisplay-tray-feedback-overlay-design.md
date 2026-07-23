# PowerDisplay Tray Feedback Overlay Design

| Field | Value |
| --- | --- |
| Parent issue | [microsoft/PowerToys#49410](https://github.com/microsoft/PowerToys/issues/49410) |
| Pull request | [microsoft/PowerToys#49446](https://github.com/microsoft/PowerToys/pull/49446) |
| Parent design | `docs/superpowers/specs/2026-07-21-issue-49410-design.md` |
| Supersedes | `docs/superpowers/specs/2026-07-22-powerdisplay-tray-tooltip-feedback-design.md` |
| Status | Approved design amendment |
| Date | 2026-07-22 |

## Why the Native Tooltip Design Was Rejected

The approved native-tooltip design required immediate visible feedback during the same tray-icon
hover. A current-branch runtime probe established:

- `Shell_NotifyIcon(NIM_MODIFY)` successfully updates registered tooltip data.
- The modern overflow UI is a XAML Shell surface.
- It exposes no classic `SysPager` / `ToolbarWindow32` tooltip HWND.
- The EarTrumpet `TB_GETTOOLTIPS` / `TTM_POPUP` path is therefore unavailable.
- The tooltip already visible in the current hover does not refresh after `NIM_MODIFY`.

The native design failed its explicit go/no-go criterion before any product source was changed.
PowerDisplay will instead own a no-activate, click-through WinUI overlay that replaces both ordinary
tray hover text and wheel-adjustment feedback.

## Summary

PowerDisplay will permanently disable the Shell-managed notification tooltip and replace it with a
small native-looking WinUI overlay anchored to the real PowerDisplay notification icon.

Behavior:

1. Hover the icon for approximately 500 ms: show `Power Display`.
2. Scroll a complete wheel notch: immediately show target and post-adjustment brightness values.
3. Stop scrolling for two seconds:
   - if the pointer is still over the icon, return to `Power Display`;
   - otherwise hide.
4. Leave the icon at any time: hide immediately.

Examples:

- `Primary display · 55%`
- `Primary displays · 35%, 70%`
- `All displays · 35%, 70%`
- `All displays · 35–90% (6 displays)`

The window never activates, never accepts input, does not appear in Alt+Tab, and is positioned from
`Shell_NotifyIconGetRect` toward the inside of the icon's display.

## Confirmed Product Decisions

- Permanently remove the native PowerDisplay `NIF_TIP` tooltip.
- Use the custom overlay for ordinary hover and wheel feedback.
- Show ordinary hover text after 500 ms, not immediately.
- Poll pointer containment every 100 ms only while a hover session is active.
- Show wheel feedback immediately.
- Restore `Power Display` two seconds after the last valid adjustment if still hovered.
- Hide immediately when the pointer leaves.
- Keep ordinary hover feedback active even when mouse-wheel control mode is `Disabled`.
- Make the overlay no-activate, topmost, absent from task switchers, and mouse-click-through.
- Anchor to the notification icon and expand toward the screen interior for bottom, top, left, and
  right taskbars.
- Use an 8 DIP icon-to-overlay gap.
- Use 12 DIP horizontal and 8 DIP vertical padding.
- Use an 8 DIP corner radius.
- Use a one-line layout with a 120 DIP minimum and 420 DIP maximum width.
- Use current PowerToys/Windows theme resources.
- Preserve the previously approved target-first wording and count/range rules.
- Do not add a setting for the overlay.

## Goals

- Provide immediate and deterministic feedback on modern pinned and overflow notification UI.
- Replace the ordinary application tooltip so only one feedback surface is visible.
- Preserve foreground focus and all existing tray interactions.
- Handle mixed DPI, multiple displays, every taskbar edge, and overflow placement.
- Keep hover and timing behavior independently unit-testable.
- Keep geometry calculations independently unit-testable.
- Preserve the low-level hook hot path and self-healing registration state machine.
- Provide accessible live text despite the no-activate, click-through window.

## Non-goals

- A focusable or clickable popup.
- A close button, progress bar, icon, or per-monitor names.
- A Windows toast or notification-center entry.
- A permanent on-screen brightness indicator.
- Persisting overlay state across process or Explorer restarts.
- Changing the existing wheel mode settings or target-selection semantics.
- Changing the notification callback from version 0.
- Replacing `Shell_NotifyIconGetRect` as the icon geometry source.

## Existing Components Reused

- `TrayWheelAdjustmentFeedback`
  - mode plus post-adjustment values
- `TrayWheelFeedbackFormatter`
  - selected A wording, primary singular/plural, all-display values, range/count fallback, 127-unit
    limit, and localized template fallback
- `TrayWheelAdjustmentPlanner`
  - eligible targets and post-clamp values
- `TrayIconService`
  - notification identity, current bounds, callback messages, registration health, and dispatcher
- `FlyoutWindowHelper`
  - display DPI and cross-monitor `MoveAndResizeOnDisplay`
- `WindowEx`
  - borderless WinUI window foundation

## Native Notification Registration

`EnsureTrayIconIdentity` will no longer register a Shell tooltip:

- remove `NIF_TIP` from `NOTIFYICONDATAW.uFlags`
- leave `szTip` empty/default
- keep `NIF_MESSAGE | NIF_ICON`

No tooltip-related `NIM_MODIFY`, toolbar-window search, or tooltip-window message remains in the
product implementation.

This change does not affect icon identity, click callbacks, wheel hover callbacks, context menu, or
registration health. Explorer continues sending `WM_MOUSEMOVE` for the icon because `NIF_MESSAGE`
remains set.

## Pure Hover Session State

Add `TrayWheelFeedbackSession` to `PowerDisplay.Lib`. It has no timers, WinUI, Shell, or resource
dependencies.

Inputs:

- monotonic milliseconds
- whether the pointer is still inside the icon rectangle
- optional formatted adjustment text

State:

- idle/hidden
- hover start timestamp
- optional adjustment text
- adjustment expiration timestamp

Outputs:

- `Hidden`
- `AppName`
- `Adjustment` with text

Transitions:

1. `StartHover(now)`
   - preserves an existing hover session
   - records `now` only on first entry
2. `Tick(now, pointerInside: false)`
   - clears all state
   - returns `Hidden`
3. `Tick(now, pointerInside: true)` before 500 ms and without adjustment
   - returns `Hidden`
4. `Tick(now, pointerInside: true)` at or after 500 ms
   - returns `AppName`
5. `ShowAdjustment(text, now)`
   - marks hover active
   - stores text
   - sets expiration to `now + 2000 ms`
   - returns `Adjustment`
6. `Tick` before adjustment expiration
   - returns `Adjustment`
7. `Tick` at or after expiration while inside
   - clears adjustment text
   - returns `AppName`
8. `Stop()`
   - clears all state
   - returns `Hidden`
9. A null/no-target adjustment while inside
   - clears adjustment text
   - returns `AppName`

Timestamp comparisons use subtraction-based monotonic arithmetic consistent with the existing wheel
sample age logic.

## Pure Overlay Placement

Add `TrayWheelFeedbackPlacement` to `PowerDisplay.Lib`.

Inputs are physical pixels:

- icon rectangle
- display outer bounds
- display work area
- overlay width and height
- gap

Algorithm:

1. Compute the icon center.
2. Calculate its distance to each outer display edge.
3. Select the closest edge as the taskbar/overflow side.
4. Place toward the screen interior:
   - bottom edge: centered over the icon, above it
   - top edge: centered under the icon
   - left edge: vertically centered, to the right
   - right edge: vertically centered, to the left
5. Clamp the final rectangle to the display work area.

If distances tie, prefer bottom, then top, then left, then right. This gives deterministic behavior
for unusual taskbar/overflow geometry.

The overlay gap is calculated as 8 DIP scaled to target-monitor physical pixels before calling the
pure helper.

## Overlay Window

Add:

- `PowerDisplayXAML/TrayWheelFeedbackWindow.xaml`
- `PowerDisplayXAML/TrayWheelFeedbackWindow.xaml.cs`

### XAML

Use a borderless `WindowEx`:

- `IsTitleBarVisible="False"`
- `IsResizable="False"`
- `IsMinimizable="False"`
- `IsMaximizable="False"`

Content:

- root `Border`
- `Padding="12,8"`
- `CornerRadius="8"`
- theme background, stroke, and primary text brushes
- one `TextBlock`
- no wrapping
- `MaxWidth="420"`
- `AutomationProperties.LiveSetting="Polite"`

### Native window styles

After obtaining the HWND, preserve existing extended styles and add:

- `WS_EX_NOACTIVATE`
- `WS_EX_TOOLWINDOW`
- `WS_EX_TRANSPARENT`

Set topmost without activation.

Subclass the overlay window procedure and return `HTTRANSPARENT` for `WM_NCHITTEST`. This combines
with `WS_EX_TRANSPARENT` so the window never becomes a mouse target even when it overlaps a Shell
surface from another process.

Show through:

- `ShowWindow(SW_SHOWNOACTIVATE)`
- `SetWindowPos(HWND_TOPMOST, ..., SWP_NOACTIVATE)`

Never call `Activate()` to show this window.

Use `SetIsShownInSwitchers(false)` and best-effort rounded DWM corners. DWM corner APIs may be
unavailable on older Windows and must not block display.

### Measurement and DPI

`ShowText`:

1. Set the `TextBlock` text.
2. Measure the root content in DIP.
3. Clamp measured width to 120-420 DIP.
4. Obtain the target `DisplayArea` from the notification icon center.
5. Use `FlyoutWindowHelper.GetDpiScale(displayArea)`.
6. Convert measured width, height, and the 8 DIP gap to physical pixels.
7. Calculate the final physical rectangle with `TrayWheelFeedbackPlacement`.
8. Use `FlyoutWindowHelper.MoveAndResizeOnDisplay`.
9. Call `ShowWindow(SW_SHOWNOACTIVATE)` and enforce topmost/no-activate positioning.

`Hide` uses `ShowWindow(SW_HIDE)` and retains the window for reuse.

`Dispose` hides, clears event/timer references, closes the WindowEx, and is idempotent.

## Accessibility

The overlay cannot receive focus, so text changes must raise a live notification explicitly:

1. Set `AutomationProperties.LiveSetting="Polite"` on the text element.
2. Obtain/create its `FrameworkElementAutomationPeer`.
3. Raise `AutomationNotificationKind.Other` with
   `AutomationNotificationProcessing.MostRecent`.
4. Use a stable activity ID, for example `PowerDisplayTrayFeedback`.

This coalesces rapid wheel updates rather than queueing every percentage announcement.

Do not announce `Hidden`. Announce `Power Display` on normal hover and adjustment text when it
changes.

## TrayIconService Hover Coordinator

`TrayIconService` owns:

- one lazy `TrayWheelFeedbackWindow`
- one repeating 100 ms `DispatcherQueueTimer`
- one pure `TrayWheelFeedbackSession`
- the latest valid icon bounds
- one rate-limited window-creation/style failure flag

### Enter/update hover

`HandleTrayMouseMove` must be split:

1. Resolve current cursor and notification icon bounds for all wheel modes.
2. Start or continue the feedback session.
3. Start the 100 ms timer if needed.
4. If wheel mode is not `Disabled`, continue the existing hook-arm path.
5. If wheel mode is `Disabled`, do not create/arm `TrayIconMouseWheelListener`.

Repeated `WM_MOUSEMOVE` inside the icon must not reset the 500 ms hover start.

### Poll

Every 100 ms while active:

1. Re-query current icon bounds and cursor position.
2. If either fails or the cursor is outside:
   - stop the session
   - hide the overlay
   - stop the timer
3. Otherwise tick the session:
   - `Hidden`: keep polling until 500 ms
   - `AppName`: show localized `AppName`
   - `Adjustment`: keep current feedback visible

The hover poll uses a non-destructive bounds-query helper. A single poll failure hides/stops the
overlay but does not mark notification registration stale or enter registration recovery. The
existing five-second registration health state machine remains authoritative for Explorer
registration loss.

### Adjustment

`UpdateAdjustmentFeedback` always runs on the UI thread:

- non-null feedback:
  - format through `TrayWheelFeedbackFormatter`
  - if formatting succeeds, call `ShowAdjustment`
  - show/reposition immediately
- null feedback:
  - clear adjustment state
  - show `AppName` if still hovered
  - otherwise hide

Every adjustment extends the expiration to two seconds from the latest wheel action.

### Stop/cleanup

Add `StopHoverFeedback()` and call it from:

- pointer leave
- `Destroy`
- registration-stale transition
- icon hidden
- Explorer restart handling
- module shutdown

It stops the 100 ms timer, resets the session, hides the window, and clears cached overlay bounds.
It is idempotent.

## Error Handling

- Failure to create/configure the overlay window logs once per window lifecycle and never blocks
  brightness adjustment.
- `GetCursorPos`, current icon bounds, or `DisplayArea` failure hides feedback without per-poll logs.
- DWM corner failure is ignored.
- Show/move failure hides the window and logs once.
- No overlay operation runs from the low-level hook callback.
- Overlay failure never alters notification registration state or retry backoff.
- Native Shell callback and registration errors retain their existing handling.

## Localization

Reuse the formatting resources defined by the superseded design:

- Primary display format
- Primary displays format
- All displays format
- Percentage format
- Range/count format
- List separator

The ordinary hover label uses existing `AppName` (`Power Display`).

Resource comments document every placeholder and provide English examples.

## Planned Files

Already completed and retained:

- `PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs`
- `PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs`
- `PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs`
- `PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs`
- `PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs`
- `PowerDisplay/PowerDisplayXAML/App.xaml.cs` return-value plumbing

New:

- `PowerDisplay.Lib/Services/TrayWheelFeedbackSession.cs`
- `PowerDisplay.Lib/Services/TrayWheelFeedbackPlacement.cs`
- `PowerDisplay.Lib.UnitTests/TrayWheelFeedbackSessionTests.cs`
- `PowerDisplay.Lib.UnitTests/TrayWheelFeedbackPlacementTests.cs`
- `PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml`
- `PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml.cs`

Modified:

- `PowerDisplay/Helpers/TrayIconService.cs`
- `PowerDisplay/PowerDisplayXAML/App.xaml.cs`
- `PowerDisplay/Strings/en-us/Resources.resw`

The failed native-tooltip probe adds no tracked product files.

## Automated Test Plan

### Session

- hover before 500 ms remains hidden
- hover at 500 ms shows AppName
- adjustment shows immediately
- subsequent adjustment extends the two-second deadline
- expiry inside returns AppName
- expiry outside hides
- leave before hover delay hides
- null feedback inside returns AppName
- null feedback outside remains hidden
- Stop is idempotent
- monotonic timestamp wrap-safe behavior

### Placement

- bottom edge positions above
- top edge positions below
- left edge positions right
- right edge positions left
- tie order is bottom/top/left/right
- horizontal and vertical centering
- work-area clamp on every side
- icon inside overflow still selects nearest outer edge
- physical sizes/gap remain unmodified by pure placement

### Existing

- formatter tests remain green
- planner/accumulator/backoff/settings tests remain green
- PowerDisplay x64 and ARM64 builds remain green
- XAML Styler accepts the new window

## Runtime Verification

Use current-branch bits and persisted settings backups:

1. Native Shell tooltip no longer appears for PowerDisplay.
2. Hover for less than 500 ms shows nothing.
3. Hover for at least 500 ms shows one `Power Display` overlay.
4. Moving away hides it within the 100 ms poll interval plus scheduling tolerance.
5. `Disabled` mode still shows ordinary hover but performs no wheel adjustment.
6. Primary wheel adjustment immediately shows the current percentage.
7. All mode immediately shows both current percentages.
8. `+60` alone does not change text; the second `+60` applies and updates.
9. Rapid wheel input extends the two-second feedback deadline.
10. Two-second expiry while still hovered restores `Power Display`.
11. Leaving during feedback hides instead of restoring.
12. Overlay never becomes foreground and never appears in Alt+Tab.
13. Clicking through the overlay still reaches the underlying tray surface.
14. Bottom taskbar and current overflow positioning are correct.
15. Other taskbar edges and mixed DPI are verified where environment permits.
16. Hide/re-enable and Explorer restart do not leave a stale overlay.
17. Existing tray click/menu, wheel targeting, brightness, and registration recovery remain green.
18. All settings, brightness, Explorer, and original runner state are restored exactly.

## Acceptance Criteria

1. PowerDisplay registers no native tooltip.
2. Ordinary hover uses the custom overlay after approximately 500 ms.
3. Wheel feedback appears immediately with the approved wording and values.
4. Feedback restores to `Power Display` after two seconds while still hovered.
5. Pointer leave hides the overlay promptly.
6. Disabled mode retains ordinary hover without installing the low-level wheel hook.
7. The overlay is no-activate, click-through, topmost, and absent from task switchers.
8. Positioning follows the icon and expands inward for all taskbar edges.
9. Positioning and size honor target-monitor DPI.
10. UIA live notifications expose ordinary and adjustment text without focus.
11. Registration loss, hide, Explorer restart, and destroy clear overlay state.
12. Overlay failures never block brightness adjustment or alter registration recovery.
13. Pure session and placement behavior is comprehensively unit-tested.
14. Existing parent-feature automated and runtime validation remains green.
