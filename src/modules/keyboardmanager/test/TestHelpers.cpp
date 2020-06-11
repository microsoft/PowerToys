#include "pch.h"
#include "TestHelpers.h"

namespace TestHelpers
{
    // Function to reset the environment variables for tests
    void ResetTestEnv(MockedInput& input, KeyboardManagerState& state)
    {
        input.ResetKeyboardState();
        input.SetHookProc(nullptr);
        state.ClearSingleKeyRemaps();
        state.ClearOSLevelShortcuts();
    }
}
