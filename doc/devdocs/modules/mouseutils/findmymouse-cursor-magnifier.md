# Find My Mouse Cursor Magnifier Overlay

## Summary

Add a cursor magnifier overlay that draws a scaled copy of the current system cursor while Find My Mouse is active. This provides a "larger cursor" effect without changing the system cursor scheme or managing cursor assets.

## Goals

- Show a visibly larger cursor during the Find My Mouse spotlight effect.
- Avoid global cursor changes and asset maintenance.
- Keep the real system cursor visible and unmodified.

## Non-goals

- Changing the system cursor size globally.
- Replacing cursor assets or updating cursor registry schemes.
- Providing a configurable scale (initial implementation uses a fixed scale).

## Proposed Solution

Create a lightweight, topmost, layered overlay window that:

- Polls the current cursor (`GetCursorInfo`, `GetIconInfo`).
- Renders a scaled cursor into a 32-bit DIB via `DrawIconEx`.
- Composites the result using `UpdateLayeredWindow`.
- Runs at ~60 Hz while the Find My Mouse effect is active.

This overlay is independent of the spotlight window and does not interfere with the system cursor itself.

## Integration Points

- Initialize the overlay in `FindMyMouseMain` after the sonar window is created.
- Show/hide the overlay from `SuperSonar::StartSonar` and `SuperSonar::StopSonar`.
- Terminate the overlay when the module is disabled.

## Implementation Notes

- The overlay window uses `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`.
- The scaled cursor position uses the current cursor hotspot multiplied by the scale factor.
- If the cursor is hidden, the overlay hides itself as well.
- The overlay allocates a DIB buffer sized to the scaled cursor and reuses it until the size changes.

## Risks and Considerations

- Performance: 60 Hz rendering can be expensive on low-end machines; consider throttling or caching if needed.
- DPI/scale: the overlay operates in screen pixels; verify on mixed-DPI setups.
- Z-order: topmost layered window should stay above most content, but might need adjustment if other topmost overlays are present.

## Future Enhancements

- Expose cursor scale as a setting.
- Cache rendered cursor bitmaps per `HCURSOR` to reduce per-frame work.
- Consider a composition-based drawing path for smoother integration with existing visuals.
