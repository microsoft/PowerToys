# PowerOCR WinUI 3 Migration Design

**Date:** 2026-07-20  
**Status:** Approved

## Context

PowerOCR (Text Extractor) is currently a WPF executable launched and controlled by
`PowerOCRModuleInterface`. The application creates one overlay per monitor, captures
screen content, runs Windows OCR, and copies the selected text to the clipboard.

The current implementation mixes UI concerns with OCR and table-formatting logic:

- `PowerOCR.csproj` enables WPF and Windows Forms.
- `App`, `OCROverlay`, event dispatch, and window management use `System.Windows`.
- Image conversion and scaling use WPF imaging types.
- Table analysis creates WPF `Canvas` and `Border` objects for geometry calculations.
- Localization uses `.resx` resources referenced directly from WPF XAML.

The migration will move the executable to WinUI 3 and split reusable OCR logic into a
UI-independent core project. Native WinUI 3 visuals may differ modestly from WPF, but
the existing feature behavior and external contracts must remain unchanged.

## Goals

- Replace the PowerOCR production UI with WinUI 3 and remove all WPF dependencies.
- Preserve the executable name, Runner integration, Named Events, settings contracts,
  hotkeys, telemetry, and user workflows.
- Preserve one overlay per display and correct behavior on displays with different DPI
  values or negative virtual-screen coordinates.
- Separate OCR, text formatting, table analysis, and geometry models from the UI.
- Add focused unit coverage for the extracted core logic.
- Integrate the migrated binaries and PRI resources with the existing WinUI3Apps build,
  signing, installer, and verification pipelines.

## Non-goals

- Changing PowerOCR settings schemas or Named Event names.
- Changing the activation shortcut or OCR language semantics.
- Migrating the existing UI tests to a different test framework.
- Redesigning Text Extractor beyond native WinUI 3 control and theme differences.
- Replacing the Windows OCR engine or GDI-based screen capture in this change.

## Approaches Considered

### 1. In-place UI migration

Convert the current executable to WinUI 3 while retaining the existing project
boundaries. This minimizes project churn but leaves OCR algorithms coupled to UI and
does not provide meaningful unit-test seams.

### 2. Core/UI split

Create a UI-independent `PowerOCR.Core` project and migrate the executable to WinUI 3.
This has a larger build and packaging surface, but removes the current WPF coupling,
improves testability, and establishes clear ownership between OCR logic and platform UI.

This is the selected approach.

### 3. Hybrid WinUI/WPF host

Host retained WPF components from a WinUI shell. This reduces initial conversion work
but keeps two XAML stacks, complicates deployment and DPI behavior, and does not meet
the goal of removing WPF.

## Project Structure

### `PowerOCR.Core`

`PowerOCR.Core` is a Windows-specific but UI-independent class library. It may use
Windows OCR and imaging APIs, but it must not reference WPF, WinUI, or visual controls.

Responsibilities:

- Invoke `Windows.Media.Ocr`.
- Convert OCR output into internal line, word, point, rectangle, and scale models.
- Apply language-specific word-spacing rules.
- Format normal, single-line, clicked-word, and table output.
- Perform table row and column analysis using pure rectangle intersection.
- Scale or pad source bitmaps as required by the OCR engine.

The assembly name will be `PowerToys.PowerOCR.Core`.

### `PowerOCR`

The existing executable project remains named `PowerOCR` and continues to produce
`PowerToys.PowerOCR.exe` and `PowerToys.PowerOCR.dll`. It becomes an unpackaged,
self-contained WinUI 3 application under the `WinUI3Apps` output directory.

Responsibilities:

- Process startup, GPO validation, single-instance enforcement, and Runner lifetime.
- Named Event and standalone keyboard-hook activation.
- Display enumeration, DPI discovery, screen capture, and window placement.
- Overlay session and multi-window coordination.
- WinUI image conversion and rendering.
- Clipboard access, settings monitoring, localization, telemetry, and error UI.

XAML will live under `PowerOCRXAML\` to prevent shared `WinUI3Apps` XBF name
collisions, following the repository convention used by other WinUI 3 modules.

### `PowerOCR.Core.UnitTests`

The new MSTest project will reference only `PowerOCR.Core` and will be added to
`PowerToys.slnx`.

### `PowerOCRModuleInterface`

The native module interface keeps its existing process and event contracts. Its launch
path changes to `WinUI3Apps\PowerToys.PowerOCR.exe`, matching other migrated WinUI 3
modules. It continues to pass the Runner PID and signal the existing show and terminate
events.

## Startup and Lifetime

`Program.cs` will provide the explicit `[STAThread]` entry point and:

1. Initialize WinRT COM wrappers.
2. Initialize logging.
3. Enforce the existing GPO policy.
4. Acquire the existing single-instance mutex.
5. Parse the optional Runner PID.
6. Register Runner-exit and terminate-event handling.
7. Start WinUI with a `DispatcherQueueSynchronizationContext`.

`App` will own the application-wide services and `OverlayManager`. Named Event callbacks
will marshal to the WinUI dispatcher. Standalone mode will continue to install the
existing global keyboard hook; Runner-managed mode will continue to rely on the show
event.

Each overlay session owns a cancellation token. Closing the session cancels in-flight
OCR, closes all overlay windows, and releases screenshots. Application-wide Named Event,
settings, and keyboard listeners remain active so another session can be launched.
Disabling the module, terminating the process, or losing the Runner additionally stops
those listeners and disposes keyboard and telemetry resources.

## Overlay Architecture

`OverlayManager` owns all windows and session-wide state:

- Selected OCR language.
- Normal, single-line, or table output mode.
- The current overlay collection.
- The session cancellation token.
- Capture and clipboard operations.

When activation is requested:

1. Reject the request if an overlay session is already active.
2. Enumerate displays with Windows App SDK display APIs.
3. Capture each display before any overlay becomes visible.
4. Create one `OCROverlay` window per display.
5. Move and resize each `AppWindow` to the display's physical-pixel bounds.
6. Configure each window as borderless, fixed-size, topmost, and absent from task
   switchers.
7. Show the overlays and make the appropriate overlay receive keyboard input.

Commands from any overlay are routed through `OverlayManager`, so Escape, language
selection, single-line mode, and table mode remain synchronized across all displays.

## Coordinate and Capture Model

Display bounds and captured bitmaps are stored in physical pixels. WinUI pointer
coordinates are reported in device-independent pixels and are converted with the
window's current DPI scale.

The conversion must support:

- 100%, 150%, and 200% DPI.
- Displays with different DPI values.
- Displays positioned left of or above the primary display.
- Single-click word capture and rectangular region capture.

Each overlay retains the bitmap captured before the overlay was shown. OCR operates on
a crop of that cached bitmap rather than recapturing the screen while the overlay is
visible. This avoids capturing the overlay itself and makes capture behavior independent
of overlay opacity or rendering timing.

## WinUI 3 User Interface

`OCROverlay` will derive from `WinUIEx.WindowEx`. The XAML will use native WinUI 3
controls and theme resources:

- `Image` for the captured display.
- `Canvas` for pointer interaction and the selection border.
- Four semi-transparent rectangles around the selection to replace WPF
  `CombinedGeometry`.
- `MenuFlyout` for language, formatting, settings, and cancel commands.
- `ComboBox`, `ToggleButton`, and `Button` for the top toolbar.
- `FontIcon` or `SymbolIcon` for command glyphs.

Existing AutomationIds, including `TextExtractorWindow` and `CancelMenuItem`, will be
preserved wherever they are part of current UI automation.

## Core API and Data Flow

The UI submits a request containing:

- A source bitmap or cropped bitmap.
- The selected OCR language.
- The capture mode.
- An optional clicked point in physical image coordinates.
- A cancellation token.

The core service:

1. Validates the request and OCR language.
2. Pads or scales the bitmap when required.
3. Converts it to a `SoftwareBitmap`.
4. Runs `OcrEngine`.
5. Converts WinRT OCR results into UI-independent models.
6. Applies the requested formatting mode.
7. Returns extracted text.

Table analysis will no longer instantiate visual elements. The existing grid-scan,
row/column grouping, outlier merging, and tab-delimited formatting logic will operate
only on numeric rectangles and collections.

## Error Handling

- No installed OCR language produces a localized user-facing message and leaves the
  overlay open.
- OCR failures are logged with context and leave the overlay open for retry.
- Clipboard failures are logged and leave the overlay open so extracted content is not
  silently lost.
- Failed display capture aborts that overlay session with a visible error rather than
  presenting an unusable transparent window.
- Dispatcher enqueue failures during shutdown fall back to deterministic process
  termination so the process and hooks cannot be orphaned.
- Cancellation is treated separately from product errors and does not emit failure
  telemetry.

Successful clipboard copy closes all overlays and emits the existing capture telemetry.
Cancel paths emit the existing cancellation telemetry.

## Localization

User-facing UI strings will move from `Properties\Resources.resx` to
`Strings\en-us\Resources.resw`.

- XAML strings use `x:Uid`.
- Programmatic strings use a view-independent `ResourceLoader`.
- The preferred application language continues to be loaded through the shared
  `LanguageHelper` and applied with
  `Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride`.
- The WinUI project uses `PowerToys.PowerOCR.pri` as its project PRI file name.

The obsolete installer component for `PowerToys.PowerOCR.resources.dll` will be removed;
PRI resources will be installed through the WinUI3Apps file-generation and hardlink
mechanism.

## Build, Signing, and Installer Integration

The WinUI project will follow the current repository baseline:

- `<UseWinUI>true</UseWinUI>`.
- Unpackaged Windows App SDK configuration.
- Self-contained Windows App SDK deployment.
- Explicit `Program.cs` entry point.
- `WinUI3Apps` output directory.
- AOT compatibility properties and no MEF-based runtime construction.
- x64 and ARM64 platforms.

Build and release integration will include:

- Add `PowerOCR.Core` and `PowerOCR.Core.UnitTests` to `PowerToys.slnx`.
- Move PowerOCR executable and managed-assembly signing entries to `WinUI3Apps\`.
- Add `WinUI3Apps\PowerToys.PowerOCR.Core.dll` to the signing manifest.
- Keep `PowerToys.PowerOCRModuleInterface.dll` in the install root.
- Update `PowerOCRModuleInterface` to launch
  `WinUI3Apps\PowerToys.PowerOCR.exe`.
- Move PowerOCR executable and managed DLL checks from the install-root verification
  list to the WinUI3Apps verification list.
- Add the core DLL to WinUI3Apps verification.
- Remove the old satellite-resource component from `Resources.wxs`.
- Rely on the existing WinUI3 file-component generator and `hardlinks.txt` installation
  mechanism for the executable, managed assemblies, PRI, and XBF files.

## Testing

### Core unit tests

`PowerOCR.Core.UnitTests` will cover:

- Latin word joining.
- Chinese and Japanese no-space joining.
- Right-to-left line handling.
- Single-line formatting.
- Clicked-word hit testing.
- Table row and column detection.
- Sparse and multi-row table formatting.
- Empty and minimal OCR results.
- Coordinate conversion with positive and negative display origins.
- 100%, 150%, and 200% scale factors.

### Existing UI tests

`PowerOCR-UITests` remains on its current harness and will be updated for WinUI control
types where required. It will continue to verify:

- Activation by shortcut.
- Escape cancellation.
- Context-menu cancellation.
- Disabled-module behavior.
- Shortcut settings.
- Language selection.
- Region capture and clipboard output.

### Manual sign-off

The Text Extractor checklist will include:

- Native WinUI light and dark themes.
- Toolbar and flyout accessibility.
- Multiple displays with different DPI values.
- Displays with negative virtual-screen coordinates.
- Single-click, region, single-line, and table capture.
- Runner-managed and standalone process modes.

## Acceptance Criteria

- No production PowerOCR source or project enables WPF or references `System.Windows`.
- `PowerToys.PowerOCR.exe` remains launch-compatible with the native module interface.
- Existing Named Events, GPO behavior, settings schema, telemetry, and AutomationIds
  remain compatible.
- All supported capture modes work on single- and multi-display systems.
- Core unit tests and existing PowerOCR UI tests pass.
- PowerOCR builds for x64 and ARM64.
- The installed build contains signed PowerOCR WinUI binaries, core DLL, PRI, and XBF
  files in the expected WinUI3Apps layout.
