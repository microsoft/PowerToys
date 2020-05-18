#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

namespace KeyboardEventHandlers
{
    // Function to set the value of a key event based on the arguments
    void SetKeyEvent(LPINPUT keyEventArray, int index, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo);

    // Function to a handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(LowlevelKeyboardEvent* data, KeyboardManagerState& keyboardManagerState) noexcept;
};
