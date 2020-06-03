#include "pch.h"
#include "MockedInput.h"

// Set the keyboard hook procedure to be tested
void MockedInput::SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure)
{
    hookProc = hookProcedure;
}

// Function to simulate keyboard input - arguments and return value based on SendInput function (https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
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
            // If key up flag is set, then set keyboard state to false
            keyboardState[pInputs[i].ki.wVk] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
        }
    }

    return cInputs;
}

// Function to simulate keyboard hook behavior
intptr_t MockedInput::MockedKeyboardHook(LowlevelKeyboardEvent* data)
{
    // If the hookProc is set to null, then skip the hook
    if (hookProc != nullptr)
    {
        return hookProc(data);
    }
    else
    {
        return 0;
    }
}

// Function to get the state of a particular key
bool MockedInput::GetVirtualKeyState(int key)
{
    return keyboardState[key];
}

// Function to reset the mocked keyboard state
void MockedInput::ResetKeyboardState()
{
    std::fill(keyboardState.begin(), keyboardState.end(), false);
}
