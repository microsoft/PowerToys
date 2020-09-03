#pragma once
class MockedInput;
class KeyboardManagerState;

namespace TestHelpers
{
    // Function to reset the environment variables for tests
    void ResetTestEnv(MockedInput& input, KeyboardManagerState& state);

    // Function to return the index of the given key code from the drop down key list
    int GetDropDownIndexFromDropDownList(DWORD key, const std::vector<DWORD>& keyList);
}
