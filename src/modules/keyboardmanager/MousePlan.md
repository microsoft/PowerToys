# Implementation Plan: Mouse Click Support for Keyboard Manager

## Overview

This plan takes an incremental approach, starting with the simplest case (**Mouse → Single Key**) and expanding from there. Each phase builds on the previous, allowing you to validate the core infrastructure before adding complexity.

### Scope Summary

**Buttons Supported:**
- Left click
- Middle click
- Right click
- X1 (Back)
- X2 (Forward)

**Remapping Combinations:**

| Source (Left Side) | Target (Right Side) |
|--------------------|---------------------|
| Mouse Button | Key (single) |
| Mouse Button | Shortcut (multiple keys) |
| Mouse Button | Mouse Button |
| Mouse Button | Text |
| Mouse Button | Open Program |
| Key (single) | Mouse Button |

**Not Included in MVP:**
- Mouse + modifier combinations (Ctrl+Click, etc.)
- Double clicks or long presses
- Scroll wheel
- Shortcut → Mouse (ambiguous hold behavior)

**Behavior on Hold:**
- Mouse source → fires once (mouse doesn't auto-repeat)
- Key source → repeats clicks (keyboard auto-repeats) — *confirm with team*

---

## Phase 1: Mouse → Single Key (Foundation)

**Goal**: X1 button press → types the letter "A"

This phase establishes the mouse hook infrastructure and proves the event pipeline works.

### 1.1 Define Mouse Button Enum
**File**: `common/Helpers.h` (or new `MouseButton.h`)

```cpp
enum class MouseButton : DWORD {
    Left = 0,
    Right = 1,
    Middle = 2,
    X1 = 3,
    X2 = 4
};
```

Add helper functions:
- `MouseButtonFromWParam(WPARAM wParam, DWORD mouseData)` — Convert hook message to enum
- `GetMouseButtonName(MouseButton)` — For UI display

### 1.2 Add Mouse Remap Table
**File**: `common/MappingConfiguration.h`

```cpp
// Add new table alongside existing ones
using MouseToKeyRemapTable = std::unordered_map<MouseButton, KeyShortcutTextUnion>;

// Add member
MouseToKeyRemapTable mouseToKeyReMap;
```

**File**: `common/MappingConfiguration.cpp`

- Add JSON loading for new `remapMouseToKey` section
- Add `ClearMouseRemaps()` method
- Update `HasAnyRemappings()` to include mouse remaps

### 1.3 Add Mouse Hook
**File**: `KeyboardManagerEngineLibrary/KeyboardManager.h`

```cpp
// Add alongside keyboard hook
HHOOK mouseHook;
static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);
void StartMouseHook();
void StopMouseHook();
intptr_t HandleMouseHookEvent(WPARAM wParam, MSLLHOOKSTRUCT* data) noexcept;
```

**File**: `KeyboardManagerEngineLibrary/KeyboardManager.cpp`

Implement mouse hook installation (mirror keyboard hook pattern):
- Install with `SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, ...)`
- Check for editor running, loading settings (same guards as keyboard)
- Call `HandleMouseHookEvent` for `HC_ACTION`

### 1.4 Add Mouse Event Handler
**File**: `KeyboardManagerEngineLibrary/KeyboardEventHandlers.h`

```cpp
// Add new handler
intptr_t HandleMouseToKeyRemapEvent(
    State& state,
    InputInterface& ii,
    MouseButton button,
    bool isButtonDown);
```

**File**: `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp`

Implement handler:
- Look up button in `mouseToKeyReMap`
- If found and target is `DWORD` (single key):
  - Button down → `SendInput` key down
  - Button up → `SendInput` key up
- Return 1 to suppress original mouse event, 0 to pass through

### 1.5 Update Engine Startup
**File**: `KeyboardManagerEngine/main.cpp`

- Call `StartMouseHook()` if mouse remappings exist
- Call `StopMouseHook()` on shutdown

### 1.6 Extend State Class
**File**: `KeyboardManagerEngineLibrary/State.h`

```cpp
std::optional<KeyShortcutTextUnion> GetMouseButtonRemap(MouseButton button);
```

### 1.7 JSON Configuration Format
```json
{
    "remapMouseToKey": {
        "inProcess": [
            {
                "originalButton": "X1",
                "newRemapKeys": "65"
            }
        ]
    }
}
```

### 1.8 Validation Checkpoint
- [ ] X1 button press sends "A" key down
- [ ] X1 button release sends "A" key up
- [ ] Holding X1 keeps "A" held (no repeat — mouse doesn't repeat)
- [ ] Settings reload picks up new mappings
- [ ] Editor running suspends mouse remapping

---

## Phase 2: Mouse → Shortcut / Text / Program

**Goal**: Extend the mouse handler to support all existing target types.

### 2.1 Extend Handler for Shortcuts
**File**: `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp`

In `HandleMouseToKeyRemapEvent`:
```cpp
if (std::holds_alternative<Shortcut>(target)) {
    Shortcut& sc = std::get<Shortcut>(target);
    if (isButtonDown) {
        // Send modifier downs + action key down
    } else {
        // Send action key up + modifier ups
    }
}
```

Reuse existing `Shortcut::SendInput` helpers where possible.

### 2.2 Extend Handler for Text
```cpp
if (std::holds_alternative<std::wstring>(target)) {
    if (isButtonDown) {
        // Fire text once on button down (no repeat since mouse doesn't repeat)
        SendUnicodeString(std::get<std::wstring>(target));
    }
    // Button up does nothing for text
}
```

### 2.3 Extend Handler for Run Program
Check how `Shortcut::CreateRunProgramObject()` works and invoke the same logic on mouse button down.

### 2.4 JSON Format Extension
```json
{
    "remapMouseToKey": {
        "inProcess": [
            { "originalButton": "X1", "newRemapKeys": "162;67" },
            { "originalButton": "X2", "newRemapString": "Hello World" },
            { "originalButton": "Middle", "runProgram": "notepad.exe" }
        ]
    }
}
```

### 2.5 Validation Checkpoint
- [ ] X1 → Ctrl+C works (copies selected text)
- [ ] X2 → "Hello World" types text on click
- [ ] Middle → Opens notepad
- [ ] All fire once per click (no repeat on hold)

---

## Phase 3: Key → Mouse Click

**Goal**: F5 key press → Left click

### 3.1 Add Reverse Remap Table
**File**: `common/MappingConfiguration.h`

```cpp
using KeyToMouseRemapTable = std::unordered_map<DWORD, MouseButton>;
KeyToMouseRemapTable keyToMouseReMap;
```

### 3.2 Add Handler
**File**: `KeyboardManagerEngineLibrary/KeyboardEventHandlers.h`

```cpp
intptr_t HandleKeyToMouseRemapEvent(
    State& state,
    InputInterface& ii,
    LowlevelKeyboardEvent* data);
```

### 3.3 Integrate into Keyboard Pipeline
**File**: `KeyboardManagerEngineLibrary/KeyboardManager.cpp`

In `HandleKeyboardHookEvent`, add check **before** existing handlers:
```cpp
// Check key-to-mouse first
if (HandleKeyToMouseRemapEvent(state, ii, data)) {
    return 1; // Suppress keyboard event
}
// Then existing handlers...
```

### 3.4 Send Mouse Input
Use `SendInput` with `INPUT_MOUSE`:
```cpp
INPUT input = {};
input.type = INPUT_MOUSE;
input.mi.dwFlags = isKeyDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
// Handle X1/X2 with MOUSEEVENTF_XDOWN/XUP and mouseData = XBUTTON1/XBUTTON2
SendInput(1, &input, sizeof(INPUT));
```

### 3.5 Handle Keyboard Repeat
```cpp
// Keyboard auto-repeats, so holding a key = rapid clicks
// This matches existing behavior (key→text repeats text)
if (isKeyDown) {
    SendMouseDown(targetButton);
    SendMouseUp(targetButton);  // Complete click per repeat
}
```

Or, if you want hold = hold (drag behavior):
```cpp
if (data->flags & LLKHF_REPEAT) {
    return 1; // Suppress repeats, keep mouse held
}
```

**Discuss with team which behavior to use.**

### 3.6 Validation Checkpoint
- [ ] F5 → Left click works
- [ ] Holding F5 rapid-fires clicks (or holds, based on team decision)
- [ ] F5 release stops clicking

---

## Phase 4: Mouse → Mouse

**Goal**: X1 → Middle click

### 4.1 Add Table
```cpp
using MouseToMouseRemapTable = std::unordered_map<MouseButton, MouseButton>;
MouseToMouseRemapTable mouseToMouseReMap;
```

### 4.2 Update Mouse Handler
In `HandleMouseHookEvent`, check `mouseToMouseReMap` first:
```cpp
if (auto target = GetMouseToMouseRemap(button)) {
    SendMouseEvent(*target, isButtonDown);
    return 1;
}
```

### 4.3 Validation Checkpoint
- [ ] X1 → Middle click works
- [ ] Hold X1 = hold middle button (for drag)

---

## Phase 5: Editor UI

**Goal**: Users can configure mouse remaps in the UI.

### 5.1 Understand Editor Architecture
**Files to study**:
- `KeyboardManagerEditorLibrary/` — UI logic
- `KeyboardManagerEditor/` — XAML UI

### 5.2 Add Mouse Button Picker
Create a dropdown or button-capture control for mouse buttons (simpler than keyboard capture since there are only 5 options).

### 5.3 Extend Remap Tables UI
- Add "Remap Mouse" section alongside "Remap Keys" and "Remap Shortcuts"
- Reuse existing shortcut/key/text pickers for the target side

### 5.4 JSON Save/Load
Ensure editor writes the new JSON sections and engine reads them.

---

## Phase 6: Polish & Edge Cases

### 6.1 App-Specific Mouse Remaps
Extend `appSpecificShortcutReMap` pattern to mouse, reusing foreground detection.

### 6.2 Telemetry
**File**: `KeyboardManagerEngineLibrary/trace.h`

Add events for mouse remap usage.

### 6.3 GPO Support
If needed, add GPO policy for mouse remapping separate from keyboard.

### 6.4 Documentation
Update the KeyboardManagerEngine README with mouse-specific sections.

---

## File Change Summary

| Phase | Files to Modify | Files to Create |
|-------|-----------------|-----------------|
| 1 | `MappingConfiguration.h/cpp`, `KeyboardManager.h/cpp`, `KeyboardEventHandlers.h/cpp`, `State.h/cpp`, `main.cpp` | `MouseButton.h` (optional) |
| 2 | `KeyboardEventHandlers.cpp`, `MappingConfiguration.cpp` | — |
| 3 | `MappingConfiguration.h/cpp`, `KeyboardEventHandlers.h/cpp`, `KeyboardManager.cpp` | — |
| 4 | `MappingConfiguration.h/cpp`, `KeyboardEventHandlers.cpp` | — |
| 5 | `KeyboardManagerEditor*`, `KeyboardManagerEditorLibrary*` | New XAML controls |
| 6 | `trace.h/cpp`, `README.md`, GPO files | — |

---

## Questions to Resolve with Team

1. **Key → Mouse repeat behavior**: Rapid-fire clicks or hold-to-drag?
2. **App-specific mouse remaps**: Include in MVP or Phase 2?
3. **UI priority**: Ship engine-only first with JSON config, or wait for full UI?
4. **Naming**: "Mouse Button 4/5" vs "X1/X2" vs "Back/Forward" in UI?

---

## Suggested First PR

**Phase 1 only** — Mouse hook + Mouse → Single Key remap. This proves the architecture works without UI or complex target types. Can be tested via manual JSON editing.
