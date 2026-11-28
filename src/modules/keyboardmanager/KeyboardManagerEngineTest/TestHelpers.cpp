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
        input.SetQueuedDelivery(false);
        input.SetEncodeNumpadOrigin(false);
        input.SetEventTime(0);
        input.SetFilterKeysRepeatMs(0);
        input.ClearDeliveredEvents();
        input.SetForegroundProcess(L"");
        state.ClearSingleKeyRemaps();
        state.ClearSingleKeyAloneRemaps();
        state.ClearSingleKeyToSequenceRemaps();
        state.ClearOSLevelShortcuts();
        state.ClearAppSpecificShortcuts();
        state.ClearSingleKeyToTextRemaps();

        // Make the (now empty) key-sequence table the live one, and forget the press records
        state.StageSequenceTable();
        state.PublishStagedSequenceTable();
        state.SequencePresses().Reset();

        // Allocate memory for the keyboardManagerState activatedApp member to avoid CRT assert errors
        std::wstring maxLengthString;
        maxLengthString.resize(MAX_PATH);
        state.SetActivatedApp(maxLengthString);
        state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
    }
}
