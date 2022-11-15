#include "pch.h"
#include "MockedInput.h"

using namespace KeyboardManagerInput;

// Set the keyboard hook procedure to be tested
void MockedInput::SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure)
{
    hookProc = hookProcedure;
}

// Function to simulate keyboard input - arguments and return value based on SendInput function (https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput)
UINT MockedInput::SendVirtualInput(UINT cInputs, LPINPUT pInputs, int /*cbSize*/)
{
    // Iterate over inputs
    for (UINT i = 0; i < cInputs; i++)
    {
        LowlevelKeyboardEvent keyEvent;

        // Distinguish between key and sys key by checking if the key is either F10 (for syskeydown) or if the key message is sent while Alt is held down. SYSKEY messages are also sent if there is no window in focus, but that has not been mocked since it would require many changes. More details on key messages at https://learn.microsoft.com/windows/win32/inputdev/wm-syskeydown
        if (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP)
        {
            if (keyboardState[VK_MENU] == true)
            {
                keyEvent.wParam = WM_SYSKEYUP;
            }
            else
            {
                keyEvent.wParam = WM_KEYUP;
            }
        }
        else
        {
            if (pInputs[i].ki.wVk == VK_F10 || keyboardState[VK_MENU] == true)
            {
                keyEvent.wParam = WM_SYSKEYDOWN;
            }
            else
            {
                keyEvent.wParam = WM_KEYDOWN;
            }
        }
        KBDLLHOOKSTRUCT lParam = {};

        // Set only vkCode and dwExtraInfo since other values are unused
        lParam.vkCode = pInputs[i].ki.wVk;
        lParam.dwExtraInfo = pInputs[i].ki.dwExtraInfo;
        keyEvent.lParam = &lParam;

        // If the SendVirtualInput call condition is true, increment the count. If no condition is set then always increment the count
        if (sendVirtualInputCallCondition == nullptr || sendVirtualInputCallCondition(&keyEvent))
        {
            sendVirtualInputCallCount++;
        }

        // Call low level hook handler
        intptr_t result = MockedKeyboardHook(&keyEvent);

        // Set keyboard state if the hook does not suppress the input
        if (result == 0)
        {
            // If key up flag is set, then set keyboard state to false
            keyboardState[pInputs[i].ki.wVk] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;

            // Handling modifier key codes
            switch (pInputs[i].ki.wVk)
            {
            case VK_CONTROL:
                if (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP)
                {
                    keyboardState[VK_LCONTROL] = false;
                    keyboardState[VK_RCONTROL] = false;
                }
                break;
            case VK_LCONTROL:
                keyboardState[VK_CONTROL] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_RCONTROL:
                keyboardState[VK_CONTROL] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_MENU:
                if (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP)
                {
                    keyboardState[VK_LMENU] = false;
                    keyboardState[VK_RMENU] = false;
                }
                break;
            case VK_LMENU:
                keyboardState[VK_MENU] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_RMENU:
                keyboardState[VK_MENU] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_SHIFT:
                if (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP)
                {
                    keyboardState[VK_LSHIFT] = false;
                    keyboardState[VK_RSHIFT] = false;
                }
                break;
            case VK_LSHIFT:
                keyboardState[VK_SHIFT] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_RSHIFT:
                keyboardState[VK_SHIFT] = (pInputs[i].ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            }
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

// Function to set SendVirtualInput call count condition
void MockedInput::SetSendVirtualInputTestHandler(std::function<bool(LowlevelKeyboardEvent*)> condition)
{
    sendVirtualInputCallCount = 0;
    sendVirtualInputCallCondition = condition;
}

// Function to get SendVirtualInput call count
int MockedInput::GetSendVirtualInputCallCount()
{
    return sendVirtualInputCallCount;
}

// Function to get the foreground process name
void MockedInput::SetForegroundProcess(std::wstring process)
{
    currentProcess = process;
}

// Function to get the foreground process name
void MockedInput::GetForegroundProcess(_Out_ std::wstring& foregroundProcess)
{
    foregroundProcess = currentProcess;
}
