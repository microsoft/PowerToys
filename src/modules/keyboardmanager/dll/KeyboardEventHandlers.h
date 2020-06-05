#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

namespace KeyboardEventHandlers
{
    // Function to a handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to a change a key's behavior from toggle to modifier
    intptr_t HandleSingleKeyToggleToModEvent(LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to a handle a shortcut remap
    intptr_t HandleShortcutRemapEvent(LowlevelKeyboardEvent* data, std::map<Shortcut, RemapShortcut>& reMap, std::mutex& map_mutex) noexcept;

    // Function to a handle an os-level shortcut remap
    intptr_t HandleOSLevelShortcutRemapEvent(LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to a handle an app-specific shortcut remap
    intptr_t HandleAppSpecificShortcutRemapEvent(LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to ensure Num Lock state does not change when it is suppressed by the low level hook
    void SetNumLockToPreviousState();
};
