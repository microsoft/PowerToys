# Always On Top – System Menu Injection & Handling

## Overview
Adds “Always on top” to a window’s system menu and keeps the menu state in sync with the module’s pin/unpin logic and hotkeys.

## Algorithm
1. **Event triggers**  
   - On startup.  
   - On `EVENT_SYSTEM_FOREGROUND` and `EVENT_SYSTEM_MENUPOPUPSTART`.
2. **EnsureSystemMenuForWindow(hwnd)**  
   - Filter: top-level, visible, has `WS_SYSMENU`, not `WS_CHILD`, not system/PowerToys/excluded.  
   - If item missing: insert separator (only if previous item isn’t one) and add menu item ID `0x1000` before `SC_CLOSE`; call `DrawMenuBar`.  
   - Always update the checkmark via `CheckMenuItem` based on `IsTopmost`.
3. **Click handling** (`EVENT_OBJECT_INVOKED` / `EVENT_OBJECT_COMMAND`, child ID `0x1000`)  
   - Resolve actionable window: `GW_OWNER` → `GA_ROOTOWNER` → `GA_ROOT` → foreground fallback.  
   - If the resolved window lacks our item, attempt foreground-root fallback.  
   - Toggle with `ProcessCommandWithSource(hwnd, "systemmenu")`, then refresh the menu item state.
4. **Hotkeys / LLKH events**  
   - Use the same toggle path via `ProcessCommandWithSource(hwnd, "hotkey"/"llkh")`, ensuring menu checkmark stays aligned with hotkey toggles.

## Key IDs & resources
- Menu command ID: `0x1000` (outside `SC_*` range).
- Label: `System_Menu_Always_On_Top` in `Resources.resx` (generated to `resource.h`).

## Logging (for diagnostics)
- Source-tagged toggles: `[AOT] ProcessCommand source=<hotkey|llkh|systemmenu> hwnd=...`
- Target resolution chain: `GW_OWNER / GA_ROOTOWNER / GA_ROOT` plus foreground fallback.
- Injection: insertions, “already present”, and `GetMenuItemCount` failures (with `GetLastError`).
- Clicks: `System menu click captured (event=..., src=..., target=...)`.

## Edge cases handled
- Menu popup HWND without `WS_SYSMENU`: we climb to owner/root and optionally foreground to find the real system menu.
- Duplicate separators avoided by checking the previous item.
- Foreground elevated windows still blocked by existing UIPI limits; we log skips accordingly.

## How to test quickly
1. Start Always On Top, open a normal Win32 app, open its system menu, click “Always on top”; check that the window pins and the menu item shows a checkmark.  
2. Use the hotkey to toggle the same window; ensure the menu checkmark follows.  
3. Check `AppData\Local\Microsoft\PowerToys\AlwaysOnTop\Logs\...` for the trace lines above if something is off.
