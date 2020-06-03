#include "pch.h"
#include "CppUnitTest.h"
#include "MockedInput.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/dll/KeyboardEventHandlers.h>
#include "TestHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

MockedInput mockedInputHandler;
KeyboardManagerState testState;

namespace KeyboardManagerRemapLogicTest
{
    TEST_CLASS (MockedInputSanityTests)
    {
    public:
        // Test if mocked input is working
        TEST_METHOD (MockedInput_ShouldSetKeyboardState_OnKeyEvent)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Send key down and key up for A key (0x41) and check keyboard state both times)
            INPUT input;
            input.type = INPUT_KEYBOARD;
            input.ki.wVk = 0x41;

            // Send A keydown
            mockedInputHandler.SendVirtualInput(1, &input, sizeof(INPUT));

            // A key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), true);
            input.ki.dwFlags = KEYEVENTF_KEYUP;

            // Send A keyup
            mockedInputHandler.SendVirtualInput(1, &input, sizeof(INPUT));

            // A key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
        }
    };

    TEST_CLASS (SingleKeyRemappingTests)
    {
    public:
        // Test if correct keyboard states are set for a single key remap
        TEST_METHOD (RemappedKey_ShouldSetTargetKeyState_OnKeyEvent)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleSingleKeyRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleSingleKeyRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);

            // Remap A to B
            testState.AddSingleKeyRemap(0x41, 0x42);
            INPUT input;
            input.type = INPUT_KEYBOARD;
            input.ki.wVk = 0x41;

            // Send A keydown
            mockedInputHandler.SendVirtualInput(1, &input, sizeof(INPUT));

            // A key state should be unchanged, and B key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            input.ki.dwFlags = KEYEVENTF_KEYUP;

            // Send A keyup
            mockedInputHandler.SendVirtualInput(1, &input, sizeof(INPUT));

            // A key state should be unchanged, and B key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), false);
        }
    };

    TEST_CLASS (OSLevelShortcutRemappingTests)
    {
    public:
        // Test if correct keyboard states are set for a 2 key shortcut remap key down
        TEST_METHOD (RemappedTwoKeyShortcut_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleSingleKeyRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);

            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 2;
            INPUT input[nInputs] = { {}, {} };
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl and A key states should be unchanged, Alt and V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 2 key shortcut remap key down followed by key up
        TEST_METHOD (Remapped2KeyShortcut_ShouldClearKeyboard_OnKeyUp)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleSingleKeyRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);

            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);
            const int nInputs = 4;
            INPUT input[nInputs] = { {}, {} };
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = VK_CONTROL;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;
            // Send Ctrl+A keydown, followed by A and Ctrl released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, Alt, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }
    };
}
