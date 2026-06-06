#pragma once

// Starts the dedicated UI thread that owns all overlay windows and the Switcher
// state machine. Must be called from the PowerToys module enable() path.
// Safe to call multiple times (idempotent).
bool InitializeAltWindowCycle(HINSTANCE hinst);

// Stops the UI thread and destroys all overlay resources. Safe to call when not
// initialized (idempotent). Called from disable() and destroy().
void ShutdownAltWindowCycle();

// Called from on_hotkey() on the runner thread. Does a cheap window-count check
// and posts to the UI thread. `holdModifiers` is an AltWindowCycleLogic modifier
// mask that controls which modifier release commits the visible cycle.
// Returns false (do not swallow) if the focused app has fewer than 2 cycle
// candidates and the overlay is not already active.
bool HandleAltWindowCycleHotkey(bool forward, unsigned int holdModifiers);

// Instant (no-overlay) cycle helper, kept for internal use.
void CycleForegroundAppWindows(bool forward);
