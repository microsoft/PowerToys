#include "pch.h"
#include "TestHelpers.h"
#include "MockedInput.h"
#include "keyboardmanager/common/KeyboardManagerState.h"

namespace TestHelpers
{
    // Function to reset the environment variables for tests
    void ResetTestEnv(MockedInput& input, KeyboardManagerState& state)
    {
        input.ResetKeyboardState();
        input.SetHookProc(nullptr);
        input.SetSendVirtualInputTestHandler(nullptr);
        input.SetForegroundProcess(L"");
        state.ClearSingleKeyRemaps();
        state.ClearOSLevelShortcuts();
        state.ClearAppSpecificShortcuts();

        // Allocate memory for the keyboardManagerState activatedApp member to avoid CRT assert errors
        std::wstring maxLengthString;
        maxLengthString.resize(MAX_PATH);
        state.SetActivatedApp(maxLengthString);
        state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
    }
}
