# Macro Module — Design Spec

**Date:** 2026-04-26  
**Status:** Approved  
**Scope:** v1 — keyboard-focused (keystrokes, text insertion, delays, repeats)

---

## Overview

New standalone PowerToys module that lets users define and run multi-step keyboard macros. Targets two tiers:

- **Non-technical power users** — GUI flowchart editor + record mode
- **Power users / AHK refugees** — hand-edit raw JSON, same format the GUI produces

Macros trigger via hotkey or Command Palette. Optional per-app scoping.

---

## Architecture

```
MacroModuleInterface/     C++ DLL, PowerToys runner entry point (thin shell)
MacroEngine/              C# background service — trigger listening + execution
MacroEditor/              WinUI3 editor — flowchart UI, record mode, JSON view
MacroCommon/              C# shared library — schema models, serialization, IPC contracts
MacroCmdPalExtension/     cmdpal extension — lists and runs macros via Command Palette
```

**Startup flow:**
1. PowerToys runner loads `MacroModuleInterface.dll`
2. DLL spawns `MacroEngine.exe` as child process
3. Engine registers hotkeys, listens on named pipe for cmdpal trigger requests
4. Editor launched on-demand from PowerToys Settings UI

**IPC:** Named pipe between `MacroEngine` ↔ `MacroEditor` and `MacroEngine` ↔ `MacroCmdPalExtension`. Same pattern as Workspaces module.

**File storage:** `%APPDATA%\Microsoft\PowerToys\Macros\` — one JSON file per macro.

---

## Data Model

Each macro is one JSON file:

```json
{
  "id": "a3f1c2d4-0000-0000-0000-000000000000",
  "name": "Insert signature",
  "description": "Type email signature and move to next field",
  "hotkey": "Ctrl+Shift+S",
  "app_scope": "OUTLOOK.EXE",
  "steps": [
    { "type": "type_text", "text": "Regards,\nKavin" },
    { "type": "wait",      "ms": 100 },
    { "type": "press_key", "key": "Tab" },
    { "type": "repeat",    "count": 3, "steps": [
        { "type": "press_key", "key": "Down" }
    ]}
  ]
}
```

### v1 Step Types

| Type | Required Params | Purpose |
|------|----------------|---------|
| `press_key` | `key` (string) | Single key or combo, e.g. `Ctrl+C` |
| `type_text` | `text` (string) | Insert literal string via `SendInput` Unicode events |
| `wait` | `ms` (int) | Delay between steps |
| `repeat` | `count` (int), `steps` (array) | Loop a sub-sequence N times |

`app_scope`: optional process name (e.g. `notepad.exe`). Omit = global.  
`hotkey`: optional. Macros without a hotkey are cmdpal-only.

---

## Engine

`MacroEngine.exe` — C# background service.

### Trigger Listening

**Hotkeys:** Register via `RegisterHotKey` Win32 API. On match:
1. Check `app_scope` against foreground window process name
2. Execute if match (or if `app_scope` is null)

**cmdpal:** `MacroCmdPalExtension` sends macro `id` over named pipe → engine looks up file → executes.

### App Scope Check

```csharp
string foreground = GetForegroundProcessName(); // GetForegroundWindow + GetWindowThreadProcessId
if (macro.AppScope != null && !foreground.Equals(macro.AppScope, StringComparison.OrdinalIgnoreCase))
    return;
```

### Execution Pipeline

```
Load macro JSON → validate steps → foreach step → dispatch handler → next step
```

Step handlers use P/Invoke `SendInput`:
- `press_key` → build `INPUT[]` key-down + key-up events, call `SendInput`
- `type_text` → convert string to `INPUT[]` Unicode `KEYEVENTF_UNICODE` events, call `SendInput`
- `wait` → `await Task.Delay(ms)` (non-blocking)
- `repeat` → recursive dispatch on sub-steps N times

---

## Editor UI

`MacroEditor.exe` — WinUI3 app, launched from PowerToys Settings.

### Toolbar
Macro name field · Hotkey picker · App scope picker (dropdown of running processes + "All apps" option; refreshes on open) · **Visual / JSON** toggle · Save · Delete

### Visual View (Flowchart)

Vertical chain of step cards connected by arrows:

```
┌─────────────────────┐
│ ⌨ Press Key  Ctrl+C │  ← drag handle to reorder, click to edit inline
└────────┬────────────┘
         ↓
┌─────────────────────┐
│ ⏱ Wait     100ms    │
└────────┬────────────┘
         ↓
┌─────────────────────┐
│ T  Type Text  Hello │
└─────────────────────┘
        [+] Add step
```

- Click card → inline edit popover (change type, edit params)
- Drag handle → reorder steps
- `+` between cards → add step (type picker dropdown)
- Select multiple cards → wrap in `repeat` block

### Record Mode

1. User clicks **Record** → engine temporarily unregisters all macro hotkeys to prevent re-triggering during capture; editor minimizes, floating "Recording…" pill appears on screen
2. Editor installs `WH_KEYBOARD_LL` hook via P/Invoke to capture keystrokes + inter-keystroke timing
3. Conversion rules:
   - Key combos → `press_key`
   - Gaps ≥ 200ms → `wait` (rounded to nearest 50ms); gaps < 200ms dropped
   - Consecutive printable characters → merged into single `type_text`
4. User clicks **Stop** → hook removed, engine re-registers hotkeys, editor restores, captured steps populate flowchart
5. User reviews, edits, saves

### JSON View

- Monaco editor (already at `src/Monaco` in PowerToys)
- Editable raw JSON
- Validate schema on save; show inline error markers
- Toggle back to Visual syncs changes

---

## cmdpal Integration

`MacroCmdPalExtension` implements the cmdpal extension SDK.

- **Discovery:** Reads `%APPDATA%\Microsoft\PowerToys\Macros\*.json` on load; file-watches for changes
- **Running:** User selects macro → extension sends macro `id` over named pipe to engine → engine executes against previously-focused window (cmdpal has already dismissed)
- **Creating:** "New Macro" command → launches `MacroEditor.exe` with blank macro
- **Search:** Macro names + descriptions indexed by cmdpal's existing fuzzy search — no extra work

---

## Out of Scope (v1)

- Mouse clicks / mouse movement
- Conditionals / branching
- Variables / dynamic values
- App launch / window focus steps
- Cloud sync of macros
- Import from AutoHotkey

---

## Open Questions

- Conflict resolution: what happens when a macro hotkey conflicts with an existing KeyboardManager remap? Show warning at save time.
- UAC elevation: `SendInput` does not cross UAC boundaries. Document this limitation; do not attempt workaround in v1.
