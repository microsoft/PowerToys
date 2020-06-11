#pragma once
#include "MockedInput.h"
#include <keyboardmanager/common/KeyboardManagerState.h>

namespace TestHelpers
{
    // Function to reset the environment variables for tests
    void ResetTestEnv(MockedInput& input, KeyboardManagerState& state);
}
