#pragma once

// Starts the dedicated UI thread that owns all overlay windows and the Switcher
// state machine. Must be called from the PowerToys module enable() path.
// Safe to call multiple times (idempotent).
bool InitializeAltWindowCycle(HINSTANCE hinst);

// Stops the UI thread and destroys all overlay resources. Safe to call when not
// initialized (idempotent). Called from disable() and destroy().
void ShutdownAltWindowCycle();

// Called from on_hotkey() on the runner thread. Posts to the UI thread without
// enumerating windows. `holdModifiers` is an AltWindowCycleLogic modifier mask
// that controls which modifier release commits the visible cycle.
bool HandleAltWindowCycleHotkey(bool forward, unsigned int holdModifiers);

// Posts an Escape cancellation request when the overlay is active. Returns true
// only when Escape should be swallowed by the centralized keyboard hook.
bool HandleAltWindowCycleCancel();

// Instant (no-overlay) cycle helper, kept for internal use.
void CycleForegroundAppWindows(bool forward);
