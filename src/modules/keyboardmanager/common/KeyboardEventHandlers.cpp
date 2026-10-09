#include "pch.h"
#include "KeyboardEventHandlers.h"
#include <keyboardmanager/common/InputInterface.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

namespace KeyboardEventHandlers
{
    // Function to ensure Num Lock state does not change when it is suppressed by the low level hook
    void SetNumLockToPreviousState(KeyboardManagerInput::InputInterface& ii)
    {
        // Num Lock's key state is applied before it is intercepted by low level keyboard hooks, so we have to manually set back the state when we suppress the key. This is done by sending an additional key up, key down set of messages.
        std::vector<INPUT> keyEventList;

        // Use the suppress flag to ensure these are not intercepted by any remapped keys or shortcuts
        Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, VK_NUMLOCK, KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG);
        Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, VK_NUMLOCK, 0, KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG);
        ii.SendVirtualInput(keyEventList);
    }
}
