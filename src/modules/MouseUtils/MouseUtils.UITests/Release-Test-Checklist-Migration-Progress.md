## Mouse Utils

The migrated suite is [MouseUtils.UITests.Next](../MouseUtils.UITests.Next/) and currently contains 65 test methods. Checked items have an automated effect-based assertion. CursorBrake items that need two monitors say so; they report Inconclusive on the single-monitor Hyper-V profile and run on a guest with several virtual displays. Unchecked items require physical hardware or desktop state that neither profile can establish faithfully.

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

### Mouse Button Lock

- [x] The Mouse Button Lock settings group is visible; disabling the module disables its "Buttons and behavior" options and re-enabling restores them. The module has no separate worker process, window, or named event, so the enabled/disabled effect is verified by whether holding a configured button past its hold duration actually locks it.
- [x] Left, right, and middle button lock checkboxes change and persist independently, including across a restart.
- [x] Hold duration (200-2200 ms) and move-cancel distance (0-100 px) boundary values persist, including across a restart.
- [x] Holding an enabled button past its configured hold duration locks it until a same-button tap releases it; releasing before the hold duration elapses, or holding a disabled button, never locks.

Automated by [MouseButtonLockSettingsTests.cs](../MouseUtils.UITests.Next/MouseButtonLockSettingsTests.cs).

### CursorBrake

- [x] The CursorBrake settings group is visible; disabling the module disables its "Resistance and behavior" options and re-enabling restores them.
- [x] Strength boundary values (0, 300 and 1000 px) persist, including across a restart.
- [x] Free return and Disable resistance while dragging change and persist independently, including across a restart.
- [x] Every Apply to and Cross freely while value persists, including across a restart.
- [x] With the module on, marked moves within one monitor land where they were sent, so a single monitor is a no-op. The module has no worker process, window, or named event, so the test waits for the module log's "hook installed" line before moving and fails on a hook-install failure.
- [x] Marked moves into an inner edge are held on the leaving monitor's last pixel until the push past the edge reaches Strength, and the move that reaches it crosses. Each move adds at most 10 logical px, so a hard push crosses on move Strength / 10 at any scale. The crossing cases take an edge from the live monitor layout and report Inconclusive on one monitor.
- [x] Holding the cross key crosses on the first move into an inner edge, where the same move without it is held, for both Cross freely while values (Ctrl and Shift). Inconclusive on one monitor.
- [x] Right after a crossing, the move straight back crosses without resistance, at once and after a wander 10 px short of free return's reach: 100 logical px from the crossed edge, scaled by the DPI of the monitor crossed onto. After a wander 2 px past the reach, the way back brakes again. Over an edge between monitors of different DPI, the case checks both ways, so a reach scaled by the monitor the cursor crossed from, or not scaled at all, fails. Inconclusive on one monitor.
- [x] The push needed scales by the DPI of the monitor the cursor leaves, checked both ways over an edge between monitors of different DPI. Inconclusive where every edge joins monitors of one DPI.
- [x] The "Edges between monitors" editor follows the module toggle.
- [x] On one monitor, the editor shows the "connect another monitor" state and no edge map. Inconclusive on more than one monitor.
- [x] One-way entries for monitors that are not connected show under "Disconnected monitors" and persist unchanged across a restart.
- [x] Forget removes a disconnected pair's entries from settings.json and its row from the editor.
- [x] A Wall chosen for an edge survives the saves that follow a Strength change, the other edge's Default shows the new Strength, and the module applies the settings a bounded number of times rather than in a save loop. Inconclusive on one monitor.
- [x] An edge box whose list opened reports the new "Default (N px)" text to UI Automation after Strength changes. Inconclusive on one monitor.
- [x] Every "Full-screen apps and games" value persists, and the last one across a restart. The Wall item names the chosen cross key, before and after the restart.
- [x] Excluded apps persist, including across a restart, and reach the module, which logs that it took them.
- [x] Disabling the module disables "Resistance and behavior", which holds the full-screen choice, and Excluded apps; re-enabling restores both, with the full-screen choice and the excluded apps text box enabled.
- [ ] Dragging with "Disable resistance while dragging" on crosses an inner edge without resistance; Apply to leaves the other axis's edges free; an edge's Wall, Off and one-way values hold, free and brake one direction only; and display changes rebuild the layout. Manual: requires changing displays during the run; the drag bypass, Apply to and per-edge effects have no automated case.

Automated by [CursorBrakeSettingsTests.cs](../MouseUtils.UITests.Next/CursorBrakeSettingsTests.cs).