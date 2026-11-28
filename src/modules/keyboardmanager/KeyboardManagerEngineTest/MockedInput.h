#pragma once
#include <keyboardmanager/common/InputInterface.h>
#include <deque>
#include <vector>
#include <functional>

#include <common/hooks/LowlevelKeyboardEvent.h>

namespace KeyboardManagerInput
{
    // A key event that reached the mocked system, i.e. one the hook did not suppress
    struct DeliveredKeyEvent
    {
        DWORD vkCode;
        bool isKeyUp;
        ULONG_PTR dwExtraInfo;
        DWORD scanCode;
    };

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

        std::wstring currentProcess;

        // When true, SendVirtualInput calls made from inside the hook are put in a queue and go
        // through the hook after the event being handled, the way SendInput behaves. When false
        // (the default) they go through the hook at once, nested inside the caller.
        bool queuedDelivery = false;
        bool deliveringQueue = false;
        std::deque<INPUT> queuedInputs;

        // When true, the virtual key the hook sees carries the numpad-origin marker, the way
        // KeyboardManager::HookProc sets it from the extended-key flag.
        bool encodeNumpadOrigin = false;

        // The time stamp the hook sees on an event that does not bring its own (KEYBDINPUT.time is
        // 0), the way the system stamps such an event with its tick count
        DWORD eventTime = 0;

        // The events that reached the system (the hook returned 0), in order
        std::vector<DeliveredKeyEvent> deliveredEvents;

        // Sends one event through the hook and, if it is not suppressed, to the keyboard state
        void DeliverInput(const INPUT& input);

    public:
        MockedInput()
        {
            keyboardState.resize(256, false);
        }

        // Set the keyboard hook procedure to be tested
        void SetHookProc(std::function<intptr_t(LowlevelKeyboardEvent*)> hookProcedure);

        // Function to simulate keyboard input
        bool SendVirtualInput(const std::vector<INPUT>& inputs);

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

        // Function to get SendVirtualInput call count
        int GetSendVirtualInputCallCount();

        // Function to choose between queued and nested delivery of input sent from inside the hook
        void SetQueuedDelivery(bool queued);

        // Function to make the hook see numpad-origin encoded virtual keys, as the real hook procedure does
        void SetEncodeNumpadOrigin(bool encode);

        // Function to set the time stamp (in milliseconds, as KBDLLHOOKSTRUCT.time) of the events sent from now on
        void SetEventTime(DWORD time);

        // Function to get the events that reached the system (were not suppressed by the hook), in order
        const std::vector<DeliveredKeyEvent>& GetDeliveredEvents() const;

        // Function to forget the events that reached the system so far
        void ClearDeliveredEvents();

        // Function to get the foreground process name
        void SetForegroundProcess(std::wstring process);

        // Function to get the foreground process name
        void GetForegroundProcess(_Out_ std::wstring& foregroundProcess);

        // Function to set what GetFilterKeysRepeatMs returns (0: Filter Keys off)
        void SetFilterKeysRepeatMs(DWORD milliseconds);

        // Function to get the repeat time set by SetFilterKeysRepeatMs
        DWORD GetFilterKeysRepeatMs();

    private:
        DWORD filterKeysRepeatMs = 0;
    };
}

