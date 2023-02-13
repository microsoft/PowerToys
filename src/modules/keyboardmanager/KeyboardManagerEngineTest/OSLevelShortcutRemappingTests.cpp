#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include "TestHelpers.h"
#include <common/interop/shared_constants.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for shortcut remapping logic
    TEST_CLASS (OSLevelShortcutRemappingTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleOSLevelShortcutRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc([currentHookProc](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                {
                    return currentHookProc(data);
                }
                else
                {
                    return 1LL;
                }
            });
        }

        // Tests for shortcut to shortcut remappings

        // Test if correct keyboard states are set for a 2 key shortcut remap with different modifiers key down
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

        // Test if correct keyboard states are set for a 3 key shortcut remap with different modifiers key down
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

        // Test if correct keyboard states are set for a 3 key shortcut remap with partially different modifiers key down
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

        // Tests for shortcut to key remappings

        // Test if correct keyboard states are set for a 2 key shortcut to a single key remap not containing that key on key down followed by key up
        TEST_METHOD (RemappedTwoKeyShortcutToSingleKeyNotContainingThatKey_ShouldSetCorrectKeyStates_OnKeyEvents)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A should be false, Alt should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, Alt should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
        }

        // Test if correct keyboard states are set for a 3 key shortcut to a single key remap not containing that key on key down followed by key up
        TEST_METHOD (RemappedThreeKeyShortcutToSingleKeyNotContainingThatKey_ShouldSetCorrectKeyStates_OnKeyEvents)
        {
            // Remap Ctrl+Shift+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

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

            // Ctrl, Shift, A should be false, Alt should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl+Shift+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A, Alt should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
        }

        // Test if correct keyboard states are set for a 2 key shortcut to a single key remap containing that key on key down followed by key up
        TEST_METHOD (RemappedTwoKeyShortcutToSingleKeyContainingThatKey_ShouldSetCorrectKeyStates_OnKeyEvents)
        {
            // Remap Ctrl+A to Ctrl
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CONTROL);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A should be false, Ctrl should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
        }

        // Test if correct keyboard states are set for a 3 key shortcut to a single key remap containing that key on key down followed by key up
        TEST_METHOD (RemappedThreeKeyShortcutToSingleKeyContainingThatKey_ShouldSetCorrectKeyStates_OnKeyEvents)
        {
            // Remap Ctrl+Shift+A to Ctrl
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CONTROL);

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

            // Shift, A should be false, Ctrl should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl+Shift+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, Shift, A should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
        }

        // Test if keyboard state is not reverted for a shortcut to a single key remap (target key is not a part of the shortcut) on key down followed by releasing the action key
        TEST_METHOD (RemappedShortcutToSingleKeyWhereKeyIsNotInShortcut_ShouldNotSetOriginalModifier_OnReleasingActionKey)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, Alt should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if keyboard state is not reverted for a shortcut to a single key remap (target key is a modifier in the shortcut) on key down followed by releasing the action key
        TEST_METHOD (RemappedShortcutToSingleKeyWhereKeyIsAModifierInShortcut_ShouldNotSetOriginalModifier_OnReleasingActionKey)
        {
            // Remap Ctrl+A to Ctrl
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CONTROL);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Both A and Ctrl should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if keyboard state is not reverted for a shortcut to a single key remap (target key is the action key in the shortcut) on key down followed by releasing the action key
        TEST_METHOD (RemappedShortcutToSingleKeyWhereKeyIsActionKeyInShortcut_ShouldNotSetOriginalModifier_OnReleasingActionKey)
        {
            // Remap Ctrl+A to A
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x41);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if keyboard state is reverted for a shortcut to a single key remap (target key is not a part of the shortcut) on key down followed by releasing the modifier key
        TEST_METHOD (RemappedShortcutToSingleKeyWhereKeyIsNotInShortcut_ShouldSetOriginalModifier_OnReleasingModifierKey)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Alt, Ctrl should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if keyboard state is reverted for a shortcut to a single key remap (target key is a modifier in the shortcut) on key down followed by releasing the modifier key
        TEST_METHOD (RemappedShortcutToSingleKeyWhereKeyIsAModifierInShortcut_ShouldSetOriginalModifier_OnReleasingModifierKey)
        {
            // Remap Ctrl+A to Ctrl
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CONTROL);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if keyboard state is reverted for a shortcut to a single key remap (target key is the action key in the shortcut) on key down followed by releasing the modifier key
        TEST_METHOD (RemappedShortcutToSingleKeyWhereKeyIsActionKeyInShortcut_ShouldSetOriginalModifier_ModifierKey)
        {
            // Remap Ctrl+A to A
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x41);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
        }

        // Test if remap is invoked for a shortcut to a single key remap when the shortcut is invoked along with other keys pressed before it
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldBeInvoked_IfOtherKeysArePressedAlongWithIt)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x42;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Press B+Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl should be false, B, Alt should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
        }

        // Test if remap is invoked for a shortcut to a single key remap and the keyboard state is reverted back to the physical keys when the shortcut is invoked along with other keys pressed before it and the action key is released
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldRevertBackToPhysicalKeys_IfOtherKeysArePressedAlongWithItAndThenActionKeyIsReleased)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 4;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x42;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[3].type = INPUT_KEYBOARD;
            input[3].ki.wVk = 0x41;
            input[3].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press B+Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Alt, A should be false, Ctrl, B should be true
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_MENU));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test that remap is not invoked for a shortcut to a single key remap when a larger remapped shortcut to shortcut containing those shortcut keys is invoked
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldNotBeInvoked_IfALargerRemappedShortcutToShortcutContainingThoseShortcutKeysIsInvoked)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);
            // Remap Shift+Ctrl+A to Ctrl+V
            src.SetKey(VK_SHIFT);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_SHIFT;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Press Shift+Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Alt, A, Shift should be false, Ctrl, V should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test that remap is not invoked for a shortcut to a single key remap when a larger remapped shortcut to key containing those shortcut keys is invoked
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldNotBeInvoked_IfALargerRemappedShortcutToKeyContainingThoseShortcutKeysIsInvoked)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);
            // Remap Shift+Ctrl+A to B
            src.SetKey(VK_SHIFT);
            testState.AddOSLevelShortcut(src, (DWORD)0x42);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_SHIFT;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Press Shift+Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Alt, Ctrl, A, Shift should be false, B should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
        }

        // Test if remap is invoked and then reverted to physical keys for a shortcut to a single key remap when the shortcut is invoked along with other keys pressed after it and then action key is released
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldBeInvokedAndThenRevertToPhysicalKeys_IfOtherKeysArePressedAfterItAndActionKeyIsReleased)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x42;

            // Press Ctrl+A+B
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl should be false, B, Alt should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release A
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // A, Alt should be false, Ctrl, B should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if remap is invoked and then reverted to physical keys for a shortcut to a single key remap when the shortcut is invoked along with other keys pressed after it and modifier key is released
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldBeInvokedAndThenRevertToPhysicalKeys_IfOtherKeysArePressedAfterItAndModifierKeyIsReleased)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x42;

            // Press Ctrl+A+B
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl should be false, B, Alt should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // Ctrl, Alt, A should be false, B should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if remap is invoked and then reverted to physical keys for a shortcut to a single key remap when the shortcut is invoked and action key is released and then other keys pressed after it
        TEST_METHOD (RemappedShortcutToSingleKey_ShouldBeInvokedAndThenRevertToPhysicalKeys_IfActionKeyIsReleasedAndThenOtherKeysArePressedAfterIt)
        {
            // Remap Ctrl+A to Alt
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_MENU);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Press Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl, Alt should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x42;
            input[0].ki.dwFlags = 0;

            // Press B
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // A, Alt should be false, Ctrl, B should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test if Windows left key state is set when a shortcut remap to Win both is invoked
        TEST_METHOD (RemappedShortcutToWinBoth_ShouldSetLWinKeyState_OnKeyEvent)
        {
            // Remap Ctrl+A to Win both
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)CommonSharedConstants::VK_WIN_BOTH);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;

            // Press Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A, Ctrl should be false, LWin should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), true);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[1].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release A, Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Ctrl, A, LWin should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
        }

        // Tests for interaction between shortcut to shortcut and shortcut to key remappings

        // Test if invoking two remapped shortcuts that share modifiers, where the first one remaps to a key and the second one remaps to a shortcut, in succession sets the correct keyboard states
        TEST_METHOD (TwoRemappedShortcutsThatShareModifiersWhereFirstOneRemapsToAKeyAndSecondOneRemapsToAShortcut_ShouldSetRemappedKeyStates_OnPressingSecondShortcutActionKeyAfterInvokingFirstShortcutRemap)
        {
            // Remap Alt+A to D
            Shortcut src;
            src.SetKey(VK_MENU);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x44);

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

            // Alt, A, D, V key states should be unchanged, Ctrl, X should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x44), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x58), true);
        }

        // Test if invoking two remapped shortcuts that share modifiers, where the first one remaps to a key and the second one remaps to a key, in succession sets the correct keyboard states
        TEST_METHOD (TwoRemappedShortcutsThatShareModifiersWhereFirstOneRemapsToAKeyAndSecondOneRemapsToAKey_ShouldSetRemappedKeyStates_OnPressingSecondShortcutActionKeyAfterInvokingFirstShortcutRemap)
        {
            // Remap Alt+A to D
            Shortcut src;
            src.SetKey(VK_MENU);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x44);

            // Remap Alt+V to X
            Shortcut src1;
            src1.SetKey(VK_MENU);
            src1.SetKey(0x56);
            testState.AddOSLevelShortcut(src1, (DWORD)0x58);

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

            // Alt, A, D, V key states should be unchanged, X should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x44), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x58), true);
        }

        // Test if invoking two remapped shortcuts that share modifiers, where the first one remaps to a shortcut and the second one remaps to a key, in succession sets the correct keyboard states
        TEST_METHOD (TwoRemappedShortcutsThatShareModifiersWhereFirstOneRemapsToAShortcutAndSecondOneRemapsToAKey_ShouldSetRemappedKeyStates_OnPressingSecondShortcutActionKeyAfterInvokingFirstShortcutRemap)
        {
            // Remap Alt+A to Ctrl+C
            Shortcut src;
            src.SetKey(VK_MENU);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x43);
            testState.AddOSLevelShortcut(src, dest);

            // Remap Alt+V to X
            Shortcut src1;
            src1.SetKey(VK_MENU);
            src1.SetKey(0x56);
            testState.AddOSLevelShortcut(src1, (DWORD)0x58);

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

            // Alt, A, C, V, Ctrl key states should be unchanged, X should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x44), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x58), true);
        }

        // Test if correct keyboard states are set if a shortcut to single key remap is pressed and then an unremapped shortcut with the same modifier is pressed - Ex: Ctrl+A is remapped. User invokes Ctrl+A then releases A and presses C (while Ctrl is held), should invoke Ctrl+C
        TEST_METHOD (InvokingUnremappedShortcutAfterRemappedShortcutToSingleKeyWithSameModifier_ShouldSetUnremappedShortcut_OnKeyDown)
        {
            // Remap Ctrl+A to V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x56);

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

            // A, V key states should be unchanged, Ctrl, C should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x43), true);
        }

        // Tests for IME Caps Lock workaround on shortcut remappings

        // Test if SendVirtualInput is sent exactly once with the suppress flag when Win+CapsLock is remapped to shortcut containing Ctrl
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenWinCapsLockIsMappedToShortcutContainingCtrl)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Win+CapsLock to Ctrl+A
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(VK_CAPITAL);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 2;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CAPITAL;

            // Send LWin+CapsLock keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when Win+CapsLock is remapped to Ctrl
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenWinCapsLockIsMappedToCtrl)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Win+CapsLock to Ctrl+A
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(VK_CAPITAL);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CONTROL);

            const int nInputs = 2;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CAPITAL;

            // Send LWin+CapsLock keydown
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when shortcut containing Ctrl is remapped to shortcut Win+CapsLock and Ctrl is pressed again while shortcut remap is invoked
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenShortcutContainingCtrlIsMappedToWinCapsLockAndCtrlIsPressedWhileInvoked)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Ctrl+A to Win+CapsLock
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest.SetKey(VK_CAPITAL);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;

            // Send Ctrl+A keydown followed by Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when shortcut containing Ctrl is remapped to shortcut Win+CapsLock and Shift is pressed again while shortcut remap is invoked
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenShortcutContainingCtrlIsMappedToWinCapsLockAndShiftIsPressedWhileInvoked)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Ctrl+A to Win+CapsLock
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest.SetKey(VK_CAPITAL);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_SHIFT;

            // Send Ctrl+A keydown followed by Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when shortcut containing Ctrl is remapped to CapsLock and Ctrl is pressed again while shortcut remap is invoked
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenShortcutContainingCtrlIsMappedToCapsLockAndCtrlIsPressedWhileInvoked)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Ctrl+A to CapsLock
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CAPITAL);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_CONTROL;

            // Send Ctrl+A keydown followed by Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when shortcut containing Ctrl is remapped to CapsLock and Shift is pressed again while shortcut remap is invoked
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenShortcutContainingCtrlIsMappedToCapsLockAndShiftIsPressedWhileInvoked)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Ctrl+A to CapsLock
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)VK_CAPITAL);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = VK_SHIFT;

            // Send Ctrl+A keydown followed by Ctrl
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Tests for all types of shortcut remappings

        // Test that the shortcut remap state is not reset when an unrelated key up message is sent - required to handle programs sending dummy key up messages
        TEST_METHOD (ShortcutRemap_ShouldNotGetReset_OnSendingKeyUpForAKeyNotPresentInTheShortcutAfterInvokingTheShortcut)
        {
            // Remap Ctrl+A to Ctrl+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
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
            input[1].ki.wVk = 0x41;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x42;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // Send Ctrl+A keydown, then B key up
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // A key state should be unchanged, Ctrl, V should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);

            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Tests for shortcut disable remappings

        // Test that shortcut is disabled if the current shortcut pressed matches the exact shortcut which was remapped to Disable
        TEST_METHOD (ShortcutDisable_ShouldDisableShortcut_OnExactMatch)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;

            // send Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Check that Ctrl+A was released and Disable key was not sent
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(actionKey), false);
        }

        // Test that shortcut is not disabled if the shortcut which was remapped to Disable is a subset of the keys currently pressed
        TEST_METHOD (ShortcutDisable_ShouldNotDisableShortcut_OnSubsetMatch)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_SHIFT;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = actionKey;

            // send Ctrl+Shift+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Check that Ctrl+Shift+A was not released and Disable key was not sent
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(actionKey), true);
        }

        // Test that shortcut is not disabled if the shortcut which was remapped to Disable is pressed followed by another key
        TEST_METHOD (ShortcutDisable_ShouldNotDisableShortcutSuperset_AfterShortcutWasDisabled)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;

            // send Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x42;
            // send B
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // Check that Ctrl+A+B was pressed
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(actionKey), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test that shortcut is not disabled if the shortcut which was remapped to Disable is pressed and the action key is released, followed by pressing another key
        TEST_METHOD (ShortcutDisable_ShouldNotDisableShortcutSuperset_AfterActionKeyWasReleasedAndAnotherKeyWasPressedAfterIt)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = actionKey;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // send Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Check that no keys are pressed
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(actionKey), false);
            // Shortcut invoked state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isShortcutInvoked);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x42;
            // send B
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // Check that Ctrl+B was pressed
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(actionKey), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);
            // Shortcut invoked state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isShortcutInvoked);
        }

        // Test that the isOriginalActionKeyPressed flag is set to true on exact match of the shortcut
        TEST_METHOD (ShortcutDisable_ShouldSetIsOriginalActionKeyPressed_OnExactMatch)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;

            // send Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);
        }

        // Test that the isOriginalActionKeyPressed flag is set to false on releasing the action key
        TEST_METHOD (ShortcutDisable_ShouldResetIsOriginalActionKeyPressed_OnReleasingActionKey)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;

            // send Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = actionKey;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // release A
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);
        }

        // Test that the isOriginalActionKeyPressed flag is set to true on pressing the action key again after releasing the action key
        TEST_METHOD (ShortcutDisable_ShouldSetIsOriginalActionKeyPressed_OnPressingActionKeyAfterReleasingActionKey)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 3;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = actionKey;
            input[2].ki.dwFlags = KEYEVENTF_KEYUP;

            // send Ctrl+A, release A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = actionKey;
            input[0].ki.dwFlags = 0;

            // press A
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);
        }

        // Test that the isOriginalActionKeyPressed flag is set to false on releasing the modifier key
        TEST_METHOD (ShortcutDisable_ShouldResetIsOriginalActionKeyPressed_OnReleasingModifierKey)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;

            // send Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // release Ctrl
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);
        }

        // Test that the isOriginalActionKeyPressed flag is set to false on pressing another key
        TEST_METHOD (ShortcutDisable_ShouldResetIsOriginalActionKeyPressed_OnPressingAnotherKey)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;

            testState.AddOSLevelShortcut(src, disableKey);

            const int nInputs = 2;
            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = actionKey;

            // send Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be true
            Assert::AreEqual(true, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x42;
            input[0].ki.dwFlags = 0;

            // press B
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // IsOriginalActionKeyPressed state should be false
            Assert::AreEqual(false, testState.osLevelShortcutReMap[src].isOriginalActionKeyPressed);
        }

        // Tests for dummy key events in shortcut remaps

        // Test if one set of dummy key events is sent before releasing the modifier when shortcut is remapped to a shortcut not containing original shortcut modifiers on invoking the shortcut. Example: Win+A->Ctrl+V, press Win+A, since Win will be released here we need to send a dummy event before it
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendOneSetOfDummyKeyEventsBeforeReleasingTheModifier_WhenShortcutIsRemappedToAShortcutNotContainingOriginalShortcutModifiersOnInvoke)
        {
            // Set sendvirtualinput call count condition to return true if the key event was a dummy key and LWin is pressed
            mockedInputHandler.SetSendVirtualInputTestHandler([this](LowlevelKeyboardEvent* data) {
                if (data->lParam->vkCode == KeyboardManagerConstants::DUMMY_KEY && mockedInputHandler.GetVirtualKeyState(VK_LWIN))
                    return true;
                else
                    return false;
            });

            // Remap Win+A to Ctrl+V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 2;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;

            // Send LWin+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly twice with the above condition (since two dummy key events are sent in one set)
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // LWin should be released
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LWIN));
        }

        // Test if one set of dummy key events is sent after setting the modifier when three key shortcut is remapped to a shortcut on releasing action key and a modifier. Example: Win+Ctrl+A->Ctrl+V, press Win+Ctrl+A and release A then Ctrl, since Win will be pressed here we need to send a dummy event after it
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendOneSetOfDummyKeyEventsAfterSettingTheModifier_When3KeyShortcutIsRemappedToShortcutOnReleasingActionKeyAndAModifier)
        {
            // Remap Win+Ctrl+A to Ctrl+V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddOSLevelShortcut(src, dest);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send LWin+Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Set sendvirtualinput call count condition to return true if the key event was a dummy key and LWin is pressed
            mockedInputHandler.SetSendVirtualInputTestHandler([this](LowlevelKeyboardEvent* data) {
                if (data->lParam->vkCode == KeyboardManagerConstants::DUMMY_KEY && mockedInputHandler.GetVirtualKeyState(VK_LWIN))
                    return true;
                else
                    return false;
            });

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly twice with the above condition (since two dummy key events are sent in one set)
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // LWin should be pressed
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LWIN));
        }

        // Test if one set of dummy key events is sent before releasing the modifier when shortcut is remapped to a single key on invoking the shortcut. Example: Win+A->V, press Win+A, since Win will be released here we need to send a dummy event before it
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendOneSetOfDummyKeyEventsBeforeReleasingTheModifier_WhenShortcutIsRemappedToASingleKeyOnInvoke)
        {
            // Set sendvirtualinput call count condition to return true if the key event was a dummy key and LWin is pressed
            mockedInputHandler.SetSendVirtualInputTestHandler([this](LowlevelKeyboardEvent* data) {
                if (data->lParam->vkCode == KeyboardManagerConstants::DUMMY_KEY && mockedInputHandler.GetVirtualKeyState(VK_LWIN))
                    return true;
                else
                    return false;
            });

            // Remap Win+A toV
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x56);

            const int nInputs = 2;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = 0x41;

            // Send LWin+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly twice with the above condition (since two dummy key events are sent in one set)
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // LWin should be released
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LWIN));
        }

        // Test if one set of dummy key events is sent after setting the modifier when shortcut is remapped to a single key on releasing action key and a modifier. Example: Win+A->V, press Shift+Win+A and release A, since Win will be pressed here we need to send a dummy event after it
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendOneSetOfDummyKeyEventsAfterSettingTheModifier_WhenShortcutIsRemappedToSingleKeyOnReleasingActionKeyAndAModifier)
        {
            // Remap Win+Ctrl+A to V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x56);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_LWIN;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_CONTROL;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send LWin+Ctrl+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Set sendvirtualinput call count condition to return true if the key event was a dummy key and LWin is pressed
            mockedInputHandler.SetSendVirtualInputTestHandler([this](LowlevelKeyboardEvent* data) {
                if (data->lParam->vkCode == KeyboardManagerConstants::DUMMY_KEY && mockedInputHandler.GetVirtualKeyState(VK_LWIN))
                    return true;
                else
                    return false;
            });

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_CONTROL;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release Ctrl
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly twice with the above condition (since two dummy key events are sent in one set)
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // LWin should be pressed
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LWIN));
        }

        // Test if one set of dummy key events is sent after setting the modifier when shortcut is remapped to a single key on invoking shortcut after pressing another key and then releasing the action key. Example: Win+A->V, press Shift+Win+A and release A, since Win will be pressed here we need to send a dummy event after it
        TEST_METHOD (HandleShortcutRemapEvent_ShouldSendOneSetOfDummyKeyEventsAfterSettingTheModifier_WhenShortcutIsRemappedToSingleKeyOnInvokingTheShortcutAfterPressingAnotherKeyAndThenReleasingTheActionKey)
        {
            // Remap Win+A to V
            Shortcut src;
            src.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            src.SetKey(0x41);
            testState.AddOSLevelShortcut(src, (DWORD)0x56);

            const int nInputs = 3;

            INPUT input[nInputs] = {};
            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = VK_SHIFT;
            input[1].type = INPUT_KEYBOARD;
            input[1].ki.wVk = VK_LWIN;
            input[2].type = INPUT_KEYBOARD;
            input[2].ki.wVk = 0x41;

            // Send Shift+LWin+A
            mockedInputHandler.SendVirtualInput(nInputs, input, sizeof(INPUT));

            // Set sendvirtualinput call count condition to return true if the key event was a dummy key and LWin is pressed
            mockedInputHandler.SetSendVirtualInputTestHandler([this](LowlevelKeyboardEvent* data) {
                if (data->lParam->vkCode == KeyboardManagerConstants::DUMMY_KEY && mockedInputHandler.GetVirtualKeyState(VK_LWIN))
                    return true;
                else
                    return false;
            });

            input[0].type = INPUT_KEYBOARD;
            input[0].ki.wVk = 0x41;
            input[0].ki.dwFlags = KEYEVENTF_KEYUP;

            // Release A
            mockedInputHandler.SendVirtualInput(1, input, sizeof(INPUT));

            // SendVirtualInput should be called exactly twice with the above condition (since two dummy key events are sent in one set)
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // LWin should be pressed
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LWIN));
        }
    };
}
