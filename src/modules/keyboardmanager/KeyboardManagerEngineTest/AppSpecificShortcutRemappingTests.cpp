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
    TEST_CLASS (AppSpecificShortcutRemappingTests)

    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;
        std::wstring testApp1 = L"testprocess1.exe";
        std::wstring testApp2 = L"testprocess2.exe";

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleOSLevelShortcutRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);
        }

        // Test if the app specific remap takes place when the target app is in foreground
        TEST_METHOD (AppSpecificShortcut_ShouldGetRemapped_WhenAppIsInForeground)
        {
            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddAppSpecificShortcut(testApp1, src, dest);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // Ctrl and A key states should be unchanged, Alt and V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if the app specific remap takes place when the target app is not in foreground
        TEST_METHOD (AppSpecificShortcut_ShouldNotGetRemapped_WhenAppIsNotInForeground)
        {
            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddAppSpecificShortcut(testApp1, src, dest);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp2);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // Ctrl and A key states should be true, Alt and V key states should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if the keyboard manager state's activated app is correctly set after an app specific remap takes place
        TEST_METHOD (AppSpecificShortcut_ShouldSetCorrectActivatedApp_WhenRemapOccurs)
        {
            // Remap Ctrl+A to Alt+V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(0x56);
            testState.AddAppSpecificShortcut(testApp1, src, dest);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // Activated app should be testApp1
            Assert::AreEqual(testApp1, testState.GetActivatedApp());

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL, .dwFlags = KEYEVENTF_KEYUP } }
            };

            // Release A then Ctrl
            mockedInputHandler.SendVirtualInput(inputs2);

            // Activated app should be empty string
            Assert::AreEqual(std::wstring(KeyboardManagerConstants::NoActivatedApp), testState.GetActivatedApp());
        }
        // Test if the key states get cleared if foreground app changes after app-specific shortcut is invoked and then released
        TEST_METHOD (AppSpecificShortcut_ShouldClearKeyStates_WhenForegroundAppChangesAfterShortcutIsPressedOnRelease)
        {
            // Remap Ctrl+A to Alt+Tab
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey(VK_TAB);
            testState.AddAppSpecificShortcut(testApp1, src, dest);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp2);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL, .dwFlags = KEYEVENTF_KEYUP } }
            };

            // Release A then Ctrl
            mockedInputHandler.SendVirtualInput(inputs2);

            // Ctrl, A, Alt and Tab should all be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_MENU), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_TAB), false);
        }

        // Test if the app specific shortcut to key remap takes place when the target app is in foreground
        TEST_METHOD (AppSpecificShortcutToSingleKey_ShouldGetRemapped_WhenAppIsInForeground)
        {
            // Remap Ctrl+A to V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddAppSpecificShortcut(testApp1, src, (DWORD)0x56);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // Ctrl and A key states should be unchanged, V key states should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);
        }

        // Test if the app specific shortcut to key remap takes place when the target app is not in foreground
        TEST_METHOD (AppSpecificShortcutToSingleKey_ShouldNotGetRemapped_WhenAppIsNotInForeground)
        {
            // Remap Ctrl+A to V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddAppSpecificShortcut(testApp1, src, (DWORD)0x56);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp2);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // Ctrl and A key states should be true, V key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if the keyboard manager state's activated app is correctly set after an app specific shortcut to key remap takes place
        TEST_METHOD (AppSpecificShortcutToSingleKey_ShouldSetCorrectActivatedApp_WhenRemapOccurs)
        {
            // Remap Ctrl+A to V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddAppSpecificShortcut(testApp1, src, (DWORD)0x56);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // Activated app should be testApp1
            Assert::AreEqual(testApp1, testState.GetActivatedApp());

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL, .dwFlags = KEYEVENTF_KEYUP } }
            };

            // Release A then Ctrl
            mockedInputHandler.SendVirtualInput(inputs2);

            // Activated app should be empty string
            Assert::AreEqual(std::wstring(KeyboardManagerConstants::NoActivatedApp), testState.GetActivatedApp());
        }
        // Test if the key states get cleared if foreground app changes after app-specific shortcut to key shortcut is invoked and then released
        TEST_METHOD (AppSpecificShortcutToSingleKey_ShouldClearKeyStates_WhenForegroundAppChangesAfterShortcutIsPressedOnRelease)
        {
            // Remap Ctrl+A to V
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x41);
            testState.AddAppSpecificShortcut(testApp1, src, (DWORD)0x56);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp2);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL, .dwFlags = KEYEVENTF_KEYUP } }
            };

            // Release A then Ctrl
            mockedInputHandler.SendVirtualInput(inputs2);

            // Ctrl, A, V should all be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Disable app specific shortcut
        TEST_METHOD (AppSpecificShortcutToDisable_ShouldDisable_WhenAppIsOnForeground)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            WORD actionKey = 0x41;
            src.SetKey(actionKey);
            WORD disableKey = CommonSharedConstants::VK_DISABLED;
            testState.AddAppSpecificShortcut(testApp1, src, disableKey);

            // Set the testApp as the foreground process
            mockedInputHandler.SetForegroundProcess(testApp1);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = actionKey } }
            };

            // Send Ctrl+A keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // Check if Ctrl+A is released and disable key was not send
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(actionKey), false);
        }
    };
}
