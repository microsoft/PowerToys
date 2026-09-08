## Text Extractor

 * Native selection surface:
   - [ ] Rapidly hover and drag over the selection surface; confirm the system cross cursor remains stable.
   - [ ] Verify repeated startup without white/black flashes, click/drag OCR, Shift translation, toolbar and popup input, and mixed-DPI alignment.
   - [ ] Verify Esc, Alt+F4, capture loss, and rapid reopen release capture/clipping and leave no native overlay behind.
   - [ ] Verify Esc/Alt+F4 from both the native surface and WinUI toolbar, plus successful OCR close, hide every display without exposing a white teardown frame. Repeat after an OCR error and with a popup recently dismissed.

 * Enable Text Extractor. Then:
   - [x] Press the activation shortcut and verify the overlay appears.
   - [x] Press Escape and verify the overlay disappears.
   - [x] Press the activation shortcut and verify the overlay appears.
   - [x] Right-click and select Cancel. Verify the overlay disappears.
   - [x] Disable Text Extractor and verify that the activation shortcut no longer activates the utility.
 * With Text Extractor enabled and activated:
   - [x] Try to select text and verify it is copied to the clipboard.
   - [ ] Click a single word or character without dragging and verify the recognized token is copied to the clipboard.
   - [ ] Select a blank region, wait for the error InfoBar, and verify one Escape press closes every overlay.
   - [ ] After a blank capture, press `S`, `T`, and a number for another installed OCR language without clicking the toolbar; verify all shortcuts still change their controls.
   - [ ] Repeat the shortcut check after a clipboard-copy failure, then retry capture in the same overlay and verify copying succeeds.
   - [ ] With a clipboard monitor/history tool running, repeat OCR capture; transient clipboard contention should recover without an error prompt or repeating the content write.
   - [ ] Press Escape while clipboard persistence is retrying; verify all overlays close and no later retry or error prompt appears from the cancelled session.
   - [ ] With an error InfoBar open, verify it appears below every toolbar control without covering any control, including at 150% and 200% DPI.
   - [ ] Open the language ComboBox or context menu and verify the first Escape closes only the popup and the second closes the overlay.
   - [ ] Perform a fast drag and verify OCR uses the release point rather than the last rendered selection-border position.
   - [ ] Cancel or close the overlay while dragging and verify the cursor is no longer confined afterward.
   - [ ] Leave the mouse still inside the selection surface for at least 1.5 seconds, then move it repeatedly without pressing a button; verify the cross cursor never flashes back to the underlying application's cursor during either the dwell or movement, and no automatic Escape tooltip appears over the selection surface.
   - [ ] Move from the selection surface onto every toolbar control and back; verify controls use their normal cursor and the surface returns to a stable cross cursor.
   - [ ] Open and dismiss the language ComboBox and context menu, then repeat the mouse-up sweep on the selection surface.
   - [ ] After a blank capture error, repeat the mouse-up sweep without dismissing the error InfoBar.
   - [ ] Close and reopen the overlay, then repeat the mouse-up sweep to check cursor initialization and cleanup.
   - [x] Try to select a different OCR language by right-clicking and verify the change is applied.
   - [ ] Toggle Single-line mode via `SingleLineToggleButton`; verify the button reports Selected = true.
   - [ ] Toggle Table mode via `TableToggleButton`; verify the button reports Selected = true.
   - [ ] Press Escape after toggling toolbar modes and verify the overlay is dismissed.
 * Rendering – WinUI theming:
   - [ ] Switch Windows to Light theme; activate the overlay and verify the toolbar and canvas render correctly.
   - [ ] Switch Windows to Dark theme; activate the overlay and verify the toolbar and canvas render correctly.
   - [ ] Switch Windows to High Contrast (Black or White); activate the overlay and verify all controls are legible and accessible.
 * Toolbar / flyout accessibility:
   - [ ] Find the full-overlay `TextExtractorWindow` automation peer, verify its bounds cover the monitor, and use those bounds for selection instead of a `RegionClickCanvas` locator.
   - [ ] Confirm that `SingleLineToggleButton`, `TableToggleButton`, `SettingsButton`, and `CancelButton` each have a non-empty accessible name (Name property) readable by Narrator.
   - [ ] Tab through toolbar controls and confirm each is reachable by keyboard without a mouse.
   - [ ] Open the language flyout via keyboard (right-click key or Shift+F10) and confirm language items are accessible.
 * Capture modes (manual, each requires text on screen):
   - [ ] Region capture: drag to select a region containing text; verify the correct text is on the clipboard.
   - [ ] Single-line mode: activate Single-line, click a single line of text; verify one line is on the clipboard.
   - [ ] Table mode: activate Table mode, select a tabular region; verify tab-separated values are on the clipboard.
   - [ ] Chinese/Japanese mode: capture text ending in punctuation (for example, `2026。`) and verify no extra space is inserted before it.
 * Multi-monitor / extended display:
   - [ ] Move the cursor to a monitor positioned to the **left** of the primary display and activate the overlay; verify the overlay covers that monitor and OCR succeeds.
   - [ ] Move the cursor to a monitor positioned **above** the primary display and activate the overlay; verify the overlay covers that monitor and OCR succeeds.
   - [ ] Verify text is correctly captured on all monitors (multiple DPI settings).
 * Mixed DPI:
   - [ ] Set monitors to 100% DPI; activate overlay and verify correct region selection and OCR result.
   - [ ] Set monitors to 150% DPI; activate overlay and verify correct region selection and OCR result.
   - [ ] Set monitors to 200% DPI; activate overlay and verify correct region selection and OCR result.
 * Activation paths:
   - [ ] Start PowerToys Runner normally; activate Text Extractor via `Win+Shift+T` and verify the overlay appears (Runner-managed activation).
   - [ ] Launch the Text Extractor executable directly (standalone mode); verify the overlay appears on activation.
 * Settings integration:
   - [ ] In PowerToys Settings, click the deep-link (Settings button / `SettingsButton`) inside the Text Extractor overlay; verify that the Settings page scrolls to or opens the Text Extractor section.
 * Settings page (automated):
   - [x] Activation shortcut
   - [x] OCR Language
