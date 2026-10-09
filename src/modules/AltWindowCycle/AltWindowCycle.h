#pragma once

// Starts the dedicated UI thread that owns all overlay windows and the Switcher
// state machine. Must be called from the PowerToys module enable() path.
// Safe to call multiple times (idempotent).
bool InitializeAltWindowCycle(HINSTANCE hinst);

// Stops the UI thread and destroys all overlay resources. Safe to call when not
// initialized (idempotent).
//
// The runner drives enable()/disable() on the same thread that services the
// centralized low-level keyboard hook, so disable() must not stall it: pass
// `blockUntilExit = false` there and let the UI thread finish on its own. Pass
// `blockUntilExit = true` only from destroy(), where the runner unloads the DLL
// afterwards and the thread must be gone first.
void ShutdownAltWindowCycle(bool blockUntilExit);

// Called from on_hotkey() on the runner thread. Posts to the UI thread without
// enumerating windows. `holdModifiers` is an AltWindowCycleLogic modifier mask
// that controls which modifier release commits the visible cycle.
bool HandleAltWindowCycleHotkey(bool forward, unsigned int holdModifiers);
