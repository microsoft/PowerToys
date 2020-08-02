#include "pch.h"
#include "CppUnitTest.h"
#include "MockedInput.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/dll/KeyboardEventHandlers.h>
#include <keyboardmanager/common/Helpers.h>
#include "TestHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for the SetKeyEvent method
    TEST_CLASS (SetKeyEventTests)
    {
    private:
        MockedInput mockedInputHandler;
        KeyboardManagerState testState;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);
        }

        // Test if SetKeyEvent sets the extended key flag for all the extended keys
        TEST_METHOD (SetKeyEvent_ShouldUseExtendedKeyFlag_WhenArgumentIsExtendedKey)
        {
            const int nInputs = 15;
            INPUT input[nInputs] = {};

            // List of extended keys
            WORD keyCodes[nInputs] = { VK_RCONTROL, VK_RMENU, VK_NUMLOCK, VK_SNAPSHOT, VK_CANCEL, VK_INSERT, VK_HOME, VK_PRIOR, VK_DELETE, VK_END, VK_NEXT, VK_LEFT, VK_DOWN, VK_RIGHT, VK_UP };

            for (int i = 0; i < nInputs; i++)
            {
                // Set key events for all the extended keys
                KeyboardManagerHelper::SetKeyEvent(input, i, INPUT_KEYBOARD, keyCodes[i], 0, 0);
                // Extended key flag should be set
                Assert::AreEqual(true, bool(input[i].ki.dwFlags & KEYEVENTF_EXTENDEDKEY));
            }
        }
    };
}
