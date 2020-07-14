#pragma once
class MockedInput;
class KeyboardManagerState;

namespace TestHelpers
{
    // Function to reset the environment variables for tests
    void ResetTestEnv(MockedInput& input, KeyboardManagerState& state);
}
