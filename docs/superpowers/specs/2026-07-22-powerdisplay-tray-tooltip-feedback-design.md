# PowerDisplay Tray Wheel Tooltip Feedback Design

| Field | Value |
| --- | --- |
| Parent issue | [microsoft/PowerToys#49410](https://github.com/microsoft/PowerToys/issues/49410) |
| Pull request | [microsoft/PowerToys#49446](https://github.com/microsoft/PowerToys/pull/49446) |
| Parent design | `docs/superpowers/specs/2026-07-21-issue-49410-design.md` |
| Status | Superseded after native-tooltip go/no-go failure |
| Date | 2026-07-22 |

> Superseded by `2026-07-22-powerdisplay-tray-feedback-overlay-design.md`. Runtime probing proved
> that modern overflow UI has no classic `SysPager/ToolbarWindow32` tooltip HWND and does not
> refresh visible tooltip text after `NIM_MODIFY` during the same hover.

## Summary

When tray-icon wheel input successfully adjusts brightness, PowerDisplay will update and
immediately show its native notification-area tooltip. The tooltip identifies whether the
adjustment targeted the primary logical display or all visible controllable displays and shows the
post-adjustment brightness percentages.

Examples:

- `Primary display · 55%`
- `Primary displays · 35%, 70%`
- `All displays · 35%, 70%`
- `All displays · 35–90% (6 displays)`

The implementation keeps the native Windows tooltip instead of introducing a custom OSD. It uses
`Shell_NotifyIcon(NIM_MODIFY)` to update `szTip` and the EarTrumpet pattern
(`TB_GETTOOLTIPS` + `TTM_POPUP`) as a best-effort request to show the tooltip immediately. Two
seconds after the last successful adjustment, the tooltip returns to the localized PowerDisplay
application name.

## Confirmed Product Decisions

- Use the native notification-area tooltip, not a custom WinUI popup or Windows notification.
- Use target-first concise wording.
- Keep the tooltip visible according to normal Shell hover behavior.
- Restore the normal application name two seconds after the last valid wheel adjustment.
- Reset the two-second timer after every valid wheel adjustment.
- List exact percentages for one through four adjusted physical displays.
- For more than four displays, show the minimum and maximum percentages plus the display count.
- In primary mirror mode, use the plural `Primary displays` and the same multi-value rule.
- Preserve target enumeration order for exact percentage lists.
- Show the current clamped value even when the target is already at 0% or 100%.
- If no valid adjustment target exists, leave the normal PowerDisplay tooltip unchanged.
- Do not add a feedback setting. Feedback is active whenever tray wheel control is active.
- If native tooltip update plus the EarTrumpet popup request cannot provide immediate feedback on
  supported Windows versions, stop and return to design rather than silently adding a custom OSD.

## Goals

- Confirm which target scope received a tray wheel adjustment.
- Show values that exactly match the brightness values sent to the existing debounce path.
- Match the visual language, placement, DPI behavior, and accessibility of Windows tray tooltips.
- Work for pinned and overflow notification icons.
- Keep all Shell/UI work out of the low-level mouse hook callback.
- Preserve current tray registration recovery, click behavior, context menu, and wheel input flow.
- Keep localization and string-length behavior deterministic and testable.

## Non-goals

- A custom no-activate popup window.
- A progress bar, icon animation, toast, banner, or notification-center entry.
- Persisting the last feedback text across PowerDisplay restarts.
- Showing an error tooltip when no valid brightness target exists.
- Naming each physical monitor in the tooltip.
- Adding a feedback enable/disable setting.
- Changing the existing version-0 notification callback contract.
- Replacing the self-healing tray registration state machine.

## Reference Implementation

EarTrumpet provides the reference interaction:

- `DeviceCollectionViewModel.GetTrayToolTip()` builds a live percentage tooltip.
- `ShellNotifyIcon.SetTooltip()` updates `NOTIFYICONDATA.szTip` and calls `NIM_MODIFY`.
- Its tray-wheel handler obtains the taskbar toolbar tooltip HWND through `TB_GETTOOLTIPS` and sends
  `TTM_POPUP` before applying the volume change.

Relevant source:

- `File-New-Project/EarTrumpet`, `EarTrumpet/App.xaml.cs`
- `File-New-Project/EarTrumpet`, `EarTrumpet/UI/Helpers/ShellNotifyIcon.cs`
- `File-New-Project/EarTrumpet`, `EarTrumpet/UI/Helpers/WindowsTaskbar.cs`

PowerDisplay will reuse the interaction pattern, not EarTrumpet code.

## Architecture

### Adjustment feedback data

Add a small immutable `TrayWheelAdjustmentFeedback` model containing:

- `MouseWheelControlMode Mode`
- the post-adjustment brightness values in target enumeration order

The model contains no localized text and no Shell types.

`MainViewModel.AdjustBrightnessFromTrayWheel(int notches)` will return
`TrayWheelAdjustmentFeedback?`:

1. Recheck mode, initialization, interaction state, increment, and target availability.
2. Build planner targets and call the existing `TrayWheelAdjustmentPlanner`.
3. Apply every planner result through `MonitorViewModel.Brightness`.
4. Return a feedback model from the same planner results.
5. Return `null` when no planner result exists.

Because the planner intentionally returns eligible targets whose clamped value is unchanged, wheel
input at 0% or 100% still produces accurate feedback without scheduling an unnecessary hardware
write.

### App coordination

`TrayIconService.MouseWheelScrolled` remains an `Action<int>` event. `App` handles it with a lambda:

1. Call `MainViewModel.AdjustBrightnessFromTrayWheel`.
2. Always pass the nullable result to `TrayIconService.UpdateAdjustmentFeedback`.

This keeps monitor selection and brightness state in the view model while keeping notification icon
state and native tooltip delivery inside `TrayIconService`.

### Pure text formatting

Add a pure `TrayWheelFeedbackFormatter` in `PowerDisplay.Lib`. It consumes:

- the feedback mode and brightness values
- localized placeholder templates supplied by the PowerDisplay app
- a maximum UTF-16 length

It produces either a fully formatted tooltip or `null` for empty feedback. It does not load
resources or call Shell APIs.

The formatter is responsible for:

- primary singular/plural selection
- all-displays labeling
- exact percentage lists for up to four values
- range/count summaries for five or more values
- per-value percentage formatting
- placeholder formatting with a neutral-template fallback on `FormatException`
- limiting output to 127 UTF-16 code units without leaving an unmatched high surrogate

## Text Rules

| Mode | Target count | Output |
| --- | ---: | --- |
| Primary | 1 | `Primary display · 55%` |
| Primary | 2-4 | `Primary displays · 35%, 70%` |
| Primary | 5+ | `Primary displays · 35–90% (6 displays)` |
| All | 1-4 | `All displays · 35%, 70%` |
| All | 5+ | `All displays · 35–90% (6 displays)` |

For exact lists, preserve planner/monitor enumeration order. For range summaries, calculate minimum
and maximum values. Each physical target contributes one value, including mirrored physical
monitors sharing the primary GDI source.

The localized resources use placeholder templates:

- Primary display with values
- Primary displays with values
- All displays with values
- Percentage value
- Multi-display range and count
- Exact-list separator if localization requires it

Resource comments describe every placeholder and include an English example.

## Native Tooltip Delivery

`TrayIconService.UpdateAdjustmentFeedback` runs on the UI thread. A `null` result immediately stops
the feedback timer and restores the normal `PowerDisplay` tooltip, so a failed adjustment cannot
leave a previous percentage visible.

### Update

1. Format the feedback text.
2. Stop and restart the two-second restore timer.
3. Copy the stable `NOTIFYICONDATAW` identity.
4. Set `uFlags` to include `NIF_TIP` and assign the formatted `szTip`.
5. Persist the new tooltip in `_trayIconData`.
6. If Explorer registration is healthy, call `Shell_NotifyIcon(NIM_MODIFY)`.
7. If `NIM_MODIFY` fails, mark registration stale and enter the existing registration recovery path.
8. After a successful modify, best-effort request immediate display.

Updating feedback must never call `NIM_ADD`.

### Immediate popup request

Follow the EarTrumpet pattern:

1. Find `Shell_TrayWnd`.
2. Walk `TrayNotifyWnd` -> `SysPager` -> `ToolbarWindow32`.
3. Send `TB_GETTOOLTIPS` to obtain the tooltip window.
4. Send `TTM_POPUP` to that tooltip window.

All HWND lookups and sends are best-effort:

- Missing windows or zero handles do not change registration state.
- Use `SendMessageTimeout(SMTO_ABORTIFHUNG, 100 ms)` for both messages so a stalled Explorer cannot
  block the PowerDisplay UI thread.
- The popup request produces no per-notch warning.
- The updated `NIF_TIP` remains authoritative even if immediate popup cannot be requested.

Constants:

- `TB_GETTOOLTIPS = WM_USER + 35`
- `TTM_POPUP = WM_USER + 34`

The implementation must verify this path with current pinned and overflow notification UI. The
go/no-go acceptance criterion requires the updated text to appear during a real wheel adjustment,
not only after leaving and re-entering the icon.

### Restore

Use a one-shot `DispatcherQueueTimer` with a two-second interval:

1. Every valid feedback update stops and restarts the timer.
2. Timer expiry updates `szTip` to localized `AppName` through `NIM_MODIFY`.
3. It does not request `TTM_POPUP` when restoring.

Hide, registration loss, `TaskbarCreated`, and `Destroy()` stop the timer and reset the stored
tooltip to `AppName`. A later registration therefore never starts with stale adjustment text.

## Registration and Error Handling

- `NIM_MODIFY` is allowed only when `_desiredTrayIconVisible`, `_isTrayIconRegistered`, and
  `_trayIconData` are valid.
- A failed `NIM_MODIFY` marks live registration stale without discarding the stable icon identity,
  disarms the wheel listener, and schedules existing capped-backoff recovery.
- Registration recovery always re-adds the normal `AppName` tooltip.
- Feedback timer callbacks recheck desired visibility and live registration.
- Best-effort toolbar/tooltip discovery failures are silent.
- Placeholder failures catch only `FormatException` and retry with neutral English templates.
- No tooltip operation runs from `TrayIconMouseWheelListener.HookCallback`.
- Expected tooltip failures do not create one log line per wheel notch.

## Localization and Accessibility

Add English source resources to `PowerDisplay/Strings/en-us/Resources.resw`. Translation continues
through the existing PowerToys localization pipeline.

The native Shell tooltip remains the accessibility surface:

- no focusable custom window
- no new tab stop or UI Automation tree
- Shell controls placement, animation, taskbar direction, overflow placement, and DPI

The tooltip is supplemental feedback. Brightness adjustment remains functional if tooltip popup
discovery is unavailable.

## Planned Files

- `src/modules/powerdisplay/PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs`
  - Add the immutable mode/value payload.
- `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs`
  - Add the localized template payload used by the pure formatter.
- `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs`
  - Add pure formatting and length limiting.
- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs`
  - Cover all text rules and defensive formatting.
- `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs`
  - Return feedback from applied planner results.
- `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs`
  - Coordinate adjustment and tooltip feedback.
- `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs`
  - Update/restore native tooltip and request immediate popup.
- `src/modules/powerdisplay/PowerDisplay/Strings/en-us/Resources.resw`
  - Add localized placeholder templates and comments.

No project file change is expected because these projects include C# files by default.

## Automated Test Plan

`TrayWheelFeedbackFormatterTests` covers:

- primary singular
- primary plural with two through four values
- all displays with one through four values
- primary/all range summary with five or more values
- exact-list order preservation
- minimum and maximum calculations
- 0% and 100%
- empty feedback returns null
- broken localized template falls back to the neutral template
- 127-code-unit output
- no dangling high surrogate after limiting

Existing planner, accumulator, registration-backoff, settings, and view-model tests remain green.

Build:

- PowerDisplay.Lib.UnitTests x64 Debug
- PowerDisplay x64 Debug
- PowerDisplay ARM64 Debug

## Runtime Verification

Use current-branch bits with persisted settings backups and exact cleanup:

1. Pinned icon: one wheel notch immediately shows the updated primary tooltip.
2. Overflow icon: one wheel notch immediately shows the updated tooltip.
3. Chevron-only input does not show adjustment feedback.
4. Primary mode shows one value and changes with every notch.
5. All mode shows exact values for the available displays.
6. High-resolution `+60/+60` shows feedback only when the complete notch is applied.
7. 0% and 100% show the current boundary value.
8. Rapid scrolling updates text and extends the restore deadline.
9. After two seconds, tooltip returns to `PowerDisplay`.
10. Hide/re-enable starts with `PowerDisplay`, not stale feedback.
11. Explorer restart recovery starts with `PowerDisplay`, then later wheel feedback works.
12. Existing left-click, context menu, wheel targeting, and registration recovery remain functional.

## Acceptance Criteria

1. A successful tray wheel adjustment updates the native tooltip with target scope and post-change
   percentage values.
2. The selected A wording and all count/range rules are exact.
3. Primary mirror mode uses `Primary displays`.
4. No-target input leaves the normal application tooltip.
5. Every valid adjustment restarts the two-second restore timer.
6. The normal application tooltip returns after two seconds, hide, registration loss, or destroy.
7. `NIM_MODIFY` never becomes a duplicate `NIM_ADD` path.
8. Tooltip delivery does not touch the hook callback or hardware path.
9. Formatting is localized, bounded to 127 UTF-16 code units, and unit-tested.
10. On supported Windows test environments, the updated text appears during the real wheel
    interaction for both pinned and overflow icons.
11. If criterion 10 cannot be met with native tooltip plus best-effort popup, implementation stops
    at the go/no-go gate and returns to design.
