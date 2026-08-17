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

        // Optional predicate; when set and it returns true for a SendVirtualInput
        // call, that call fails (returns false) to simulate a SendInput failure.
        std::function<bool(const std::vector<INPUT>&)> sendVirtualInputShouldFail;

        // Optional prefix count returned by the simulated SendInput call. This models
        // the documented partial-success case precisely.
        std::function<size_t(const std::vector<INPUT>&)> sendVirtualInputInjectedCount;

        std::vector<std::vector<INPUT>> sentInputBatches;
        std::wstring injectedUnicodeText;

        std::wstring currentProcess;

    public:
        MockedInput()
        {
            keyboardState.resize(256, false);
        }

        // Set the keyboard hook procedure to be tested
        void SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure);

        // Function to simulate keyboard input
        SendVirtualInputResult SendVirtualInput(const std::vector<INPUT>& inputs) override;

        // Function to simulate keyboard hook behavior
        intptr_t MockedKeyboardHook(LowlevelKeyboardEvent* data);

        // Function to get the state of a particular key
        bool GetVirtualKeyState(int key);

        // Function to set the state of a particular key for test setup
        void SetKeyboardState(int key, bool state);

        // Function to reset the mocked keyboard state
        void ResetKeyboardState();

        // Function to set SendVirtualInput call count condition
        void SetSendVirtualInputTestHandler(std::function<bool(LowlevelKeyboardEvent*)> condition);

        // Function to force SendVirtualInput to fail for calls matching a predicate
        void SetSendVirtualInputShouldFail(std::function<bool(const std::vector<INPUT>&)> condition);

        // Function to force SendVirtualInput to deliver only a prefix of a batch.
        void SetSendVirtualInputInjectedCount(std::function<size_t(const std::vector<INPUT>&)> countProvider);

        // Function to get SendVirtualInput call count
        int GetSendVirtualInputCallCount();

        const std::vector<std::vector<INPUT>>& GetSentInputBatches() const;
        const std::wstring& GetInjectedUnicodeText() const;

        // Function to get the foreground process name
        void SetForegroundProcess(std::wstring process);

        // Function to get the foreground process name
        void GetForegroundProcess(_Out_ std::wstring& foregroundProcess);
    };
}

