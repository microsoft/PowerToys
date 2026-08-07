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
    // Tests for single key remapping logic
    TEST_CLASS (SingleKeyRemappingTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            // Reset test environment
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleSingleKeyRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleSingleKeyRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);
        }

        // Test if correct keyboard states are set for a single key remap
        TEST_METHOD (RemappedKey_ShouldSetTargetKeyState_OnKeyEvent)
        {
            // Remap A to B
            testState.AddSingleKeyRemap(0x41, (DWORD)0x42);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // A key state should be unchanged, and B key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), true);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be unchanged, and B key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), false);
        }

        // When injecting the remapped key fails (e.g. SendInput is blocked by UIPI or
        // another hook), the handler must let the ORIGINAL key through instead of
        // silently swallowing it, so the user is never left with a dead key. This
        // exercises the stuck-key hardening that checks SendVirtualInput's return value.
        TEST_METHOD (RemappedKey_ShouldPassOriginalKeyThrough_WhenInjectionFails)
        {
            // Remap A to B
            testState.AddSingleKeyRemap(0x41, (DWORD)0x42);

            // Fail only KBM-injected events (tagged with a non-zero dwExtraInfo),
            // leaving the test's own driving input (dwExtraInfo == 0) untouched.
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                for (const auto& input : inputs)
                {
                    if (input.ki.dwExtraInfo != 0)
                    {
                        return true;
                    }
                }
                return false;
            });

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown - injection of B fails, so A must pass through
            mockedInputHandler.SendVirtualInput(inputs);

            // The original A is let through (state true); B was never injected (false)
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
        }

        // When the remapped key-DOWN injection is blocked but the later key-UP injection
        // would succeed, the handler must still let the ORIGINAL key-up through. The
        // key-down was passed through to the app (key is physically DOWN), so swallowing
        // the key-up would strand the physical key DOWN. This guards the asymmetric
        // injection-failure stuck-key edge case, where key-down and key-up arrive as
        // separate hook events.
        TEST_METHOD (RemappedKey_ShouldReleaseOriginalKey_WhenKeyDownInjectionFailedButKeyUpSucceeds)
        {
            // Remap A to B
            testState.AddSingleKeyRemap(0x41, (DWORD)0x42);

            // Fail only KBM-injected key-DOWN events; allow injected key-ups (and the
            // test's own driving input, which has dwExtraInfo == 0) through.
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                for (const auto& input : inputs)
                {
                    if (input.ki.dwExtraInfo != 0 && (input.ki.dwFlags & KEYEVENTF_KEYUP) == 0)
                    {
                        return true;
                    }
                }
                return false;
            });

            std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown - injection of B fails, so A passes through and is now DOWN
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));

            std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup - even though injecting B's key-up would succeed, the original A
            // key-up must pass through so the physical A key is released, not stranded down
            mockedInputHandler.SendVirtualInput(keyUp);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
        }

        // Test if key is suppressed if a key is disabled by single key remap
        TEST_METHOD (RemappedKeyDisabled_ShouldNotChangeKeyState_OnKeyEvent)
        {
            // Remap A to VK_DISABLE (disabled)
            testState.AddSingleKeyRemap(0x41, CommonSharedConstants::VK_DISABLED);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // A key state should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be unchanged
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
        }

        // Test if correct keyboard states are set for a remap to Win (Both) key
        TEST_METHOD (RemappedKeyToWinBoth_ShouldSetWinLeftKeyState_OnKeyEvent)
        {
            // Remap A to Common Win key
            testState.AddSingleKeyRemap(0x41, CommonSharedConstants::VK_WIN_BOTH);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // A key state should be unchanged, and common Win key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), true);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be unchanged, and common Win key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LWIN), false);
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when Caps Lock is remapped to Ctrl
        TEST_METHOD (HandleSingleKeyRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenCapsLockIsMappedToCtrlAltShift)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Caps Lock to Ctrl
            testState.AddSingleKeyRemap(VK_CAPITAL, (DWORD)VK_CONTROL);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CAPITAL } },
            };

            // Send Caps Lock keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when Ctrl is remapped to Caps Lock
        TEST_METHOD (HandleSingleKeyRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenCtrlAltShiftIsMappedToCapsLock)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Ctrl to Caps Lock
            testState.AddSingleKeyRemap(VK_CONTROL, (DWORD)VK_CAPITAL);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
            };

            // Send Ctrl keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly twice with the suppress flag when Caps Lock is remapped to shortcut with Ctrl and Shift
        TEST_METHOD (HandleSingleKeyRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyTwice_WhenCapsLockIsMappedToShortcutWithCtrlAltShift)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Caps Lock to Ctrl+Shift+V
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddSingleKeyRemap(VK_CAPITAL, dest);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CAPITAL } },
            };

            // Send Caps Lock keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // SendVirtualInput should be called exactly twice with the above condition
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when Ctrl is remapped to a shortcut with Caps Lock
        TEST_METHOD (HandleSingleKeyRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenCtrlAltShiftIsMappedToShortcutWithCapsLock)
        {
            // Set sendvirtualinput call count condition to return true if the key event was sent with the suppress flag
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            // Remap Ctrl to Ctrl+Caps Lock
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_CAPITAL);
            testState.AddSingleKeyRemap(VK_CONTROL, dest);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_CONTROL } },
            };

            // Send Ctrl keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // SendVirtualInput should be called exactly once with the above condition
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if SendVirtualInput is sent exactly once with the suppress flag when a Ctrl/Alt/Shift key is remapped to a non-modifier key
        TEST_METHOD (HandleSingleKeyRemapEvent_ShouldSendVirtualInputWithSuppressFlagExactlyOnce_WhenCtrlAltShiftIsMappedToNonModifierKey)
        {
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                    return true;
                else
                    return false;
            });

            testState.AddSingleKeyRemap(VK_LMENU, (DWORD)VK_BACK);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LMENU } },
            };

            mockedInputHandler.SendVirtualInput(inputs);

            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Test if correct keyboard states are set for a single key to two key shortcut remap
        TEST_METHOD (RemappedKeyToTwoKeyShortcut_ShouldSetTargetKeyState_OnKeyEvent)
        {
            // Remap A to Ctrl+V
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x56);
            testState.AddSingleKeyRemap(0x41, dest);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // A key state should be unchanged, and Ctrl, V key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be unchanged, and Ctrl, V key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a single key to three key shortcut remap
        TEST_METHOD (RemappedKeyToThreeKeyShortcut_ShouldSetTargetKeyState_OnKeyEvent)
        {
            // Remap A to Ctrl+Shift+V
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x56);
            testState.AddSingleKeyRemap(0x41, dest);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // A key state should be unchanged, and Ctrl, Shift, V key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be unchanged, and Ctrl, Shift, V key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_CONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_SHIFT), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }

        // Test if correct keyboard states are set for a remap from a single key to a shortcut containing the source key
        TEST_METHOD (RemappedKeyToShortcutContainingSourceKey_ShouldSetTargetKeyState_OnKeyEvent)
        {
            // Remap LCtrl to LCtrl+V
            Shortcut dest;
            dest.SetKey(VK_LCONTROL);
            dest.SetKey(0x56);
            testState.AddSingleKeyRemap(VK_LCONTROL, dest);

            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL } },
            };

            // Send LCtrl keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // LCtrl, V key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LCONTROL), true);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), true);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL, .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send LCtrl keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // LCtrl, V key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(VK_LCONTROL), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x56), false);
        }
    };

    // Tests for single key to text remap modifier release logic
    TEST_CLASS (SingleKeyToTextRemapModifierTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Set HandleSingleKeyToTextRemapEvent as the hook procedure
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);
        }

        // A held Win key must be released before the text is injected and then left
        // released — never re-pressed — so it can never be left stuck down.
        TEST_METHOD (HandleSingleKeyToTextRemapEvent_ShouldReleaseWinKeyAndNotRestore_WhenWinKeyIsHeld)
        {
            // Remap X to text "hello"
            testState.AddSingleKeyToTextRemap(0x58, L"hello");

            // Simulate LWin being held down
            mockedInputHandler.SetKeyboardState(VK_LWIN, true);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LWIN));

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x58 } },
            };

            // Send X keydown — handler releases LWin before the text and does not restore it
            mockedInputHandler.SendVirtualInput(inputs);

            // LWin must be left released so it can never be stuck down
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LWIN));
        }

        // A held Ctrl must be released before the text and left released afterwards.
        TEST_METHOD (HandleSingleKeyToTextRemapEvent_ShouldReleaseCtrlAndNotRestore_WhenCtrlIsHeld)
        {
            // Remap X to text "hello"
            testState.AddSingleKeyToTextRemap(0x58, L"hello");

            // Simulate LCtrl being held down
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x58 } },
            };

            // Send X keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // LCtrl must be left released so it can never be stuck down
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
        }

        // Every modifier that was held should be released, and none re-pressed.
        TEST_METHOD (HandleSingleKeyToTextRemapEvent_ShouldReleaseAllHeldModifiers_AndNotRestore)
        {
            // Remap X to text "hello"
            testState.AddSingleKeyToTextRemap(0x58, L"hello");

            // Simulate LCtrl and LShift being held down together
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_LSHIFT, true);

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x58 } },
            };

            // Send X keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // Both modifiers must be left released
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LSHIFT));
        }

        // The handler must never inject a modifier key-down (re-press) event. Doing
        // so could leave a modifier stuck down if the user released it during text
        // injection, since GetAsyncKeyState cannot distinguish a still-held key from
        // one we just released ourselves.
        TEST_METHOD (HandleSingleKeyToTextRemapEvent_ShouldNeverRePressModifier_WhenModifierIsHeld)
        {
            // Remap X to text "hello"
            testState.AddSingleKeyToTextRemap(0x58, L"hello");

            // Simulate LCtrl being held down
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);

            // Count any modifier key-down events the handler injects (i.e. a re-press)
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* keyEvent) {
                const DWORD vk = keyEvent->lParam->vkCode;
                const bool isModifier = (vk == VK_LCONTROL || vk == VK_RCONTROL || vk == VK_LSHIFT || vk == VK_RSHIFT || vk == VK_LMENU || vk == VK_RMENU || vk == VK_LWIN || vk == VK_RWIN);
                return isModifier && keyEvent->wParam == WM_KEYDOWN;
            });

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x58 } },
            };

            // Send X keydown
            mockedInputHandler.SendVirtualInput(inputs);

            // No modifier re-press should ever be injected
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // A key-to-text remap must still fire while Alt is held. Windows delivers a
        // key pressed with Alt down as WM_SYSKEYDOWN rather than WM_KEYDOWN, so a
        // handler that only accepted WM_KEYDOWN would silently drop the remap. Alt
        // being held also drives the modifier-release path, so the proof that the
        // WM_SYSKEYDOWN event was accepted and processed is that the held Alt ends
        // up released. If WM_SYSKEYDOWN were rejected the handler would return
        // before the release loop and Alt would remain down.
        TEST_METHOD (HandleSingleKeyToTextRemapEvent_ShouldFireAndReleaseAlt_WhenAltIsHeld)
        {
            // Remap X to text "hello"
            testState.AddSingleKeyToTextRemap(0x58, L"hello");

            // Simulate Left Alt being held. VK_MENU makes the mock deliver the key
            // as WM_SYSKEYDOWN (as the OS does while Alt is down); VK_LMENU is the
            // physical key the handler sees as held and must release.
            mockedInputHandler.SetKeyboardState(VK_MENU, true);
            mockedInputHandler.SetKeyboardState(VK_LMENU, true);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LMENU));

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x58 } },
            };

            // Send X keydown — arrives as WM_SYSKEYDOWN because Alt is held
            mockedInputHandler.SendVirtualInput(inputs);

            // The remap fired: the held Alt was released and never re-pressed, so it
            // can never be left stuck down.
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LMENU));
        }
    };
}
