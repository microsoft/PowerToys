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
#include <keyboardmanager/common/Helpers.h>

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

    // Tests for the "Alone" single-key remapping logic (dual-key / Karabiner to_if_alone):
    // tapping a key alone triggers a remapped action, while using it in combination passes the
    // original key through (e.g. Right Ctrl alone -> IME On, Right Ctrl + H -> Ctrl+H).
    TEST_CLASS (SingleKeyAloneRemappingTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

        // Predicate that counts how many IME On events flow through SendVirtualInput. Installing it
        // also resets the call counter to 0.
        void CountImeOnInjections()
        {
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                return data->lParam->vkCode == VK_IME_ON;
            });
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);

            // Bind the alone-remap handler under test as the hook procedure.
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleSingleKeyAloneRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);
        }

        // On key-down of an alone-mapped key nothing is injected yet (lazy): we must first see whether
        // the key is tapped alone or used in combination. The original key-down is suppressed meanwhile.
        TEST_METHOD (AloneRemap_ShouldNotInjectOnKeyDown_AndSuppressOriginal)
        {
            // Right Ctrl alone -> IME On
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);
            CountImeOnInjections();

            std::vector<INPUT> rctrlDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } },
            };
            mockedInputHandler.SendVirtualInput(rctrlDown);

            // Nothing injected yet: neither the original Right Ctrl nor the alone action.
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_IME_ON));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Tapping the alone-mapped key alone (down then up, no other key in between) fires the alone
        // action on release: the target is injected as a tap (down + up). The original key never leaks.
        TEST_METHOD (AloneRemap_ShouldInjectAloneAction_OnTapWithoutOtherKey)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);
            CountImeOnInjections();

            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);

            // Still nothing on the way down.
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);

            // On release the alone action fires as a tap: IME On down + up = 2 injected events.
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // The original Right Ctrl was never sent as a real modifier.
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
        }

        // Using the alone-mapped key in combination (another key pressed while it is held) passes the
        // ORIGINAL key through as a real modifier, and the alone action never fires.
        TEST_METHOD (AloneRemap_ShouldPassOriginalKeyThrough_InCombination)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);
            CountImeOnInjections();

            // Right Ctrl down (held, tap candidate) — not injected yet.
            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));

            // H pressed while Right Ctrl held -> combination: Right Ctrl is now injected as a real
            // modifier and H passes through.
            std::vector<INPUT> hDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x48 } } };
            mockedInputHandler.SendVirtualInput(hDown);

            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x48));
            // The alone action must NOT have fired.
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            // Releasing Right Ctrl releases the real modifier; still no alone action.
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // A disabled alone target ("do nothing when tapped alone") injects nothing on tap, yet the
        // source key is still fully suppressed (it never leaks as its original key).
        TEST_METHOD (AloneRemap_DisabledAloneTarget_ShouldInjectNothing_OnTap)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)CommonSharedConstants::VK_DISABLED);

            // Count every event that reaches SendVirtualInput.
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent*) { return true; });

            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);

            // Only the two driving events flowed through; the handler injected nothing extra.
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
        }

        // Runtime state must reset cleanly between presses: after using the key in a combination, a
        // later solo tap still fires the alone action.
        TEST_METHOD (AloneRemap_ShouldTapAgain_AfterPreviousCombinationCycle)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);

            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            std::vector<INPUT> hDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x48 } } };
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };

            // Cycle 1: combination (Right Ctrl + H) — resolves without firing the alone action.
            mockedInputHandler.SendVirtualInput(rctrlDown);
            mockedInputHandler.SendVirtualInput(hDown);
            mockedInputHandler.SendVirtualInput(rctrlUp);

            // Start counting IME On injections for cycle 2.
            CountImeOnInjections();

            // Cycle 2: solo tap — the alone action fires again (down + up = 2 injected events).
            mockedInputHandler.SendVirtualInput(rctrlDown);
            mockedInputHandler.SendVirtualInput(rctrlUp);
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // A click (or scroll) while the alone-mapped key is held must promote it to a real modifier
        // just like a keyboard key would, so combinations like Ctrl+Click / Ctrl+Wheel work. This is
        // the mouse-hook entry point (PromotePendingAloneKeysToCombination); the alone action must NOT
        // fire and the original key must pass through as a real modifier.
        TEST_METHOD (AloneRemap_MousePromotion_ShouldPassOriginalKeyThrough_OnClick)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);
            CountImeOnInjections();

            // Right Ctrl down (held, tap candidate) — nothing injected yet.
            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));

            // A mouse button-down/scroll arrives while Right Ctrl is held: the mouse hook promotes the
            // held key to a real modifier (Right Ctrl is now injected as a real Ctrl).
            KeyboardEventHandlers::PromotePendingAloneKeysToCombination(mockedInputHandler, testState);

            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
            // The alone action must NOT have fired.
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            // Releasing Right Ctrl releases the real modifier; still no alone action.
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // A click with NO alone key held is a no-op for promotion (nothing pending), and a later solo
        // tap still fires the alone action — i.e. a stray click never disturbs the tap machinery.
        TEST_METHOD (AloneRemap_MousePromotion_NoHeldKey_IsNoOp_AndTapStillFires)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);

            // Click while nothing is held: promotion has no pending keys, so it injects nothing.
            KeyboardEventHandlers::PromotePendingAloneKeysToCombination(mockedInputHandler, testState);

            CountImeOnInjections();

            // Solo tap afterwards still fires the alone action (down + up = 2 injected events).
            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            mockedInputHandler.SendVirtualInput(rctrlUp);
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // An "alone" remap whose target is a SHORTCUT (not a single key) must inject the whole
        // shortcut as a tap (modifiers + action key, pressed then released), not silently do nothing.
        TEST_METHOD (AloneRemap_ShortcutTarget_ShouldInjectFullShortcut_OnTap)
        {
            // Right Ctrl alone -> Ctrl+A
            Shortcut aloneTarget;
            aloneTarget.SetKey(VK_CONTROL);
            aloneTarget.SetKey(0x41);
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, aloneTarget);

            // Count injections of the shortcut's action key (A).
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                return data->lParam->vkCode == 0x41;
            });

            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            // Lazy: nothing injected on the way down.
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);

            // On tap the shortcut's action key is injected as down + up = 2 events (previously the
            // shortcut target was ignored and nothing was injected).
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            // The original Right Ctrl was never sent as its real self.
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
        }

        // A single-key alone target of "Win (Both)" (VK_WIN_BOTH) is an artificial KBM key code. On tap
        // it must be filtered to a real virtual key (VK_LWIN) before injection; otherwise SendInput
        // receives the invalid key 0x104 and the tap silently does nothing.
        TEST_METHOD (AloneRemap_WinBothTarget_ShouldInjectLeftWin_OnTap)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)CommonSharedConstants::VK_WIN_BOTH);

            // Count injections of the filtered real key (Left Win). The artificial 0x104 must never appear.
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                Assert::AreNotEqual((DWORD)CommonSharedConstants::VK_WIN_BOTH, (DWORD)data->lParam->vkCode);
                return data->lParam->vkCode == VK_LWIN;
            });

            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            // Lazy: nothing injected on the way down.
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);

            // On tap, Left Win is injected as down + up = 2 events.
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Two alone-mapped keys: pressing the second while the first is still held is a combination,
        // not a solo tap. The second key must be promoted to a real key and must NOT fire its alone
        // action on release.
        TEST_METHOD (AloneRemap_SecondAloneKeyWhileFirstHeld_IsCombination_NotTap)
        {
            testState.AddSingleKeyAloneRemap(VK_LCONTROL, (DWORD)VK_IME_OFF);
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);

            // Count either alone (IME) action firing.
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                return data->lParam->vkCode == VK_IME_ON || data->lParam->vkCode == VK_IME_OFF;
            });

            // Left Ctrl down (held, tap candidate) — nothing injected yet.
            std::vector<INPUT> lctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL } } };
            mockedInputHandler.SendVirtualInput(lctrlDown);

            // Right Ctrl down while Left Ctrl is held: both are a combination now. Left Ctrl is promoted
            // to a real modifier and Right Ctrl is injected as a real key too; no alone action fires.
            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));

            // Releasing Right Ctrl releases the real key; it must NOT fire IME On (it was not tapped alone).
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));

            // Release Left Ctrl as well.
            std::vector<INPUT> lctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(lctrlUp);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));

            // No alone (IME) action fired at any point.
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // If injecting the promoted real key is blocked (e.g. by UIPI / SendInput failure), the alone key
        // must NOT be recorded as a started combination -- otherwise its release would inject an unmatched
        // real key-up while the physical key-down was swallowed. It instead stays a tap candidate and fires
        // its alone action on release. Regression test for the promotion path ignoring SendVirtualInput's
        // result.
        TEST_METHOD (AloneRemap_PromotionInjectionBlocked_DoesNotStrandCombination)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, (DWORD)VK_IME_ON);

            // Block only the KBM-injected real Right Ctrl (the promotion), identified by the single-key
            // injection marker, so the test's own driving Right Ctrl events still reach the hook and the
            // alone action (IME On) still succeeds.
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                for (const INPUT& i : inputs)
                {
                    if (i.ki.wVk == VK_RCONTROL && i.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG)
                    {
                        return true;
                    }
                }
                return false;
            });

            // Count IME On (the alone action) injections.
            CountImeOnInjections();

            // Right Ctrl down (tap candidate), then H down triggers promotion whose injection is blocked.
            std::vector<INPUT> rctrlDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL } } };
            mockedInputHandler.SendVirtualInput(rctrlDown);
            std::vector<INPUT> hDown{ { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x48 } } };
            mockedInputHandler.SendVirtualInput(hDown);

            // Right Ctrl was never injected as a real modifier (the injection was blocked).
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));

            // Because the blocked promotion left Right Ctrl a tap candidate (not a stranded combination),
            // releasing it fires the alone action (IME On down + up = 2) rather than injecting an unmatched
            // real Right Ctrl key-up.
            std::vector<INPUT> rctrlUp{ { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RCONTROL, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(rctrlUp);
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // A numpad-originated alone key must be re-injected preserving its origin. Alone keys are tracked
        // by the numpad-origin-encoded vkCode (marker in bit 31); a bare WORD cast would drop it and turn
        // e.g. a NumLock-off numpad navigation key into its extended (arrow-cluster) twin. Verifies the
        // extended flag AppendAloneSourceKeyEvent produces.
        TEST_METHOD (AppendAloneSourceKeyEvent_PreservesNumpadOrigin)
        {
            // NumLock-off numpad navigation key: VK_LEFT arriving NOT extended encodes as numpad-origin.
            const DWORD numpadLeft = Helpers::EncodeKeyNumpadOrigin(VK_LEFT, /*extended*/ false);
            Assert::IsTrue(Helpers::IsNumpadOriginated(numpadLeft), L"numpad-origin bit should be set");

            std::vector<INPUT> numpadEvent;
            KeyboardEventHandlers::AppendAloneSourceKeyEvent(numpadEvent, numpadLeft, /*keyUp*/ false);
            Assert::AreEqual((size_t)1, numpadEvent.size());
            // The real VK is recovered (marker cleared) and the event is NOT extended (numpad, not arrow).
            Assert::AreEqual((WORD)VK_LEFT, numpadEvent[0].ki.wVk);
            Assert::AreEqual((DWORD)0, numpadEvent[0].ki.dwFlags & KEYEVENTF_EXTENDEDKEY);

            // Contrast: the real arrow (extended) VK_LEFT carries no origin marker and IS injected extended.
            const DWORD arrowLeft = Helpers::EncodeKeyNumpadOrigin(VK_LEFT, /*extended*/ true);
            Assert::IsFalse(Helpers::IsNumpadOriginated(arrowLeft), L"arrow key should carry no numpad-origin bit");

            std::vector<INPUT> arrowEvent;
            KeyboardEventHandlers::AppendAloneSourceKeyEvent(arrowEvent, arrowLeft, /*keyUp*/ false);
            Assert::AreEqual((size_t)1, arrowEvent.size());
            Assert::AreEqual((WORD)VK_LEFT, arrowEvent[0].ki.wVk);
            Assert::AreNotEqual((DWORD)0, arrowEvent[0].ki.dwFlags & KEYEVENTF_EXTENDEDKEY);
        }
    };

    // Tests for the single-key-to-sequence remap (#5679): pressing one key sends an ordered list of
    // key taps, in order; the source key is fully replaced (its own down and up are both suppressed).
    TEST_CLASS (SingleKeyToSequenceRemappingTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

        // Records the vkCode of each KBM-INJECTED event (carrying the single-key flag), in order, so
        // the emitted sequence and its ordering can be asserted. The physical key the test itself
        // sends via SendVirtualInput carries no KBM flag and is deliberately excluded (mirroring how
        // the alone tests count only their injected target). Returns false so it never affects the
        // SendVirtualInput call counter.
        std::vector<DWORD> injected;

        // Adds a key-sequence remap and makes it the one the hook uses, as a settings load does
        // (load into the table, stage a finished copy, publish it on the hook thread).
        void AddSequenceRemap(DWORD key, const std::vector<DWORD>& sequence)
        {
            testState.AddSingleKeyToSequenceRemap(key, sequence);
            testState.StageSequenceTable();
            testState.PublishStagedSequenceTable(mockedInputHandler);
        }

        void RecordInjected()
        {
            injected.clear();
            mockedInputHandler.SetSendVirtualInputTestHandler([this](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG)
                {
                    injected.push_back(data->lParam->vkCode);
                }
                return false;
            });
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);
            std::function<intptr_t(LowlevelKeyboardEvent*)> currentHookProc = std::bind(&KeyboardEventHandlers::HandleSingleKeyToSequenceRemapEvent, std::ref(mockedInputHandler), std::placeholders::_1, std::ref(testState));
            mockedInputHandler.SetHookProc(currentHookProc);
        }

        // Esc -> [Esc, IME-Off]: tapping Esc injects an Esc tap then an IME-Off tap (down+up each), in
        // that order, and the physical Esc down is suppressed. This is the motivating case for #5679.
        TEST_METHOD (SequenceRemap_ShouldInjectEachTargetAsTap_InOrder_OnKeyDown)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });
            RecordInjected();

            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            mockedInputHandler.SendVirtualInput(escDown);

            // Esc tap (down, up) then IME-Off tap (down, up) = 4 events, in order.
            Assert::AreEqual((size_t)4, injected.size());
            Assert::AreEqual((DWORD)VK_ESCAPE, injected[0]);
            Assert::AreEqual((DWORD)VK_ESCAPE, injected[1]);
            Assert::AreEqual((DWORD)VK_IME_OFF, injected[2]);
            Assert::AreEqual((DWORD)VK_IME_OFF, injected[3]);
        }

        // The source key's own key-up is suppressed too (the sequence already fired on the key-down),
        // so the original key never reaches the app on its own.
        TEST_METHOD (SequenceRemap_ShouldSuppressOriginalKeyUp_WithNoFurtherInjection)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });

            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            mockedInputHandler.SendVirtualInput(escDown);

            RecordInjected(); // start recording for the key-up only
            std::vector<INPUT> escUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(escUp);

            Assert::AreEqual((size_t)0, injected.size());
        }

        // A key with no sequence remap is passed through untouched (handler returns 0 -> not suppressed).
        TEST_METHOD (SequenceRemap_ShouldIgnoreUnmappedKey)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });

            std::vector<INPUT> aDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(aDown);

            // 'A' is not sequence-mapped: it passes through and sets its own key state.
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState('A'));
        }

        // Holding the source key sends the sequence once: auto-repeat key-downs are swallowed without
        // injecting again. After the key-up, the next press sends the sequence again.
        TEST_METHOD (SequenceRemap_ShouldSendOncePerPress_IgnoringAutoRepeat)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });
            RecordInjected();

            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            std::vector<INPUT> escUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE, .dwFlags = KEYEVENTF_KEYUP } },
            };

            mockedInputHandler.SendVirtualInput(escDown);
            mockedInputHandler.SendVirtualInput(escDown); // auto-repeat
            mockedInputHandler.SendVirtualInput(escDown); // auto-repeat
            Assert::AreEqual((size_t)4, injected.size());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_ESCAPE));

            mockedInputHandler.SendVirtualInput(escUp);
            mockedInputHandler.SendVirtualInput(escDown);
            Assert::AreEqual((size_t)8, injected.size());
        }

        // A key-up whose key-down this handler never saw (the key was already held when the remap was
        // loaded, so its down reached the app) is passed through, so the key isn't left stuck down.
        TEST_METHOD (SequenceRemap_ShouldPassThroughKeyUp_WhenKeyDownWasNotHandled)
        {
            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            mockedInputHandler.SendVirtualInput(escDown); // no remap yet: Esc goes down normally
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_ESCAPE));

            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });
            RecordInjected();

            std::vector<INPUT> escUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(escUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_ESCAPE));
            Assert::AreEqual((size_t)0, injected.size());
        }

        // The source key never reaches the app: after a full press (down + up) of a source key that
        // is not itself in the sequence, its own key state was never set.
        TEST_METHOD (SequenceRemap_SourceKeyStateIsNeverSet)
        {
            AddSequenceRemap(VK_F1, std::vector<DWORD>{ 0x41, 0x42 });

            std::vector<INPUT> f1Down{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1 } },
            };
            mockedInputHandler.SendVirtualInput(f1Down);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));

            std::vector<INPUT> f1Up{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1, .dwFlags = KEYEVENTF_KEYUP } },
            };
            RecordInjected();
            mockedInputHandler.SendVirtualInput(f1Up);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));
            Assert::AreEqual((size_t)0, injected.size());
        }

        // A Disabled element is skipped, and the KBM "Win (either)" code is sent as Left Win.
        TEST_METHOD (SequenceRemap_ShouldSkipDisabled_AndFilterWinBoth)
        {
            AddSequenceRemap(VK_F1, std::vector<DWORD>{ CommonSharedConstants::VK_DISABLED, CommonSharedConstants::VK_WIN_BOTH, 0x41 });
            RecordInjected();

            std::vector<INPUT> f1Down{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1 } },
            };
            mockedInputHandler.SendVirtualInput(f1Down);

            Assert::AreEqual((size_t)4, injected.size());
            Assert::AreEqual((DWORD)VK_LWIN, injected[0]);
            Assert::AreEqual((DWORD)VK_LWIN, injected[1]);
            Assert::AreEqual((DWORD)0x41, injected[2]);
            Assert::AreEqual((DWORD)0x41, injected[3]);
        }

        // If the injection is blocked (e.g. by UIPI), the original key is passed through -- both its
        // key-down and its key-up -- instead of becoming a dead key or being stranded down.
        TEST_METHOD (SequenceRemap_ShouldPassThroughOriginalKey_WhenInjectionFails)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                for (const auto& input : inputs)
                {
                    if (input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG)
                    {
                        return true;
                    }
                }
                return false;
            });

            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            mockedInputHandler.SendVirtualInput(escDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_ESCAPE));

            std::vector<INPUT> escUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(escUp);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_ESCAPE));
        }

        // If the injection is blocked on the key-down and starts working while the key is still held,
        // the rest of the press is still passed through (no retry): the key went down in the app, so
        // its key-up must reach the app too.
        TEST_METHOD (SequenceRemap_KeepsPassingThroughPress_WhenInjectionRecoversWhileHeld)
        {
            AddSequenceRemap(VK_F1, std::vector<DWORD>{ 0x41, 0x42 });
            bool blocked = true;
            mockedInputHandler.SetSendVirtualInputShouldFail([&blocked](const std::vector<INPUT>& inputs) {
                // Block only KBM's own injection, not the simulated physical key events.
                return blocked && !inputs.empty() && inputs[0].ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
            });
            RecordInjected();

            std::vector<INPUT> f1Down{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1 } },
            };
            mockedInputHandler.SendVirtualInput(f1Down);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_F1));

            blocked = false;
            mockedInputHandler.SendVirtualInput(f1Down); // auto-repeat, injection would now succeed
            Assert::AreEqual((size_t)0, injected.size());

            std::vector<INPUT> f1Up{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(f1Up);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));

            // The next press sends the sequence normally.
            mockedInputHandler.SendVirtualInput(f1Down);
            Assert::AreEqual((size_t)4, injected.size());
        }

        // A passed-through press stays passed through across a settings reload: its key-down already
        // reached the app, so the reload must not turn its next auto-repeat into a fresh press whose
        // key-up would then be swallowed.
        TEST_METHOD (SequenceRemap_PassedThroughPress_SurvivesReload)
        {
            AddSequenceRemap(VK_F1, std::vector<DWORD>{ 0x41, 0x42 });
            bool blocked = true;
            mockedInputHandler.SetSendVirtualInputShouldFail([&blocked](const std::vector<INPUT>& inputs) {
                return blocked && !inputs.empty() && inputs[0].ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
            });
            RecordInjected();

            std::vector<INPUT> f1Down{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1 } },
            };
            mockedInputHandler.SendVirtualInput(f1Down);

            // settings reload while held
            testState.ClearSingleKeyToSequenceRemaps();
            AddSequenceRemap(VK_F1, std::vector<DWORD>{ 0x41, 0x42 });
            Assert::IsFalse(testState.SequencePresses().Empty());
            blocked = false;
            mockedInputHandler.SendVirtualInput(f1Down); // auto-repeat
            Assert::AreEqual((size_t)0, injected.size());

            std::vector<INPUT> f1Up{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(f1Up);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));
        }

        // A Ctrl/Alt/Shift source key: before the taps, a key-up of the source key is sent with the
        // suppress flag (as the single-key remap does), so the taps aren't delivered as
        // WM_SYSKEYDOWN. A non-modifier source key sends no such key-up.
        TEST_METHOD (SequenceRemap_ModifierSource_ResetsModifierBeforeTaps)
        {
            AddSequenceRemap(VK_LMENU, std::vector<DWORD>{ 0x46 });
            AddSequenceRemap(VK_F1, std::vector<DWORD>{ 0x46 });
            std::vector<std::pair<DWORD, ULONG_PTR>> events;
            mockedInputHandler.SetSendVirtualInputTestHandler([&events](LowlevelKeyboardEvent* data) {
                if (data->lParam->dwExtraInfo != 0)
                {
                    events.emplace_back(data->lParam->vkCode, data->lParam->dwExtraInfo);
                }
                return false;
            });

            std::vector<INPUT> altDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LMENU } },
            };
            mockedInputHandler.SendVirtualInput(altDown);

            Assert::AreEqual((size_t)3, events.size());
            Assert::AreEqual((DWORD)VK_LMENU, events[0].first);
            Assert::IsTrue(events[0].second == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG);
            Assert::AreEqual((DWORD)0x46, events[1].first);
            Assert::AreEqual((DWORD)0x46, events[2].first);

            events.clear();
            std::vector<INPUT> f1Down{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_F1 } },
            };
            mockedInputHandler.SendVirtualInput(f1Down);
            Assert::AreEqual((size_t)2, events.size());
            Assert::AreEqual((DWORD)0x46, events[0].first);
        }

        // A key-up that never arrives leaves its press recorded: the next key-down is taken for an
        // auto-repeat and ignored, and the key-up of that second press clears the record. (A known
        // limitation, see SequencePressTracker.)
        TEST_METHOD (SequenceRemap_LostKeyUp_IgnoresOnePress_ThenRecovers)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });
            RecordInjected();

            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            std::vector<INPUT> escUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(escDown); // key-up of this press is "lost"
            mockedInputHandler.SendVirtualInput(escDown);
            Assert::AreEqual((size_t)4, injected.size());

            mockedInputHandler.SendVirtualInput(escUp);
            mockedInputHandler.SendVirtualInput(escDown);
            Assert::AreEqual((size_t)8, injected.size());
        }

        // Installing the hook again starts the press records afresh, so a key-up lost before that
        // doesn't make the next press be treated as an auto-repeat and ignored.
        TEST_METHOD (SequenceRemap_ResetOfPressRecords_ForgetsHeldKey)
        {
            AddSequenceRemap(VK_ESCAPE, std::vector<DWORD>{ static_cast<DWORD>(VK_ESCAPE), static_cast<DWORD>(VK_IME_OFF) });
            RecordInjected();

            std::vector<INPUT> escDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_ESCAPE } },
            };
            mockedInputHandler.SendVirtualInput(escDown); // key-up of this press is "lost"

            // What KeyboardManager::StartLowlevelKeyboardHook does before it installs the hook
            testState.SequencePresses().Reset();
            testState.SequencePresses().SeedForwardedPresses(mockedInputHandler);
            Assert::IsTrue(testState.SequencePresses().Empty());
            mockedInputHandler.SendVirtualInput(escDown);

            Assert::AreEqual((size_t)8, injected.size());
        }
    };

    // Tests for how a key-sequence remap pairs each physical press with its release (#5679). They
    // run the whole handler chain (DispatchKeyboardEvent), as the hook does, and look at the events
    // that reached the system.
    TEST_CLASS (SequencePressPairingTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

        // True while remapping is bypassed (settings loading, or the editor window open)
        bool remappingSuspended = false;

        using SequenceRemaps = std::vector<std::pair<DWORD, std::vector<DWORD>>>;

        // Loads key-sequence remaps into the loader's table, without making them live.
        void LoadSequenceRemaps(const SequenceRemaps& remaps)
        {
            testState.ClearSingleKeyToSequenceRemaps();
            for (const auto& [key, sequence] : remaps)
            {
                testState.AddSingleKeyToSequenceRemap(key, sequence);
            }
        }

        // Replaces the key-sequence remaps the way a settings reload does: load, stage a finished
        // copy, and publish it on the hook thread.
        void ReloadSequenceRemaps(const SequenceRemaps& remaps)
        {
            LoadSequenceRemaps(remaps);
            testState.StageSequenceTable();
            testState.PublishStagedSequenceTable(mockedInputHandler);
        }

        // What KeyboardManager::StartLowlevelKeyboardHook does to the press records before it installs the hook
        void RestartPressTracking()
        {
            testState.SequencePresses().Reset();
            testState.SequencePresses().SeedForwardedPresses(mockedInputHandler);
        }

        void SendKey(WORD vk, bool keyUp, WORD scanCode = 0)
        {
            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = vk, .wScan = scanCode, .dwFlags = static_cast<DWORD>(keyUp ? KEYEVENTF_KEYUP : 0) } },
            };
            mockedInputHandler.SendVirtualInput(inputs);
        }

        // Number of events of the key itself (not sent by Keyboard Manager) that reached the system
        size_t Delivered(DWORD vk, bool keyUp)
        {
            size_t count = 0;
            for (const auto& keyEvent : mockedInputHandler.GetDeliveredEvents())
            {
                if (keyEvent.vkCode == vk && keyEvent.isKeyUp == keyUp && keyEvent.dwExtraInfo == 0)
                {
                    count++;
                }
            }
            return count;
        }

        // Number of key-sequence events (key downs and key ups of the taps) that reached the system
        size_t SequenceEvents()
        {
            size_t count = 0;
            for (const auto& keyEvent : mockedInputHandler.GetDeliveredEvents())
            {
                if (keyEvent.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG)
                {
                    count++;
                }
            }
            return count;
        }

        // Makes Keyboard Manager's own injection fail while blocked is true
        void BlockInjectionWhile(const bool& blocked)
        {
            mockedInputHandler.SetSendVirtualInputShouldFail([&blocked](const std::vector<INPUT>& inputs) {
                return blocked && !inputs.empty() && inputs[0].ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
            });
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);
            remappingSuspended = false;
            mockedInputHandler.SetHookProc([this](LowlevelKeyboardEvent* data) {
                return KeyboardEventHandlers::DispatchKeyboardEvent(mockedInputHandler, data, testState, remappingSuspended);
            });
        }

        // A remap that starts to apply while its key is held (the key-down already reached the app)
        // waits for the next press: the auto-repeat must not send the sequence, and the key-up must
        // reach the app.
        TEST_METHOD (SequenceRemap_EnabledWhileKeyIsHeld_WaitsForTheNextPress)
        {
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));

            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            SendKey(VK_F1, false); // auto-repeat
            Assert::AreEqual((size_t)0, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));

            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));

            // The next press is replaced by the sequence
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));
        }

        // Reloading the settings while the key is held must not send the sequence again on the next
        // auto-repeat, and the key-up of that press stays suppressed.
        TEST_METHOD (SequenceRemap_ReloadedWhileKeyIsHeld_DoesNotSendTheSequenceAgain)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());

            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            SendKey(VK_F1, false); // auto-repeat
            Assert::AreEqual((size_t)4, SequenceEvents());

            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)0, Delivered(VK_F1, false));
            Assert::AreEqual((size_t)0, Delivered(VK_F1, true));

            // The next press sends the sequence again
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)8, SequenceEvents());
        }

        // Removing the remap while the key is held keeps the rest of that press suppressed: the app
        // never saw the key-down, so it must not get auto-repeats or a key-up for it.
        TEST_METHOD (SequenceRemap_RemovedWhileKeyIsHeld_KeepsThatPressSuppressed)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());

            ReloadSequenceRemaps({});

            SendKey(VK_F1, false); // auto-repeat
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)0, Delivered(VK_F1, false));
            Assert::AreEqual((size_t)0, Delivered(VK_F1, true));

            // The next press is an ordinary F1
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));

            // And once the remap is back, a press sends the sequence
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)8, SequenceEvents());
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));
        }

        // A press that was passed through because the injection failed leaves nothing behind when
        // the remap is removed before the key is released.
        TEST_METHOD (SequenceRemap_InjectionFailed_ThenRemapRemovedBeforeRelease_LeavesNoStaleState)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            bool blocked = true;
            BlockInjectionWhile(blocked);

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));

            ReloadSequenceRemaps({});
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));

            // With the remap back and the injection working, the next press sends the sequence
            blocked = false;
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));
        }

        // When the injection fails the original press is passed through as it is. A later handler in
        // the chain (here a Disabled remap of the same key) must not consume it.
        TEST_METHOD (SequenceRemap_InjectionFailed_PressIsNotConsumedByADisabledRemap)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            testState.AddSingleKeyRemap(VK_F1, static_cast<DWORD>(CommonSharedConstants::VK_DISABLED));
            bool blocked = true;
            BlockInjectionWhile(blocked);

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));

            SendKey(VK_F1, false); // auto-repeat
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));

            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));
            Assert::AreEqual((size_t)0, SequenceEvents());
        }

        // A key pressed while remapping is suspended reaches the app. When remapping resumes while
        // the key is still held, the rest of that press reaches the app too.
        TEST_METHOD (SequenceRemap_KeyPressedWhileRemappingIsSuspended_IsForwardedUntilItsKeyUp)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            remappingSuspended = true;
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));

            remappingSuspended = false;
            SendKey(VK_F1, false); // auto-repeat
            Assert::AreEqual((size_t)0, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));

            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_F1));

            // The next press is replaced by the sequence
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));
        }

        // A keypad key can be released under another virtual key than it was pressed with (NumPad1
        // goes down, Shift changes the state, and the key comes up as End). Both events carry the
        // same scan code, so the release must end the press and the next press must send the
        // sequence again.
        TEST_METHOD (SequenceRemap_KeypadKeyReleasedUnderAnotherVirtualKey_IsPairedByScanCode)
        {
            constexpr WORD keypad1ScanCode = 0x4F;
            mockedInputHandler.SetEncodeNumpadOrigin(true);
            ReloadSequenceRemaps({ { VK_NUMPAD1, { 0x41, 0x42 } } });

            SendKey(VK_NUMPAD1, false, keypad1ScanCode);
            Assert::AreEqual((size_t)4, SequenceEvents());

            // The release arrives as End, without the extended-key flag (a keypad key)
            SendKey(VK_END, true, keypad1ScanCode);
            Assert::AreEqual((size_t)0, Delivered(Helpers::EncodeKeyNumpadOrigin(VK_END, false), true));

            SendKey(VK_NUMPAD1, false, keypad1ScanCode);
            Assert::AreEqual((size_t)8, SequenceEvents());
            Assert::AreEqual((size_t)0, Delivered(VK_NUMPAD1, false));
        }

        // The remap is chosen by the virtual key of the first key-down: a keypad key that goes down
        // as End (no remap for it) is an ordinary press, even if it comes up as NumPad1, which has a
        // sequence remap.
        TEST_METHOD (SequenceRemap_IsChosenByTheVirtualKeyOfTheFirstKeyDown)
        {
            constexpr WORD keypad1ScanCode = 0x4F;
            mockedInputHandler.SetEncodeNumpadOrigin(true);
            ReloadSequenceRemaps({ { VK_NUMPAD1, { 0x41, 0x42 } } });
            const DWORD keypadEnd = Helpers::EncodeKeyNumpadOrigin(VK_END, false);

            SendKey(VK_END, false, keypad1ScanCode);
            Assert::AreEqual((size_t)1, Delivered(keypadEnd, false));

            SendKey(VK_NUMPAD1, false, keypad1ScanCode); // auto-repeat under the other virtual key
            SendKey(VK_NUMPAD1, true, keypad1ScanCode);
            Assert::AreEqual((size_t)0, SequenceEvents());
            Assert::AreEqual((size_t)1, Delivered(VK_NUMPAD1, false));
            Assert::AreEqual((size_t)1, Delivered(VK_NUMPAD1, true));
        }

        // Events without a scan code are paired by virtual key, and the two kinds of identity are kept apart.
        TEST_METHOD (SequencePressTracker_MakeKeyId_UsesScanCodeAndExtendedFlag_OrTheVirtualKey)
        {
            // Same physical key under two virtual keys
            Assert::IsTrue(SequencePressTracker::MakeKeyId(0x4F, false, VK_NUMPAD1) == SequencePressTracker::MakeKeyId(0x4F, false, VK_END));

            // The extended-key flag tells the keypad key from the navigation key with the same scan code
            Assert::IsTrue(SequencePressTracker::MakeKeyId(0x4F, false, VK_END) != SequencePressTracker::MakeKeyId(0x4F, true, VK_END));

            // No scan code: the virtual key decides
            Assert::IsTrue(SequencePressTracker::MakeKeyId(0, false, VK_F1) == SequencePressTracker::MakeKeyId(0, true, VK_F1));
            Assert::IsTrue(SequencePressTracker::MakeKeyId(0, false, VK_F1) != SequencePressTracker::MakeKeyId(0, false, VK_F2));

            // A virtual-key identity never equals a scan-code identity
            Assert::IsTrue(SequencePressTracker::MakeKeyId(0, false, 0x4F) != SequencePressTracker::MakeKeyId(0x4F, false, 0x4F));
        }

        // A key that is already down when the hook is installed reached the system before: its
        // auto-repeats and its key-up are let through, and its remap waits for the next press.
        TEST_METHOD (SequenceRemap_KeyAlreadyDownWhenTheHookStarts_WaitsForTheNextPress)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            mockedInputHandler.SetKeyboardState(VK_F1, true);
            RestartPressTracking();

            SendKey(VK_F1, false); // auto-repeat
            SendKey(VK_F1, false); // auto-repeat
            Assert::AreEqual((size_t)0, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));

            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));
        }

        // A key that was down when the hook was installed, and went up without an event the hook
        // tracks (Keyboard Manager itself had sent it), is not mistaken for a held key later.
        TEST_METHOD (SequenceRemap_KeyDownWhenTheHookStarts_ThenReleasedUnseen_IsANewPress)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            mockedInputHandler.SetKeyboardState(VK_F1, true);
            RestartPressTracking();
            mockedInputHandler.SetKeyboardState(VK_F1, false);

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)0, Delivered(VK_F1, false));
        }

        // A press that a sequence replaced stays suppressed when remapping is suspended in the
        // middle of it: the system never saw its key-down.
        TEST_METHOD (SequenceRemap_RemappingSuspendedWhileKeyIsHeld_KeepsThatPressSuppressed)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());

            remappingSuspended = true;
            SendKey(VK_F1, false); // auto-repeat
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)0, Delivered(VK_F1, false));
            Assert::AreEqual((size_t)0, Delivered(VK_F1, true));

            // A new press while suspended is an ordinary F1
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));
            Assert::AreEqual((size_t)1, Delivered(VK_F1, true));
        }

        // A key that the "Alone" remap took is not handed to a sequence remap that is loaded while it is held.
        TEST_METHOD (SequenceRemap_EnabledWhileAnAloneKeyIsHeld_WaitsForTheNextPress)
        {
            testState.AddSingleKeyAloneRemap(VK_F1, static_cast<DWORD>(0x43));
            ReloadSequenceRemaps({ { VK_F2, { 0x41, 0x42 } } });

            SendKey(VK_F1, false); // held as a tap candidate, suppressed
            Assert::AreEqual((size_t)0, Delivered(VK_F1, false));

            testState.ClearSingleKeyAloneRemaps();
            testState.ClearAllAloneKeyState();
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });

            SendKey(VK_F1, false); // auto-repeat
            Assert::AreEqual((size_t)0, SequenceEvents());
        }

        // Loading key-sequence remaps does not change what the hook does. Only publishing the staged
        // table does, and that happens on the hook thread, between two hook calls.
        TEST_METHOD (SequenceTable_LoadedRemaps_AreNotUsed_UntilPublished)
        {
            LoadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)0, SequenceEvents());
            Assert::AreEqual((size_t)1, Delivered(VK_F1, false));

            testState.StageSequenceTable();
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)0, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));

            Assert::IsTrue(testState.PublishStagedSequenceTable(mockedInputHandler));
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)2, Delivered(VK_F1, false));

            // The same goes for removing them: the loader clearing its table changes nothing for the hook
            LoadSequenceRemaps({});
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)8, SequenceEvents());

            testState.StageSequenceTable();
            Assert::IsTrue(testState.PublishStagedSequenceTable(mockedInputHandler));
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)8, SequenceEvents());
            Assert::AreEqual((size_t)3, Delivered(VK_F1, false));
        }

        // Publishing a new table keeps the press records: a press that began under the old table
        // ends the way it began, and the new table applies from the next press.
        TEST_METHOD (SequenceTable_Publish_KeepsPressRecords_AndAppliesFromTheNextPress)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)4, SequenceEvents());

            ReloadSequenceRemaps({ { VK_F1, { 0x43 } } });
            Assert::IsFalse(testState.SequencePresses().Empty());

            SendKey(VK_F1, false); // auto-repeat
            SendKey(VK_F1, true);
            Assert::AreEqual((size_t)4, SequenceEvents());
            Assert::AreEqual((size_t)0, Delivered(VK_F1, true));
            Assert::IsTrue(testState.SequencePresses().Empty());

            SendKey(VK_F1, false);
            Assert::AreEqual((size_t)6, SequenceEvents());
            Assert::AreEqual((DWORD)0x43, mockedInputHandler.GetDeliveredEvents().back().vkCode);
        }

        // Only the newest staged table is published, and each staged table is published once.
        TEST_METHOD (SequenceTable_Publish_TakesTheNewestStagedTable_Once)
        {
            Assert::IsFalse(testState.PublishStagedSequenceTable(mockedInputHandler));

            LoadSequenceRemaps({ { VK_F1, { 0x41 } } });
            testState.StageSequenceTable();
            LoadSequenceRemaps({ { VK_F2, { 0x42 } } });
            testState.StageSequenceTable();

            Assert::IsTrue(testState.PublishStagedSequenceTable(mockedInputHandler));
            Assert::IsFalse(testState.PublishStagedSequenceTable(mockedInputHandler));

            const auto live = testState.GetLiveSequenceTable();
            Assert::AreEqual((size_t)1, live->size());
            Assert::IsTrue(live->contains(VK_F2));
        }

        // A table that is still in use stays valid when another one is published.
        TEST_METHOD (SequenceTable_SnapshotInUse_StaysValid_AfterPublish)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            const auto inUse = testState.GetLiveSequenceTable();

            ReloadSequenceRemaps({});

            Assert::IsTrue(inUse != testState.GetLiveSequenceTable());
            Assert::IsTrue(testState.GetLiveSequenceTable()->empty());
            Assert::AreEqual((size_t)1, inUse->size());
            Assert::AreEqual((size_t)2, inUse->at(VK_F1).size());
        }

        // Without key-sequence remaps nothing is tracked. Tracking starts when a table with remaps is
        // published, and stops for new presses when the remaps are gone.
        TEST_METHOD (SequenceTable_PressesAreTrackedOnlyWhileThereAreSequenceRemaps)
        {
            Assert::IsTrue(testState.IsSequenceRemapIdle());
            SendKey(0x43, false);
            Assert::IsTrue(testState.SequencePresses().Empty());
            SendKey(0x43, true);

            ReloadSequenceRemaps({ { VK_F1, { 0x41, 0x42 } } });
            Assert::IsFalse(testState.IsSequenceRemapIdle());
            SendKey(0x43, false);
            Assert::IsFalse(testState.SequencePresses().Empty());

            // The remaps are removed while C is held: its record stays until its key-up, and new presses are not recorded
            ReloadSequenceRemaps({});
            Assert::IsFalse(testState.IsSequenceRemapIdle());
            SendKey(0x44, false);
            SendKey(0x44, true);
            SendKey(0x43, true);
            Assert::IsTrue(testState.IsSequenceRemapIdle());
            Assert::AreEqual((size_t)2, Delivered(0x43, false));
            Assert::AreEqual((size_t)2, Delivered(0x43, true));
        }

        // The events that reached the system from the given position on, as (key, is key-up) pairs.
        // Keyboard Manager's dummy key events are left out.
        using KeyTrace = std::vector<std::pair<DWORD, bool>>;
        KeyTrace DeliveredFrom(size_t first)
        {
            KeyTrace trace;
            const auto& delivered = mockedInputHandler.GetDeliveredEvents();
            for (size_t i = first; i < delivered.size(); i++)
            {
                if (delivered[i].vkCode != KeyboardManagerConstants::DUMMY_KEY)
                {
                    trace.emplace_back(delivered[i].vkCode, delivered[i].isKeyUp);
                }
            }
            return trace;
        }

        void AssertDeliveredFrom(size_t first, const KeyTrace& expected)
        {
            const KeyTrace actual = DeliveredFrom(first);
            std::wstring text;
            for (const auto& [key, isKeyUp] : actual)
            {
                text += std::to_wstring(key) + (isKeyUp ? L"u " : L"d ");
            }
            Assert::IsTrue(actual == expected, text.c_str());
        }

        // A target key that is already down is not tapped: its injected key-up would release a key
        // the sequence did not press. With Left Ctrl held, F1 -> Left Ctrl, A sends only the A tap, so A
        // arrives while Ctrl is down, and Ctrl stays down until its own key-up.
        TEST_METHOD (SequenceRemap_TargetAlreadyHeld_IsNotTapped)
        {
            ReloadSequenceRemaps({ { VK_F1, { VK_LCONTROL, 0x41 } } });

            SendKey(VK_LCONTROL, false);
            const size_t first = mockedInputHandler.GetDeliveredEvents().size();

            SendKey(VK_F1, false);
            AssertDeliveredFrom(first, { { 0x41, false }, { 0x41, true } });
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));

            SendKey(VK_F1, true);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));

            // The real key-up releases Ctrl
            SendKey(VK_LCONTROL, true);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
            AssertDeliveredFrom(first, { { 0x41, false }, { 0x41, true }, { VK_LCONTROL, true } });
        }

        // The same target is tapped as before when it is not down.
        TEST_METHOD (SequenceRemap_TargetNotHeld_IsTapped)
        {
            ReloadSequenceRemaps({ { VK_F1, { VK_LCONTROL, 0x41 } } });

            SendKey(VK_F1, false);
            AssertDeliveredFrom(0, { { VK_LCONTROL, false }, { VK_LCONTROL, true }, { 0x41, false }, { 0x41, true } });
        }

        // A target that stands for two keys is resolved first: Ctrl (either) is not tapped while
        // Right Ctrl is down, and Win (either) is not tapped while Right Win is down.
        TEST_METHOD (SequenceRemap_AliasTargetAlreadyHeld_IsNotTapped)
        {
            ReloadSequenceRemaps({ { VK_F1, { VK_CONTROL, 0x41 } }, { VK_F2, { CommonSharedConstants::VK_WIN_BOTH, 0x42 } }, { VK_F3, { VK_SHIFT, VK_MENU, 0x43 } } });

            SendKey(VK_RCONTROL, false);
            size_t first = mockedInputHandler.GetDeliveredEvents().size();
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            AssertDeliveredFrom(first, { { 0x41, false }, { 0x41, true } });
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
            SendKey(VK_RCONTROL, true);

            SendKey(VK_RWIN, false);
            first = mockedInputHandler.GetDeliveredEvents().size();
            SendKey(VK_F2, false);
            SendKey(VK_F2, true);
            AssertDeliveredFrom(first, { { 0x42, false }, { 0x42, true } });
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_RWIN));
            SendKey(VK_RWIN, true);

            // Left Shift is down, no Alt key is: only the Shift tap is left out
            SendKey(VK_LSHIFT, false);
            first = mockedInputHandler.GetDeliveredEvents().size();
            SendKey(VK_F3, false);
            SendKey(VK_F3, true);
            AssertDeliveredFrom(first, { { VK_MENU, false }, { VK_MENU, true }, { 0x43, false }, { 0x43, true } });
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LSHIFT));
        }

        // A left or right target is only left out when that very key is down: with Right Ctrl held,
        // a Left Ctrl target is still tapped, and Right Ctrl stays down.
        TEST_METHOD (SequenceRemap_OtherSideOfTargetHeld_IsStillTapped)
        {
            ReloadSequenceRemaps({ { VK_F1, { VK_LCONTROL, 0x41 } } });

            SendKey(VK_RCONTROL, false);
            const size_t first = mockedInputHandler.GetDeliveredEvents().size();
            SendKey(VK_F1, false);
            AssertDeliveredFrom(first, { { VK_LCONTROL, false }, { VK_LCONTROL, true }, { 0x41, false }, { 0x41, true } });
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_RCONTROL));
        }

        // An ordinary key that is down is left out the same way, and when no tap is left the press is
        // still replaced (nothing is sent, the source key does not reach the system).
        TEST_METHOD (SequenceRemap_EveryTargetAlreadyHeld_SendsNothing)
        {
            ReloadSequenceRemaps({ { VK_F1, { 0x41 } } });

            SendKey(0x41, false);
            const size_t first = mockedInputHandler.GetDeliveredEvents().size();
            SendKey(VK_F1, false);
            SendKey(VK_F1, true);
            AssertDeliveredFrom(first, {});
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x41));
        }
    };
}