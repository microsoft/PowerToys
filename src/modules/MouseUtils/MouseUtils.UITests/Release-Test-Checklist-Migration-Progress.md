## Mouse Utils

The migrated suite is [MouseUtils.UITests.Next](../MouseUtils.UITests.Next/) and currently contains 40 tests. Checked items have an automated effect-based assertion. Unchecked items require physical hardware or desktop state that the single-monitor Hyper-V profile cannot establish faithfully.

### Find My Mouse

- [x] Double-tap Left Ctrl and verify the overlay appears.
- [x] Press another key and verify the overlay disappears.
- [x] Activate again, press a mouse button, and verify the overlay disappears.
- [x] Disable the module and verify Left Ctrl no longer activates it.
- [x] Re-enable the module and verify activation works again.
- [ ] With "Do not activate on game mode" enabled, verify activation is blocked by an exclusive/native fullscreen game. Manual: requires a real D3D fullscreen foreground fixture.
- [ ] With "Do not activate on game mode" disabled, verify activation works over the same game. Manual: same fixture requirement.
- [x] Background and spotlight colors, including their current ARGB alpha values. This replaces the stale standalone "Overlay opacity" row.
- [x] Spotlight radius.
- [x] Spotlight initial zoom.
- [x] Animation duration.
- [ ] Shake activation. Manual: synthetic cursor movement is not equivalent to physical raw mouse input for the shake detector.
- [x] Excluded apps block activation only while an excluded process owns foreground.
- [x] Right Ctrl activation.
- [x] Custom shortcut activation.
- [x] Requiring the Windows key gates double-Ctrl activation.

Automated by [FindMyMouseTests.cs](../MouseUtils.UITests.Next/FindMyMouseTests.cs).

### Mouse Highlighter

- [x] Toggle the module with its activation shortcut and verify left/right click highlights.
- [x] Verify left-button and right-button highlights follow their respective drags.
- [x] Toggle the overlay off and verify click highlights disappear.
- [x] Disable the module and verify its shortcut cannot activate it.
- [x] Changed activation shortcut.
- [x] Left and right highlight colors, including their current ARGB alpha values. This replaces the stale standalone "Opacity" row.
- [x] Highlight radius.
- [x] Fade delay.
- [x] Fade duration.
- [x] Spotlight mode color, radius, and cursor tracking.
- [x] Ripple size, intensity, and duration.
- [x] Ripple drag-trail enabled and disabled behavior.
- [x] Ripple right-button release pulse.
- [x] Auto-activate.

Automated by [MouseHighlighterTests.cs](../MouseUtils.UITests.Next/MouseHighlighterTests.cs).

### Mouse Pointer Crosshairs

- [x] Activate Crosshairs and verify the overlay appears and tracks the cursor.
- [x] Activate again and verify the overlay disappears.
- [x] Disable the module and verify its shortcut cannot activate it.
- [x] Changed activation shortcut.
- [x] Crosshairs color.
- [x] Crosshairs opacity, validated against the blended desktop pixel color.
- [x] Center radius.
- [x] Thickness.
- [x] Border color.
- [x] Border size.
- [x] Horizontal orientation and fixed length.
- [x] Auto-activate.
- [x] Gliding Cursor activation, movement, and Escape cancellation.
- [ ] Auto-hide while the cursor is hidden. Manual: `ShowCursor` is thread-local and cannot create a valid cross-thread hidden-cursor fixture in the test host.

Automated by [MousePointerCrosshairsTests.cs](../MouseUtils.UITests.Next/MousePointerCrosshairsTests.cs).

### Mouse Jump

- [x] Load the settings page and verify the WinUI3 process, named event, and hidden preview HWND are ready.
- [x] Press the default activation shortcut and verify the preview appears.
- [x] Change the activation shortcut and verify only the new shortcut shows the preview.
- [x] Click the preview midpoint and verify the cursor maps to the primary display midpoint.
- [x] Disable the module and verify the shortcut neither shows the preview nor restarts the process.
- [x] Verify the preview dismisses when it loses focus.
- [x] Verify configured thumbnail bounds preserve the display aspect ratio.
- [x] Verify custom background, border, and bezel colors render.
- [ ] Reorder displays and verify the preview topology and click mapping. Manual: requires a multi-monitor VM/hardware profile.
- [ ] Change per-display scaling and verify preview topology and click mapping. Manual: requires mixed-DPI displays.
- [ ] Unplug and reconnect a display and verify live topology updates. Manual: requires hot-pluggable display hardware.

Automated by [MouseJumpTests.cs](../MouseUtils.UITests.Next/MouseJumpTests.cs).

### Cursor Wrap

- [x] Enable, activate, deactivate, and disable Cursor Wrap; verify wrapping follows module state.
- [x] Both mode wraps left, right, top, and bottom edges.
- [x] Horizontal-only mode wraps only left and right edges.
- [x] Vertical-only mode wraps only top and bottom edges.
- [x] Ctrl activation mode wraps only while Ctrl is held.
- [x] Shift activation mode wraps only while Shift is held.
- [x] "Disable during drag" blocks wrapping while the left button is held.
- [x] "Disable on single monitor" blocks all edges in the one-monitor profile.
- [x] Auto-activate starts wrapping without the toggle shortcut.
- [x] A changed activation shortcut replaces the default shortcut.
- [ ] Validate outer-edge polygons, adjacent inner edges, gaps, negative coordinates, and mixed DPI on multiple displays. Manual: requires representative multi-monitor layouts; use `CursorWrap/CursorWrapTests` for captured-layout simulation alongside hardware validation.

Automated by [CursorWrapTests.cs](../MouseUtils.UITests.Next/CursorWrapTests.cs).