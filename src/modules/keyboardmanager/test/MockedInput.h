#pragma once
#include <keyboardmanager/dll/InputInterface.h>
#include <vector>
#include <functional>
#include <interface/lowlevel_keyboard_event_data.h>
#include <keyboardmanager/common/KeyboardManagerState.h>

class MockedInput :
    public InputInterface
{
private:
    // false for key up, and true for key down
    std::vector<bool> keyboardState;
    std::function<intptr_t(LowlevelKeyboardEvent*)> hookProc;

public:
    MockedInput()
    {
        keyboardState.reserve(256);
        ResetKeyboardState();
    }

    void SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure)
    {
        hookProc = hookProcedure;
    }

    UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize);

    intptr_t MockedKeyboardHook(LowlevelKeyboardEvent* data)
    {
        return hookProc(data);
    }

    bool GetVirtualKeyState(DWORD key)
    {
        return keyboardState[key];
    }

    void ResetKeyboardState()
    {
        std::fill(keyboardState.begin(), keyboardState.end(), false);
    }
};
