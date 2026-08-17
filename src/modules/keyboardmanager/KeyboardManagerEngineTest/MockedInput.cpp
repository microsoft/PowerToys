#include "pch.h"
#include "MockedInput.h"

using namespace KeyboardManagerInput;

// Set the keyboard hook procedure to be tested
void MockedInput::SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure)
{
    hookProc = hookProcedure;
}

// Function to simulate keyboard input - arguments and return value based on SendInput function (https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput)
SendVirtualInputResult MockedInput::SendVirtualInput(const std::vector<INPUT>& inputs)
{
    sentInputBatches.push_back(inputs);

    if (inputs.empty())
    {
        return { SendVirtualInputStatus::Complete, 0 };
    }

    // Simulate an injection failure (e.g. SendInput blocked) when configured.
    if (sendVirtualInputShouldFail != nullptr && sendVirtualInputShouldFail(inputs))
    {
        return { SendVirtualInputStatus::None, 0 };
    }

    const size_t injectedEventCount = sendVirtualInputInjectedCount == nullptr ?
                                          inputs.size() :
                                          (std::min)(sendVirtualInputInjectedCount(inputs), inputs.size());

    // Iterate over inputs
    for (size_t inputIndex = 0; inputIndex < injectedEventCount; ++inputIndex)
    {
        const INPUT& input = inputs[inputIndex];
        LowlevelKeyboardEvent keyEvent{};

        // Distinguish between key and sys key by checking if the key is either F10 (for syskeydown) or if the key message is sent while Alt is held down. SYSKEY messages are also sent if there is no window in focus, but that has not been mocked since it would require many changes. More details on key messages at https://learn.microsoft.com/windows/win32/inputdev/wm-syskeydown
        if (input.ki.dwFlags & KEYEVENTF_KEYUP)
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
            if (input.ki.wVk == VK_F10 || keyboardState[VK_MENU] == true)
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
        lParam.vkCode = input.ki.wVk;
        lParam.dwExtraInfo = input.ki.dwExtraInfo;
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
            if (input.type == INPUT_KEYBOARD &&
                (input.ki.dwFlags & KEYEVENTF_UNICODE) != 0 &&
                (input.ki.dwFlags & KEYEVENTF_KEYUP) == 0)
            {
                injectedUnicodeText.push_back(static_cast<wchar_t>(input.ki.wScan));
            }

            // If key up flag is set, then set keyboard state to false
            keyboardState[input.ki.wVk] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;

            // Handling modifier key codes
            switch (input.ki.wVk)
            {
            case VK_CONTROL:
                if (input.ki.dwFlags & KEYEVENTF_KEYUP)
                {
                    keyboardState[VK_LCONTROL] = false;
                    keyboardState[VK_RCONTROL] = false;
                }
                break;
            case VK_LCONTROL:
                keyboardState[VK_CONTROL] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_RCONTROL:
                keyboardState[VK_CONTROL] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_MENU:
                if (input.ki.dwFlags & KEYEVENTF_KEYUP)
                {
                    keyboardState[VK_LMENU] = false;
                    keyboardState[VK_RMENU] = false;
                }
                break;
            case VK_LMENU:
                keyboardState[VK_MENU] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_RMENU:
                keyboardState[VK_MENU] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_SHIFT:
                if (input.ki.dwFlags & KEYEVENTF_KEYUP)
                {
                    keyboardState[VK_LSHIFT] = false;
                    keyboardState[VK_RSHIFT] = false;
                }
                break;
            case VK_LSHIFT:
                keyboardState[VK_SHIFT] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            case VK_RSHIFT:
                keyboardState[VK_SHIFT] = (input.ki.dwFlags & KEYEVENTF_KEYUP) ? false : true;
                break;
            }
        }
    }

    if (injectedEventCount == 0)
    {
        return { SendVirtualInputStatus::None, 0 };
    }

    return {
        injectedEventCount == inputs.size() ? SendVirtualInputStatus::Complete : SendVirtualInputStatus::Partial,
        static_cast<UINT>(injectedEventCount),
    };
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

// Function to set the state of a particular key for test setup
void MockedInput::SetKeyboardState(int key, bool state)
{
    keyboardState[key] = state;
}

// Function to reset the mocked keyboard state
void MockedInput::ResetKeyboardState()
{
    std::fill(keyboardState.begin(), keyboardState.end(), false);
    sentInputBatches.clear();
    injectedUnicodeText.clear();
}

// Function to set SendVirtualInput call count condition
void MockedInput::SetSendVirtualInputTestHandler(std::function<bool(LowlevelKeyboardEvent*)> condition)
{
    sendVirtualInputCallCount = 0;
    sendVirtualInputCallCondition = condition;
}

// Function to force SendVirtualInput to fail for calls matching a predicate
void MockedInput::SetSendVirtualInputShouldFail(std::function<bool(const std::vector<INPUT>&)> condition)
{
    sendVirtualInputShouldFail = condition;
}

void MockedInput::SetSendVirtualInputInjectedCount(std::function<size_t(const std::vector<INPUT>&)> countProvider)
{
    sendVirtualInputInjectedCount = countProvider;
}

// Function to get SendVirtualInput call count
int MockedInput::GetSendVirtualInputCallCount()
{
    return sendVirtualInputCallCount;
}

const std::vector<std::vector<INPUT>>& MockedInput::GetSentInputBatches() const
{
    return sentInputBatches;
}

const std::wstring& MockedInput::GetInjectedUnicodeText() const
{
    return injectedUnicodeText;
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
