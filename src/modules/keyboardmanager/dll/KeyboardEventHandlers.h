#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/InputInterface.h>

namespace KeyboardEventHandlers
{
    // Function to a handle a single key remap
    __declspec(dllexport) intptr_t HandleSingleKeyRemapEvent(InputInterface& ii, LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to a change a key's behavior from toggle to modifier
    __declspec(dllexport) intptr_t HandleSingleKeyToggleToModEvent(InputInterface& ii, LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to a handle a shortcut remap
    __declspec(dllexport) intptr_t HandleShortcutRemapEvent(InputInterface& ii, LowlevelKeyboardEvent* data, std::map<Shortcut, RemapShortcut>& reMap, std::mutex& map_mutex) noexcept;

    // Function to a handle an os-level shortcut remap
    __declspec(dllexport) intptr_t HandleOSLevelShortcutRemapEvent(InputInterface& ii, LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to a handle an app-specific shortcut remap
    __declspec(dllexport) intptr_t HandleAppSpecificShortcutRemapEvent(InputInterface& ii, LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;

    // Function to ensure Num Lock state does not change when it is suppressed by the low level hook
    void SetNumLockToPreviousState(InputInterface& ii);

    // Function to ensure Ctrl/Shift/Alt modifier key state is not detected as pressed down by applications which detect keys at a lower level than hooks when it is remapped
    void ResetIfModifierKeyForLowerLevelKeyHandlers(InputInterface& ii, DWORD key);
};
