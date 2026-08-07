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

    TEST_CLASS (TextReplacementTests)
    {
    private:
        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;

        intptr_t HandleTextReplacementKey(
            const DWORD vkCode,
            const DWORD scanCode = 0,
            const WPARAM message = WM_KEYDOWN,
            const DWORD flags = 0,
            const ULONG_PTR extraInfo = 0)
        {
            KBDLLHOOKSTRUCT lParam{};
            lParam.vkCode = vkCode;
            lParam.scanCode = scanCode;
            lParam.flags = flags;
            lParam.dwExtraInfo = extraInfo;

            LowlevelKeyboardEvent keyEvent{};
            keyEvent.wParam = message;
            keyEvent.lParam = &lParam;

            return KeyboardEventHandlers::HandleTextReplacementEvent(mockedInputHandler, &keyEvent, testState);
        }

        void PrimeTextReplacementContext()
        {
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SHIFT)));
        }

        void UpdateTextReplacementToggleKey(const DWORD vkCode, const WPARAM message, const bool eventSuppressed, const ULONG_PTR extraInfo = 0)
        {
            KBDLLHOOKSTRUCT lParam{};
            lParam.vkCode = vkCode;
            lParam.dwExtraInfo = extraInfo;
            LowlevelKeyboardEvent keyEvent{};
            keyEvent.wParam = message;
            keyEvent.lParam = &lParam;

            KeyboardEventHandlers::UpdateTextReplacementToggleKeyState(&keyEvent, eventSuppressed, testState);
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);
        }

        TEST_METHOD (ResetTextReplacementRuntimeState_ShouldClearContextBoundStateAndPreserveGlobalState)
        {
            const HWND runtimeWindow = reinterpret_cast<HWND>(1);
            const HKL deadKeyLayout = reinterpret_cast<HKL>(2);

            testState.textReplacementBuffer = L"partial";
            testState.textReplacementProcessId = 42;
            testState.textReplacementWindow = runtimeWindow;
            testState.textReplacementPendingPacketHighSurrogate = static_cast<wchar_t>(0xD83D);
            testState.textReplacementDeadKeyPending = true;
            testState.textReplacementDeadKeyMustPassThrough = false;
            testState.textReplacementDeadKeyThreadId = 43;
            testState.textReplacementDeadKeyLayout = deadKeyLayout;
            testState.textReplacementCapsLockOn = true;
            testState.textReplacementNumLockOn = true;
            testState.textReplacementObservedContextEpoch = 7;
            testState.textReplacementContextEpoch.store(8, std::memory_order_relaxed);

            KeyboardEventHandlers::ResetTextReplacementRuntimeState(testState);

            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<DWORD>(0), testState.textReplacementProcessId);
            Assert::IsTrue(testState.textReplacementWindow == nullptr);
            Assert::AreEqual(L'\0', testState.textReplacementPendingPacketHighSurrogate);
            Assert::AreEqual(true, testState.textReplacementDeadKeyPending);
            Assert::AreEqual(true, testState.textReplacementDeadKeyMustPassThrough);
            Assert::AreEqual(static_cast<DWORD>(43), testState.textReplacementDeadKeyThreadId);
            Assert::IsTrue(testState.textReplacementDeadKeyLayout == deadKeyLayout);
            Assert::AreEqual(static_cast<uint64_t>(8), testState.textReplacementObservedContextEpoch);
            Assert::AreEqual(true, testState.textReplacementCapsLockOn);
            Assert::AreEqual(true, testState.textReplacementNumLockOn);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldFailOpenAfterDeadKeyContextChange)
        {
            testState.AddTextReplacement(L" ", L"expanded");
            PrimeTextReplacementContext();

            const HWND foregroundWindow = GetForegroundWindow();
            const DWORD foregroundThread = foregroundWindow ? GetWindowThreadProcessId(foregroundWindow, nullptr) : 0;
            testState.textReplacementDeadKeyPending = true;
            testState.textReplacementDeadKeyThreadId = foregroundThread;
            testState.textReplacementDeadKeyLayout = GetKeyboardLayout(foregroundThread);
            KeyboardEventHandlers::ResetTextReplacementRuntimeState(testState);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(false, testState.textReplacementDeadKeyPending);
            Assert::AreEqual(false, testState.textReplacementDeadKeyMustPassThrough);
            Assert::AreEqual(static_cast<size_t>(0), mockedInputHandler.GetSendVirtualInputBatchCount());

            testState.textReplacementDeadKeyPending = true;
            testState.textReplacementDeadKeyThreadId = foregroundThread + 1;
            testState.textReplacementDeadKeyLayout = GetKeyboardLayout(foregroundThread);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(false, testState.textReplacementDeadKeyPending);
            Assert::AreEqual(false, testState.textReplacementDeadKeyMustPassThrough);
            Assert::AreEqual(static_cast<size_t>(0), mockedInputHandler.GetSendVirtualInputBatchCount());
        }

        TEST_METHOD (UpdateTextReplacementToggleKeyState_ShouldTrackUnsuppressedKeyDownOnly)
        {
            UpdateTextReplacementToggleKey(VK_CAPITAL, WM_KEYDOWN, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            Assert::AreEqual(true, testState.textReplacementCapsLockOn);

            UpdateTextReplacementToggleKey(VK_CAPITAL, WM_KEYUP, false);
            Assert::AreEqual(true, testState.textReplacementCapsLockOn);

            UpdateTextReplacementToggleKey(VK_CAPITAL, WM_KEYDOWN, false);
            Assert::AreEqual(false, testState.textReplacementCapsLockOn);

            UpdateTextReplacementToggleKey(VK_NUMLOCK, WM_KEYDOWN, true);
            Assert::AreEqual(false, testState.textReplacementNumLockOn);

            UpdateTextReplacementToggleKey(VK_NUMLOCK, WM_KEYDOWN, false);
            Assert::AreEqual(true, testState.textReplacementNumLockOn);
        }

        TEST_METHOD (InitializeTextReplacementToggleKeyState_ShouldRefreshStateOnEveryCall)
        {
            const bool capsLockOn = (GetKeyState(VK_CAPITAL) & 0x1) != 0;
            const bool numLockOn = (GetKeyState(VK_NUMLOCK) & 0x1) != 0;

            testState.textReplacementCapsLockOn = !capsLockOn;
            testState.textReplacementNumLockOn = !numLockOn;

            KeyboardEventHandlers::InitializeTextReplacementToggleKeyState(testState);

            Assert::AreEqual(capsLockOn, testState.textReplacementCapsLockOn);
            Assert::AreEqual(numLockOn, testState.textReplacementNumLockOn);

            testState.textReplacementCapsLockOn = !capsLockOn;
            testState.textReplacementNumLockOn = !numLockOn;
            KeyboardEventHandlers::InitializeTextReplacementToggleKeyState(testState);

            Assert::AreEqual(capsLockOn, testState.textReplacementCapsLockOn);
            Assert::AreEqual(numLockOn, testState.textReplacementNumLockOn);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldClearBuffer_WhenInputContextIsInvalidated)
        {
            testState.AddTextReplacement(L"    ", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementContextTrackingEnabled.store(true, std::memory_order_relaxed);
            testState.textReplacementContextStatus.store(TextReplacementContextStatus::Editable, std::memory_order_relaxed);
            testState.textReplacementContextWindow.store(testState.textReplacementWindow, std::memory_order_relaxed);
            testState.textReplacementContextProcessId.store(testState.textReplacementProcessId, std::memory_order_relaxed);
            testState.textReplacementObservedContextEpoch = testState.textReplacementContextEpoch.load(std::memory_order_relaxed);
            testState.textReplacementClassifiedContextEpoch.store(testState.textReplacementObservedContextEpoch, std::memory_order_relaxed);

            testState.InvalidateTextReplacementContext();

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SHIFT)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::IsTrue(testState.textReplacementContextStatus.load(std::memory_order_relaxed) == TextReplacementContextStatus::Pending);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldRejectStaleEditableClassification)
        {
            testState.AddTextReplacement(L" ", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementContextTrackingEnabled.store(true, std::memory_order_relaxed);
            testState.textReplacementContextStatus.store(TextReplacementContextStatus::Editable, std::memory_order_relaxed);
            testState.textReplacementContextWindow.store(testState.textReplacementWindow, std::memory_order_relaxed);
            testState.textReplacementContextProcessId.store(testState.textReplacementProcessId, std::memory_order_relaxed);
            testState.textReplacementObservedContextEpoch = 5;
            testState.textReplacementContextEpoch.store(5, std::memory_order_relaxed);
            testState.textReplacementClassifiedContextEpoch.store(4, std::memory_order_relaxed);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<size_t>(0), mockedInputHandler.GetSendVirtualInputBatchCount());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldInjectExactUnicodePayload)
        {
            std::wstring replacement = L"h\u00E9llo ";
            replacement.push_back(static_cast<wchar_t>(0xD83D));
            replacement.push_back(static_cast<wchar_t>(0xDE00));
            replacement.append(L" \u6F22\u5B57");
            testState.AddTextReplacement(L" ", replacement);

            KBDLLHOOKSTRUCT lParam{};
            lParam.vkCode = VK_SPACE;
            LowlevelKeyboardEvent keyEvent{};
            keyEvent.wParam = WM_KEYDOWN;
            keyEvent.lParam = &lParam;

            intptr_t result = KeyboardEventHandlers::HandleTextReplacementEvent(mockedInputHandler, &keyEvent, testState);

            Assert::AreEqual(1, static_cast<int>(result));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(replacement, mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldClearBufferAndIgnoreInput_WhenShortcutModifierIsPressed)
        {
            testState.AddTextReplacement(L" ", L"hello");
            testState.textReplacementBuffer = L"partial";
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);

            KBDLLHOOKSTRUCT lParam{};
            lParam.vkCode = VK_SPACE;
            LowlevelKeyboardEvent keyEvent{};
            keyEvent.wParam = WM_KEYDOWN;
            keyEvent.lParam = &lParam;

            intptr_t result = KeyboardEventHandlers::HandleTextReplacementEvent(mockedInputHandler, &keyEvent, testState);

            Assert::AreEqual(0, static_cast<int>(result));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldReplaceMultiCharacterTriggerAndBackspacePreviousCharacters)
        {
            testState.AddTextReplacement(L"  ", L"expanded");
            PrimeTextReplacementContext();
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* keyEvent) {
                return keyEvent->lParam->vkCode == VK_BACK;
            });

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L" "), testState.textReplacementBuffer);

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldTrimBufferToLongestTriggerLength)
        {
            testState.AddTextReplacement(L"    ", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"abcd";

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"bcd "), testState.textReplacementBuffer);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPassOriginalKeyThrough_WhenReplacementInjectionFails)
        {
            testState.AddTextReplacement(L" ", L"replacement");
            PrimeTextReplacementContext();
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return (input.ki.dwFlags & KEYEVENTF_UNICODE) != 0;
                });
            });

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<size_t>(1), mockedInputHandler.GetSendVirtualInputBatchCount());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSplitLongReplacementAcrossBoundedInputBatches)
        {
            const std::wstring longReplacement(KeyboardManagerConstants::MaxTextReplacementTextLength, L'x');
            Assert::IsTrue(testState.AddTextReplacement(L" ", longReplacement));
            PrimeTextReplacementContext();

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::IsTrue(mockedInputHandler.GetSendVirtualInputBatchCount() > 1);
            Assert::IsTrue(mockedInputHandler.GetLargestSendVirtualInputBatchSize() <= 32);
            Assert::AreEqual(longReplacement, mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSuppressOriginalKey_WhenLaterInputBatchFails)
        {
            const std::wstring longReplacement(KeyboardManagerConstants::MaxTextReplacementTextLength, L'x');
            Assert::IsTrue(testState.AddTextReplacement(L" ", longReplacement));
            PrimeTextReplacementContext();
            mockedInputHandler.SetKeyboardState(VK_LSHIFT, true);
            size_t attemptedBatchCount = 0;
            mockedInputHandler.SetSendVirtualInputShouldFail([&attemptedBatchCount](const std::vector<INPUT>&) {
                return ++attemptedBatchCount == 3;
            });

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<size_t>(4), mockedInputHandler.GetSendVirtualInputBatchCount());
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LSHIFT));
            const std::wstring& injectedText = mockedInputHandler.GetInjectedUnicodeText();
            Assert::IsTrue(!injectedText.empty());
            Assert::IsTrue(injectedText.size() < longReplacement.size());
            Assert::AreEqual(longReplacement.substr(0, injectedText.size()), injectedText);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldClearBuffer_WhenBackspaceHasShortcutOrAltGrModifier)
        {
            testState.AddTextReplacement(L"placeholder", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_BACK)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);

            mockedInputHandler.ResetKeyboardState();
            testState.textReplacementBuffer = L"partial";
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_RMENU, true);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_BACK, 0, WM_SYSKEYDOWN)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPreserveBuffer_WhenToggleKeyIsPressed)
        {
            testState.AddTextReplacement(L"placeholder", L"expanded");
            PrimeTextReplacementContext();
            constexpr DWORD toggleKeys[] = { VK_CAPITAL, VK_NUMLOCK, VK_SCROLL };
            for (const DWORD toggleKey : toggleKeys)
            {
                testState.textReplacementBuffer = L"partial";
                Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(toggleKey)));
                Assert::AreEqual(std::wstring(L"partial"), testState.textReplacementBuffer);
            }
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldCancelPendingDeadKeyBeforeRemovingBufferedText_OnBackspace)
        {
            testState.AddTextReplacement(L"placeholder", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementDeadKeyPending = true;
            testState.textReplacementDeadKeyThreadId = 42;
            testState.textReplacementDeadKeyLayout = reinterpret_cast<HKL>(1);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_BACK)));
            Assert::AreEqual(std::wstring(L"partial"), testState.textReplacementBuffer);
            Assert::AreEqual(false, testState.textReplacementDeadKeyPending);
            Assert::AreEqual(static_cast<DWORD>(0), testState.textReplacementDeadKeyThreadId);
            Assert::IsTrue(testState.textReplacementDeadKeyLayout == nullptr);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldRestorePressedShift_WhenTriggerMatches)
        {
            testState.AddTextReplacement(L" ", L"expanded");
            PrimeTextReplacementContext();
            mockedInputHandler.SetKeyboardState(VK_LSHIFT, true);
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* keyEvent) {
                const DWORD vkCode = keyEvent->lParam->vkCode;
                const bool isShift = vkCode == VK_SHIFT || vkCode == VK_LSHIFT || vkCode == VK_RSHIFT;
                return isShift && keyEvent->wParam == WM_KEYDOWN;
            });

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(1, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_LSHIFT));
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldUseUnicodeCodeUnitFromPacketScanCode)
        {
            testState.AddTextReplacement(L"x", L"expanded");
            PrimeTextReplacementContext();

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'x')));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldCombinePacketSurrogatePairBeforeMatching)
        {
            const std::wstring emoji{ static_cast<wchar_t>(0xD83D), static_cast<wchar_t>(0xDE00) };
            testState.AddTextReplacement(emoji, L"expanded");
            PrimeTextReplacementContext();
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* keyEvent) {
                return keyEvent->lParam->vkCode == VK_BACK;
            });

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xD83D)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xD83D, WM_KEYUP, LLKHF_UP)));

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xDE00)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }
    };
}
