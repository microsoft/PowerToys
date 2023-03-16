#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>
#include <keyboardmanager/common/KeyboardEventHandlers.h>
#include <keyboardmanager/common/Helpers.h>
#include "TestHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for the SetKeyEvent method
    TEST_CLASS (SetKeyEventTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

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
                Helpers::SetKeyEvent(input, i, INPUT_KEYBOARD, keyCodes[i], 0, 0);
                // Extended key flag should be set
                Assert::AreEqual(true, bool(input[i].ki.dwFlags & KEYEVENTF_EXTENDEDKEY));
            }
        }
        
        // Test if SetKeyEvent sets the scan code field to 0 for dummy key
        TEST_METHOD (SetKeyEvent_ShouldSetScanCodeFieldTo0_WhenArgumentIsDummyKey)
        {
            const int nInputs = KeyboardManagerConstants::DUMMY_KEY_EVENT_SIZE;
            INPUT input[nInputs] = {};

            int index = 0;
            Helpers::SetDummyKeyEvent(input, index, 0);

            // Assert that wScan for both inputs is 0
            Assert::AreEqual<unsigned int>(0, input[0].ki.wScan);
            Assert::AreEqual<unsigned int>(0, input[1].ki.wScan);
        }
    };
}
