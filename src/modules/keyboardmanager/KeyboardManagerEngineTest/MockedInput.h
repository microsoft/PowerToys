#pragma once
#include <keyboardmanager/common/InputInterface.h>
#include <vector>
#include <functional>

#include <common/hooks/LowlevelKeyboardEvent.h>

namespace KeyboardManagerInput
{
    // Class for mocked keyboard input
    class MockedInput :
        public InputInterface
    {
    private:
        // Stores the states for all the keys - false for key up, and true for key down
        std::vector<bool> keyboardState;

        // Function to be executed as a low level hook. By default it is nullptr so the hook is skipped
        std::function<intptr_t(LowlevelKeyboardEvent*)> hookProc;

        // Stores the count of sendVirtualInput calls given if the condition sendVirtualInputCallCondition is satisfied
        int sendVirtualInputCallCount = 0;
        std::function<bool(LowlevelKeyboardEvent*)> sendVirtualInputCallCondition;

        std::wstring currentProcess;

    public:
        MockedInput()
        {
            keyboardState.resize(256, false);
        }

        // Set the keyboard hook procedure to be tested
        void SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure);

        // Function to simulate keyboard input
        void SendVirtualInput(const std::vector<INPUT>& inputs);

        // Function to simulate keyboard hook behavior
        intptr_t MockedKeyboardHook(LowlevelKeyboardEvent* data);

        // Function to get the state of a particular key
        bool GetVirtualKeyState(int key);

        // Function to reset the mocked keyboard state
        void ResetKeyboardState();

        // Function to set SendVirtualInput call count condition
        void SetSendVirtualInputTestHandler(std::function<bool(LowlevelKeyboardEvent*)> condition);

        // Function to get SendVirtualInput call count
        int GetSendVirtualInputCallCount();

        // Function to get the foreground process name
        void SetForegroundProcess(std::wstring process);

        // Function to get the foreground process name
        void GetForegroundProcess(_Out_ std::wstring& foregroundProcess);
    };
}

