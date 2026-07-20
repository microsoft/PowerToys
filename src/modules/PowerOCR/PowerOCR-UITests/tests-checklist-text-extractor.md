## Text Extractor
 * Enable Text Extractor. Then:
   - [x] Press the activation shortcut and verify the overlay appears.
   - [x] Press Escape and verify the overlay disappears.
   - [x] Press the activation shortcut and verify the overlay appears.
   - [x] Right-click and select Cancel. Verify the overlay disappears.
   - [x] Disable Text Extractor and verify that the activation shortcut no longer activates the utility.
 * With Text Extractor enabled and activated:
   - [x] Try to select text and verify it is copied to the clipboard.
   - [x] Try to select a different OCR language by right-clicking and verify the change is applied.
   - [x] Toggle Single-line mode via `SingleLineToggleButton`; verify the button reports Selected = true.
   - [x] Toggle Table mode via `TableToggleButton`; verify the button reports Selected = true.
   - [x] Press Escape after toggling toolbar modes and verify the overlay is dismissed.
 * Rendering – WinUI theming:
   - [ ] Switch Windows to Light theme; activate the overlay and verify the toolbar and canvas render correctly.
   - [ ] Switch Windows to Dark theme; activate the overlay and verify the toolbar and canvas render correctly.
   - [ ] Switch Windows to High Contrast (Black or White); activate the overlay and verify all controls are legible and accessible.
 * Toolbar / flyout accessibility:
   - [ ] Confirm that `SingleLineToggleButton`, `TableToggleButton`, `SettingsButton`, and `CancelButton` each have a non-empty accessible name (Name property) readable by Narrator.
   - [ ] Tab through toolbar controls and confirm each is reachable by keyboard without a mouse.
   - [ ] Open the language flyout via keyboard (right-click key or Shift+F10) and confirm language items are accessible.
 * Capture modes (manual, each requires text on screen):
   - [ ] Region capture: drag to select a region containing text; verify the correct text is on the clipboard.
   - [ ] Single-line mode: activate Single-line, click a single line of text; verify one line is on the clipboard.
   - [ ] Table mode: activate Table mode, select a tabular region; verify tab-separated values are on the clipboard.
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