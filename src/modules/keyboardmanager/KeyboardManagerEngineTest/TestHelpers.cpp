#include "pch.h"
#include "TestHelpers.h"
#include "MockedInput.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>

namespace TestHelpers
{
    // Function to reset the environment variables for tests
    void ResetTestEnv(KeyboardManagerInput::MockedInput& input, State& state)
    {
        input.ResetKeyboardState();
        input.SetHookProc(nullptr);
        input.SetSendVirtualInputTestHandler(nullptr);
        input.SetSendVirtualInputShouldFail(nullptr);
        input.SetForegroundProcess(L"");
        state.ClearSingleKeyRemaps();
        state.ClearOSLevelShortcuts();
        state.ClearAppSpecificShortcuts();
        state.ClearSingleKeyToTextRemaps();
        state.ClearTextReplacements();
        state.textReplacementBuffer.clear();
        state.textReplacementProcessId = 0;
        state.textReplacementWindow = nullptr;
        state.textReplacementPendingPacketHighSurrogate = L'\0';
        state.textReplacementDeadKeyPending = false;
        state.textReplacementDeadKeyMustPassThrough = false;
        state.textReplacementDeadKeyThreadId = 0;
        state.textReplacementDeadKeyLayout = nullptr;
        state.textReplacementCapsLockOn = false;
        state.textReplacementNumLockOn = false;
        state.textReplacementToggleStateInitialized = false;
        state.textReplacementObservedContextEpoch = 0;
        state.textReplacementContextEpoch.store(1, std::memory_order_relaxed);
        state.textReplacementContextTrackingEnabled.store(false, std::memory_order_relaxed);
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Pending, std::memory_order_relaxed);
        state.textReplacementContextWindow.store(nullptr, std::memory_order_relaxed);
        state.textReplacementContextProcessId.store(0, std::memory_order_relaxed);
        state.textReplacementClassifiedContextEpoch.store(0, std::memory_order_relaxed);
        state.textReplacementContextRefreshEvent.store(nullptr, std::memory_order_relaxed);

        // Allocate memory for the keyboardManagerState activatedApp member to avoid CRT assert errors
        std::wstring maxLengthString;
        maxLengthString.resize(MAX_PATH);
        state.SetActivatedApp(maxLengthString);
        state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
    }
}
