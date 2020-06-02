#include "pch.h"
#include "MockedInput.h"

UINT MockedInput::SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize)
{
    // Iterate over inputs
    for (UINT i = 0; i < cInputs; i++)
    {
        LowlevelKeyboardEvent keyEvent;
        // Limited to key-up and key-down since KBM does not distinguish between key and syskey messages
        keyEvent.wParam = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? WM_KEYUP : WM_KEYDOWN;
        KBDLLHOOKSTRUCT lParam;
        // Set only vkCode and dwExtraInfo since other values are unused
        lParam.vkCode = pInputs[i].ki.wVk;
        lParam.dwExtraInfo = pInputs[i].ki.dwExtraInfo;
        keyEvent.lParam = &lParam;
        // Call low level hook handler
        intptr_t result = MockedKeyboardHook(&keyEvent);
        // Set keyboard state if the hook does not suppress the input
        if (result == 0)
        {
            // If key up flag is set, then set state to false
            keyboardState[pInputs[i].ki.wVk] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
        }
    }
    return 1;
}
