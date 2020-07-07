#include "pch.h"
#include "CppUnitTest.h"
#include "MockedInput.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/dll/KeyboardEventHandlers.h>
#include "TestHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for shortcut remapping logic
    TEST_CLASS (OSLevelShortcutRemappingTests)
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
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
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

        // Test if correct keyboard states are set for a 2 key shortcut remap with same modifiers key down
        TEST_METHOD (RemappedTwoKeyShortcutWithSameModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+A to Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
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

            // A key state should be unchanged, Ctrl and V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 2 key shortcut remap with different modifiers key down followed by key up
        TEST_METHOD (RemappedTwoKeyShortcutWithDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
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

        // Test if correct keyboard states are set for a 2 key shortcut remap with same modifiers key down followed by key up
        TEST_METHOD (RemappedTwoKeyShortcutWithSameModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+A to Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
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

            // Ctrl, A, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set when a 2 key shortcut is remapped, and a 3 key shortcut containing those keys is invoked - Ex: Ctrl+A remapped, but user presses Ctrl+Shift+A
        TEST_METHOD (RemappedTwoKeyShortcutInvokingAShortcutContainingThoseKeys_ShouldNotBeRemapped_OnKeyDown)
        {
            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Ctrl+Shift+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Since Ctrl+Shift+A is not remapped, no remapping should be invoked
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 3 key shortcut remap wih different modifiers key down
        TEST_METHOD (RemappedThreeKeyShortcutWithDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+Shift+A to Alt+LWin+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Ctrl+Shift+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A key states should be unchanged, Alt, LWin, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 3 key shortcut remap wih partially different modifiers key down
        TEST_METHOD (RemappedThreeKeyShortcutWithPartiallyDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+Shift+A to Alt+Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Ctrl+Shift+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Shift, A key states should be unchanged, Alt, Ctrl, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 3 key shortcut remap with same modifiers key down
        TEST_METHOD (RemappedThreeKeyShortcutWithSameModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+Shift+A to Ctrl+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Ctrl+Shift+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A key state should be unchanged, Ctrl, Shift, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 3 key shortcut remap with different modifiers key down followed by key up
        TEST_METHOD (RemappedThreeKeyShortcutWithDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+Shift+A to Alt+LWin+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 6;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x41;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;
            input[4].type = INPUT_KEYBOARD;
            input[4].ki.wVk = VK_SHIFT;
            input[4].ki.dwFlags = KEYEVENTF_KEYUP;
            input[5].type = INPUT_KEYBOARD;
            input[5].ki.wVk = VK_CONTROL;
            input[5].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+Shift+A keydown, followed by A, Shift and Ctrl released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A, Alt, LWin, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 3 key shortcut remap with partially different modifiers key down followed by key up
        TEST_METHOD (RemappedThreeKeyShortcutWithPartiallyDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+Shift+A to Alt+Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 6;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x41;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;
            input[4].type = INPUT_KEYBOARD;
            input[4].ki.wVk = VK_SHIFT;
            input[4].ki.dwFlags = KEYEVENTF_KEYUP;
            input[5].type = INPUT_KEYBOARD;
            input[5].ki.wVk = VK_CONTROL;
            input[5].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+Shift+A keydown, followed by A, Shift and Ctrl released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A, Alt, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 3 key shortcut remap with same modifiers key down followed by key up
        TEST_METHOD (RemappedThreeKeyShortcutWithSameModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+Shift+A to Ctrl+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 6;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x41;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;
            input[4].type = INPUT_KEYBOARD;
            input[4].ki.wVk = VK_SHIFT;
            input[4].ki.dwFlags = KEYEVENTF_KEYUP;
            input[5].type = INPUT_KEYBOARD;
            input[5].ki.wVk = VK_CONTROL;
            input[5].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+Shift+A keydown, followed by A, Shift and Ctrl released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set when a 3 key shortcut is remapped, and a 2 key shortcut which is a subset of those keys is invoked - Ex: Ctrl+Shift+A remapped, but user presses Ctrl+A
        TEST_METHOD (RemappedThreeKeyShortcutInvokingAShortcutSubsetOfThoseKeys_ShouldNotBeRemapped_OnKeyDown)
        {
            // Remap Ctrl+Shift+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
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

            // Since Ctrl+A is not remapped, no remapping should be invoked
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 3 key to 2 key shortcut remap with different modifiers key down
        TEST_METHOD (RemappedThreeKeyToTwoKeyShortcutWithDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+Shift+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Ctrl+Shift+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A key states should be unchanged, Alt, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 3 key to 2 key shortcut remap with partially different modifiers key down
        TEST_METHOD (RemappedThreeKeyToTwoKeyShortcutWithPartiallyDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+Shift+A to Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Ctrl+Shift+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Shift, A key states should be unchanged, Ctrl, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 3 key to 2 key shortcut remap with different modifiers key down followed by key up
        TEST_METHOD (RemappedThreeKeyToTwoKeyShortcutWithDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+Shift+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 6;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x41;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;
            input[4].type = INPUT_KEYBOARD;
            input[4].ki.wVk = VK_SHIFT;
            input[4].ki.dwFlags = KEYEVENTF_KEYUP;
            input[5].type = INPUT_KEYBOARD;
            input[5].ki.wVk = VK_CONTROL;
            input[5].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+Shift+A keydown, followed by A, Shift and Ctrl released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A, Alt, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 3 key to 2 key shortcut remap with partially different modifiers key down followed by key up
        TEST_METHOD (RemappedThreeKeyToTwoKeyShortcutWithPartiallyDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+Shift+A to Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 6;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x41;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;
            input[4].type = INPUT_KEYBOARD;
            input[4].ki.wVk = VK_SHIFT;
            input[4].ki.dwFlags = KEYEVENTF_KEYUP;
            input[5].type = INPUT_KEYBOARD;
            input[5].ki.wVk = VK_CONTROL;
            input[5].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+Shift+A keydown, followed by A, Shift and Ctrl released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A, V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 2 key to 3 key shortcut remap with different modifiers key down
        TEST_METHOD (RemappedTwoKeyToThreeKeyShortcutWithDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+A to Alt+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_SHIFT);
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

            // Ctrl, A key states should be unchanged, Alt, Shift, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 2 key to 3 key shortcut remap with partially different modifiers key down
        TEST_METHOD (RemappedTwoKeyToThreeKeyShortcutWithPartiallyDiffModifiers_ShouldSetTargetShortcutDown_OnKeyDown)
        {
            // Remap Ctrl+A to Ctrl+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
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

            // A key state should be unchanged, Ctrl, Shift, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if correct keyboard states are set for a 2 key to 3 key shortcut remap with different modifiers key down followed by key up
        TEST_METHOD (RemappedTwoKeyToThreeKeyShortcutWithDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+A to Alt+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
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

            // Send Ctrl+A keydown and A, Ctrl are then released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, Alt, Shift, V key states should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a 2 key to 3 key shortcut remap with partially different modifiers key down followed by key up
        TEST_METHOD (RemappedTwoKeyToThreeKeyShortcutWithPartiallyDiffModifiers_ShouldClearKeyboard_OnKeyUp)
        {
            // Remap Ctrl+A to Ctrl+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
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

            // Send Ctrl+A keydown and A, Ctrl are then released
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, Shift, V key states should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set if a shortcut remap is pressed and then an unremapped shortcut with the same modifier is pressed - Ex: Ctrl+A is remapped. User invokes Ctrl+A then releases A and presses C (while Ctrl is held), should invoke Ctrl+C
        TEST_METHOD (InvokingUnremappedShortcutAfterRemappedShortcutWithSameModifier_ShouldSetUnremappedShortcut_OnKeyDown)
        {
            // Remap Ctrl+A to Alt+Shift+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x43;

            // Send Ctrl+A keydown, A key up, then C key down
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Alt, Shift, V key states should be unchanged, Ctrl, C should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x43), true);
        }

        // Test if correct keyboard states are set for a shortcut remap with win both modifier
        TEST_METHOD (RemappedShortcutWithWinBothModifier_ShouldSetRemappedShortcut_OnKeyEvent)
        {
            // Remap Win+A to Alt+V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            // Remap Alt+D to Win+B
            Shortcut dest1;
            dest1.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest1.SetKey(0x42);
            Shortcut src1;
            src1.SetKey(VK_MENU);
            src1.SetKey(0x44);
            testState.AddOSLevelShortcut(src1, dest1);

            // Test 2 cases for first remap - LWin, A, A(Up), LWin(Up). RWin, A, A(Up), RWin(Up)
            const int nInputs = 2;
            INPUT input[nInputs] = {};

            // Case 1.1
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;

            // Send LWin+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // LWin, RWin, A key states should be unchanged, Alt, V should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_LWIN;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release LWin+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // LWin, RWin, A, Alt, V key states should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);

            // Case 1.2
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_RWIN;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;

            // Send RWin+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // LWin, RWin, A key states should be unchanged, Alt, V should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_RWIN;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release RWin+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // LWin, RWin, A, Alt, V key states should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);

            // Case 2
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_MENU;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x44;
            input[1].ki.dwFlags = 0;

            // Send Alt+D keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Alt, D, RWin key states should be unchanged, LWin, B should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x44), false);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x44;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_MENU;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Alt+D
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // LWin, RWin, B, Alt, D key states should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x44), false);
        }

        // Test if correct keyboard states are set if a win both shortcut remap is pressed and then an unremapped shortcut with the LWin modifier is pressed
        TEST_METHOD (InvokingUnremappedShortcutWithLWinAfterRemappedShortcutWithWinBothModifier_ShouldSetUnremappedShortcutWithLWinKey_OnKeyDown)
        {
            // Remap Win+A to Alt+V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            // LWin, A, A(Up), C(Down)
            const int nInputs = 4;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x43;
            input[3].ki.dwFlags = 0;

            // Send LWin+A, release A and press C
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // RWin, A, Alt, V key states should be unchanged, LWin, C should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x43), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set if a win both shortcut remap is pressed and then an unremapped shortcut with the RWin modifier is pressed
        TEST_METHOD (InvokingUnremappedShortcutWithRWinAfterRemappedShortcutWithWinBothModifier_ShouldSetUnremappedShortcutWithRWinKey_OnKeyDown)
        {
            // Remap Win+A to Alt+V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            // RWin, A, A(Up), C(Down)
            const int nInputs = 4;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_RWIN;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x43;
            input[3].ki.dwFlags = 0;

            // Send RWin+A, release A and press C
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // LWin, A, Alt, V key states should be unchanged, RWin, C should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_RWIN), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x43), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if target modifier is still held down even if the action key of the original shortcut is released - required for Alt+Tab/Win+Space cases
        TEST_METHOD (RemappedShortcutModifiers_ShouldBeDetectedAsPressed_OnReleasingActionKeyButHoldingModifiers)
        {
            // Remap Ctrl+A to Alt+Tab
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_TAB);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, Tab key states should be unchanged, Alt should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_TAB), false);
        }

        // Test if invoking two remapped shortcuts (with different modifiers between original and new shortcut) that share modifiers in succession sets the correct keyboard states
        TEST_METHOD (TwoRemappedShortcutsWithDifferentModifiersThatShareModifiers_ShouldSetRemappedKeyStates_OnPressingSecondShortcutActionKeyAfterInvokingFirstShortcutRemap)
        {
            // Remap Alt+A to Ctrl+C
            Shortcut src;
            src.SetKey(VK_MENU);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x43);
            testState.AddOSLevelShortcut(src, dest);

            // Remap Alt+V to Ctrl+X
            Shortcut src1;
            src1.SetKey(VK_MENU);
            src1.SetKey(0x56);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x58);
            testState.AddOSLevelShortcut(src1, dest1);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_MENU;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x56;
            input[3].ki.dwFlags = 0;

            // Send Alt+A, release A, press V
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Alt, A, C, V key states should be unchanged, Ctrl, X should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x43), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x58), true);
        }

        // Test if invoking two remapped shortcuts (with same modifiers between original and new shortcut) that share modifiers in succession sets the correct keyboard states
        TEST_METHOD (TwoRemappedShortcutsWithSameModifiersThatShareModifiers_ShouldSetRemappedKeyStates_OnPressingSecondShortcutActionKeyAfterInvokingFirstShortcutRemap)
        {
            // Remap Ctrl+A to Ctrl+C
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x43);
            testState.AddOSLevelShortcut(src, dest);

            // Remap Ctrl+V to Ctrl+X
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x56);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x58);
            testState.AddOSLevelShortcut(src1, dest1);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[0].ki.dwFlags = 0;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[1].ki.dwFlags = 0;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x56;
            input[3].ki.dwFlags = 0;

            // Send Ctrl+A, release A, press V
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, C, V key states should be unchanged, Ctrl, X should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x43), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x58), true);
        }
    };
}
