# Input Highlighter (dev notes)

> Working design doc for merging the keystroke overlay (team4 `KeystrokeOverlay`) into
> the existing **Mouse Highlighter** module to form a single utility, **Input Highlighter**.
> Status: in progress. See the session plan for phase/todo tracking.

## Summary

Input Highlighter is the existing Mouse Highlighter module extended to also visualize
keyboard input. It keeps Mouse Highlighter's internal identity for a frictionless upgrade
(see *Identity & migration*) and only changes the user-facing display name.

All rendering is native: **C++ + Windows.UI.Composition + Direct2D/DirectWrite**, in a
single in-process module. team4's *capture* and *display logic* are reused (ported to
C++); team4's WinUI UI, named-pipe IPC, and separate-process design are dropped.

## Identity & migration (Option A)

- Internal module key/GUID stays `MouseHighlighter`; DLL stays
  `PowerToys.MouseHighlighter.dll`. Runner (`src/runner/main.cpp` known-DLL list), the GPO
  key (`getConfiguredMouseHighlighterEnabledValue`), and the installer component are
  therefore unchanged.
- Only display strings become "Input Highlighter" (`Resources.resw`, dashboard, OOBE,
  `ModuleHelper`).
- Settings migrate in place via `MouseHighlighterSettings.UpgradeSettingsConfiguration()`
  (`ISettingsConfig`) with a `Version` bump — same pattern as `ColorPickerSettings`.
  New keystroke fields get defaults; existing mouse settings + enabled state are
  preserved. Migrated users default to **mouse-only** (`show_keystrokes = false`); fresh
  installs enable both.

## Architecture

Single native DLL, evolved from `src/modules/MouseUtils/MouseHighlighter`.

```
        WH_MOUSE_LL hook ─┐                         (existing, unchanged)
                          ├─► Highlighter (Composition ShapeVisual) ─► ripples/spotlight
   WH_KEYBOARD_LL hook ─┐ │
        (new)           │ │
                        ▼ │
     in-process SPSC queue │        (ported EventQueue.h — no named pipe)
                        │  │
                        ▼  ▼
     KeystrokeProcessor (pure C++)   ── display-mode state machine
                        │
                        ▼
     Keystroke renderer (Composition + D2D/DirectWrite key "pills")
```

### Threading
- The LL keyboard hook callback stays lean: format nothing, allocate nothing — just push
  a POD `KeystrokeEvent` onto a lock-free SPSC ring (ported from team4 `EventQueue.h`).
- The Composition/dispatcher thread (the module already owns a `DispatcherQueue` — see
  `Highlighter::CreateHighlighter`) drains the queue, runs `KeystrokeProcessor`, and
  updates the visual tree.

### Rendering keystroke "pills" in Composition
Mouse Highlighter already builds a `Compositor` + `DesktopWindowTarget` + `ContainerVisual`
root with a `ShapeVisual` (`MouseHighlighter.cpp: CreateHighlighter`). Text/glyph content
is added as follows:
- Create a `CompositionGraphicsDevice` from the compositor
  (`ICompositorInterop::CreateGraphicsDevice`) backed by a D3D/D2D device.
- Each key pill = a `SpriteVisual` whose brush is a `CompositionSurfaceBrush` over a
  `CompositionDrawingSurface`. Draw with Direct2D: rounded-rect chrome + text/glyph via
  DirectWrite (**Segoe Fluent Icons** for glyphs, matching team4's KeyVisual).
- Pills are arranged in a horizontal container; newest at one end. Per-position opacity
  fade + expiry animations via Composition (`ScalarKeyFrameAnimation`), mirroring team4's
  opacity ordering and `TimeoutMs` removal.

### Overlay behavior
- Reuse Mouse Highlighter's layered, click-through, no-activate overlay window
  (`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_LAYERED`). This fixes team4's WinUI
  click-interception problem for free.
- Positioning: corner + per-monitor (settings), with a monitor-switch hotkey. Dragging
  is an **opt-in "move mode"** hotkey that temporarily clears `WS_EX_TRANSPARENT`, rather
  than an always-draggable window (which would defeat click-through).

## Logic ported from team4 (reference: `origin/feature/kbhighlighter-team4`)

| team4 source | Ported to | Notes |
|---|---|---|
| `KeyboardService/KeyboardListener.cpp` | native capture in-module | `WH_KEYBOARD_LL`, `ToUnicodeEx`, modifier snapshot. Drop the pipe/batcher; push to in-process queue. |
| `KeyboardService/EventQueue.h` | in-process queue | lock-free SPSC ring; keep as-is. |
| `Controls/KeystrokeEvent.cs` | `KeystrokeFormatter` (C++) | `ToString()` (display string), `IsShortcut`, `IsCommandKey`, `GetKeyName`, `GetModifierSymbol`. Pure + unit-testable. |
| `Services/KeystrokeProcessor.cs` | `KeystrokeProcessor` (C++) | `Add`/`ReplaceLast`/`RemoveLast`/`None` + stream buffer/backspace. Pure + unit-testable. |
| `Controls/KeyVisual/KeyVisual.xaml.cs` | D2D glyph map | Segoe Fluent Icons: Enter `\uE751`, Back `\uE750`, Shift `\uE752`, arrows `\uE0E2..\uE0E5`. |
| `Models/Enums/DisplayMode.cs` | `DisplayMode` enum | Last5 / SingleCharactersOnly / ShortcutsOnly / Stream. |

## Settings (extends the `MouseHighlighter` blob)
Existing mouse props unchanged. New (namespaced `keystroke_*`) props:
`show_mouse`, `show_keystrokes`, `keystroke_display_mode`, `keystroke_timeout_ms`,
`keystroke_text_size`, `keystroke_text_color`, `keystroke_background_color`,
`keystroke_text_opacity`, `keystroke_bg_opacity`, `keystroke_position`,
`keystroke_switch_monitor_hotkey`, `keystroke_switch_display_mode_hotkey`.

Touched files: `MouseHighlighterProperties.cs`, `MouseHighlighterSettings.cs` (+Version,
+`UpgradeSettingsConfiguration`), `SndMouseHighlighterSettings.cs`,
`MouseHighlighterSettingsIPCMessage.cs`, `SettingsSerializationContext`, native
`MouseHighlighter.h` settings struct + `dllmain.cpp` parse/apply.

## Testing
- Unit tests for `KeystrokeFormatter` (glyph/shortcut/char formatting) and
  `KeystrokeProcessor` (mode transitions, stream backspace, expiry ordering) — factor the
  pure logic so it builds without Win32.
- **Fuzz test** for keystroke input parsing/formatting (required for a new user-input
  surface per repo guidelines).

## Open risks
- Direct2D/DirectWrite text quality vs. the XAML KeyVisual look — **de-risk with a spike
  before full parity** (render one pill on the existing overlay window).
- Segoe Fluent Icons availability/fallback across supported OS versions.
- Keeping the keyboard-hook hot path allocation-free while marshaling to the Composition
  thread.
