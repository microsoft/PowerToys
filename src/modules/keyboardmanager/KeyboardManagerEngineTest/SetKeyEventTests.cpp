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
            std::vector<INPUT> inputs;

            // List of extended keys
            WORD keyCodes[nInputs] = { VK_RCONTROL, VK_RMENU, VK_NUMLOCK, VK_SNAPSHOT, VK_CANCEL, VK_INSERT, VK_HOME, VK_PRIOR, VK_DELETE, VK_END, VK_NEXT, VK_LEFT, VK_DOWN, VK_RIGHT, VK_UP };

            for (int i = 0; i < nInputs; i++)
            {
                // Set key events for all the extended keys
                Helpers::SetKeyEvent(inputs, INPUT_KEYBOARD, keyCodes[i], 0, 0);
                // Extended key flag should be set
                Assert::AreEqual(true, bool(inputs[i].ki.dwFlags & KEYEVENTF_EXTENDEDKEY));
            }
        }

        // Test if SetKeyEvent sets the scan code field to 0 for dummy key
        TEST_METHOD (SetKeyEvent_ShouldSetScanCodeFieldTo0_WhenArgumentIsDummyKey)
        {
            std::vector<INPUT> inputs{};

            Helpers::SetDummyKeyEvent(inputs, 0);

            // Assert that wScan for both inputs is 0
            Assert::AreEqual<unsigned int>(0, inputs[0].ki.wScan);
            Assert::AreEqual<unsigned int>(0, inputs[1].ki.wScan);
        }
    };

    // Tests for the SetTextKeyEvents method
    TEST_CLASS (SetTextKeyEventsTests)
    {
    public:
        // Test that plain ASCII text produces KEYEVENTF_UNICODE events with correct scan codes
        TEST_METHOD (SetTextKeyEvents_ShouldUseUnicodeFlag_WhenTextIsPlainAscii)
        {
            std::vector<INPUT> inputs;
            std::wstring text = L"abc";

            Helpers::SetTextKeyEvents(inputs, text);

            // 3 characters × 2 events (down+up) = 6 events
            Assert::AreEqual<size_t>(6, inputs.size());
            for (size_t i = 0; i < inputs.size(); i++)
            {
                Assert::AreEqual(true, bool(inputs[i].ki.dwFlags & KEYEVENTF_UNICODE));
            }
            Assert::AreEqual<unsigned short>(L'a', inputs[0].ki.wScan);
            Assert::AreEqual<unsigned short>(L'b', inputs[2].ki.wScan);
            Assert::AreEqual<unsigned short>(L'c', inputs[4].ki.wScan);
        }

        // Test that each character generates a keydown and keyup event pair
        TEST_METHOD (SetTextKeyEvents_ShouldGenerateDownUpPairs_WhenTextHasMultipleChars)
        {
            std::vector<INPUT> inputs;
            std::wstring text = L"xy";

            Helpers::SetTextKeyEvents(inputs, text);

            Assert::AreEqual<size_t>(4, inputs.size());
            // First event: 'x' keydown (no KEYEVENTF_KEYUP flag)
            Assert::AreEqual<unsigned short>(L'x', inputs[0].ki.wScan);
            Assert::IsFalse(bool(inputs[0].ki.dwFlags & KEYEVENTF_KEYUP));
            // Second event: 'x' keyup
            Assert::AreEqual<unsigned short>(L'x', inputs[1].ki.wScan);
            Assert::AreEqual(true, bool(inputs[1].ki.dwFlags & KEYEVENTF_KEYUP));
        }

        // Test that newline characters are passed through as Unicode events (actual newline handling is done via clipboard)
        TEST_METHOD (SetTextKeyEvents_ShouldPassNewlinesAsUnicode_WhenTextContainsNewlines)
        {
            std::vector<INPUT> inputs;
            std::wstring text = L"a\r\nb";

            Helpers::SetTextKeyEvents(inputs, text);

            // All 4 characters (a, \r, \n, b) × 2 events = 8 events
            Assert::AreEqual<size_t>(8, inputs.size());
            Assert::AreEqual<unsigned short>(L'a', inputs[0].ki.wScan);
            Assert::AreEqual<unsigned short>(L'\r', inputs[2].ki.wScan);
            Assert::AreEqual<unsigned short>(L'\n', inputs[4].ki.wScan);
            Assert::AreEqual<unsigned short>(L'b', inputs[6].ki.wScan);
        }

        // Test empty string produces no events
        TEST_METHOD (SetTextKeyEvents_ShouldProduceNoEvents_WhenTextIsEmpty)
        {
            std::vector<INPUT> inputs;
            std::wstring text = L"";

            Helpers::SetTextKeyEvents(inputs, text);

            Assert::AreEqual<size_t>(0, inputs.size());
        }

        // Test that extraInfo flag is set correctly for KBM identification
        TEST_METHOD (SetTextKeyEvents_ShouldSetExtraInfoFlag_WhenTextIsProvided)
        {
            std::vector<INPUT> inputs;
            std::wstring text = L"a";

            Helpers::SetTextKeyEvents(inputs, text);

            Assert::AreEqual<size_t>(2, inputs.size());
            Assert::AreEqual(KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, inputs[0].ki.dwExtraInfo);
            Assert::AreEqual(KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, inputs[1].ki.dwExtraInfo);
        }
    };
}
