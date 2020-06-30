#include "pch.h"
#include "CppUnitTest.h"
#include "MockedInput.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/dll/KeyboardEventHandlers.h>
#include "TestHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;


namespace RemappingLogicTests
{
    TEST_CLASS (AppSpecificShortcutRemappingTests)
    {
    private:
        MockedInput mockedInputHandler;
        KeyboardManagerState testState;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleOSLevelShortcutRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);
        }

        // Test if correct keyboard states are set for a 2 key shortcut remap wih different modifiers key down
        TEST_METHOD (RemappedTwoKeyShortcutWithDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
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
    };
}
