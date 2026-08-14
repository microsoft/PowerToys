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
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.contains(0x41));

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be unchanged, and B key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x42), false);
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.empty());
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

        TEST_METHOD (RemappedKey_ShouldKeepOriginalOwnership_WhenInitialInjectionFailsButRepeatCouldSucceed)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            bool failFirstTargetDown = true;
            mockedInputHandler.SetSendVirtualInputShouldFail([&failFirstTargetDown](const std::vector<INPUT>& inputs) {
                const bool isTargetInjection = std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
                });
                if (failFirstTargetDown && isTargetInjection)
                {
                    failFirstTargetDown = false;
                    return true;
                }
                return false;
            });

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);

            const auto* press = testState.GetSingleKeyRemapPressState(0x41);
            Assert::IsNotNull(press);
            Assert::IsTrue(press->owner == SingleKeyRemapPressOwner::OriginalPassthrough);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));

            // The repeat injection would now succeed, but ownership cannot switch in the
            // middle of one physical press. The original repeat must still pass through.
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::IsTrue(testState.GetSingleKeyRemapPressState(0x41)->owner == SingleKeyRemapPressOwner::OriginalPassthrough);

            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::IsNull(testState.GetSingleKeyRemapPressState(0x41));
        }

        TEST_METHOD (RemappedKey_ShouldReleaseTarget_WhenRepeatInjectionFailsAfterInitialKeyDownSucceeds)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);

            Assert::IsTrue(testState.singleKeyRemapActiveKeys.contains(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            // The first target key-down succeeded. Fail only a later injected key-down,
            // which models a blocked auto-repeat without losing ownership of target B.
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo != 0 && (input.ki.dwFlags & KEYEVENTF_KEYUP) == 0;
                });
            });

            mockedInputHandler.SendVirtualInput(keyDown);

            Assert::IsTrue(testState.singleKeyRemapActiveKeys.contains(0x41));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);

            Assert::IsTrue(testState.singleKeyRemapActiveKeys.empty());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
        }

        TEST_METHOD (RemappedKey_ShouldRetryTargetRelease_WhenInitialReleaseInjectionFails)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG &&
                           (input.ki.dwFlags & KEYEVENTF_KEYUP) != 0;
                });
            });

            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);

            const auto* pendingPress = testState.GetSingleKeyRemapPressState(0x41);
            Assert::IsNotNull(pendingPress);
            Assert::IsTrue(pendingPress->releasePending);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            mockedInputHandler.SetSendVirtualInputShouldFail(nullptr);
            const std::vector<INPUT> unrelatedKeyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(unrelatedKeyDown);

            Assert::IsNull(testState.GetSingleKeyRemapPressState(0x41));
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.empty());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x43));
        }

        TEST_METHOD (RemappedKey_ShouldSuppressSecondPhysicalPress_UntilPendingReleaseCompletes)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG &&
                           (input.ki.dwFlags & KEYEVENTF_KEYUP) != 0;
                });
            });
            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);
            Assert::IsTrue(testState.GetSingleKeyRemapPressState(0x41)->releasePending);

            // A second physical press arrives before the old target can be released. It
            // must be swallowed as a complete pair, not mistaken for a repeat of press 1.
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::IsTrue(testState.GetSingleKeyRemapPressState(0x41)->suppressedPhysicalPressHeld);

            mockedInputHandler.SetSendVirtualInputShouldFail(nullptr);
            const std::vector<INPUT> unrelatedKeyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(unrelatedKeyDown);

            const auto* suppressedPress = testState.GetSingleKeyRemapPressState(0x41);
            Assert::IsNotNull(suppressedPress);
            Assert::IsTrue(suppressedPress->owner == SingleKeyRemapPressOwner::Suppressed);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));

            mockedInputHandler.SendVirtualInput(keyUp);
            Assert::IsNull(testState.GetSingleKeyRemapPressState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
        }

        TEST_METHOD (RemappedModifier_ShouldSuppressWholePress_WhenAuxiliaryKeyUpLandsButTargetDownFails)
        {
            testState.AddSingleKeyRemap(VK_LCONTROL, static_cast<DWORD>(0x41));
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
                });
            });

            const std::vector<INPUT> modifierDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL } },
            };
            mockedInputHandler.SendVirtualInput(modifierDown);

            const auto* suppressedPress = testState.GetSingleKeyRemapPressState(VK_LCONTROL);
            Assert::IsNotNull(suppressedPress);
            Assert::IsTrue(suppressedPress->owner == SingleKeyRemapPressOwner::Suppressed);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));

            const std::vector<INPUT> modifierUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(modifierUp);

            Assert::IsNull(testState.GetSingleKeyRemapPressState(VK_LCONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
        }

        TEST_METHOD (RemappedKeyToShortcut_ShouldSuppressPressAndReleaseOnlyInjectedPrefix_AfterPartialKeyDown)
        {
            Shortcut targetShortcut;
            targetShortcut.SetKey(VK_CONTROL);
            targetShortcut.SetKey(0x42);
            testState.AddSingleKeyRemap(0x41, targetShortcut);

            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* event) {
                return event->lParam->vkCode == 0x42 &&
                       (event->wParam == WM_KEYUP || event->wParam == WM_SYSKEYUP);
            });

            bool truncateTargetDown = true;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&truncateTargetDown](const std::vector<INPUT>& inputs) {
                const bool isTargetInjection = std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
                });
                if (truncateTargetDown && isTargetInjection && inputs.size() > 1)
                {
                    truncateTargetDown = false;
                    return static_cast<size_t>(1);
                }
                return inputs.size();
            });

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.empty());
            const auto* suppressedPress = testState.GetSingleKeyRemapPressState(0x41);
            Assert::IsNotNull(suppressedPress);
            Assert::IsTrue(suppressedPress->owner == SingleKeyRemapPressOwner::Suppressed);

            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::IsNull(testState.GetSingleKeyRemapPressState(0x41));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        TEST_METHOD (RemappedKeyToShortcut_ShouldRetryFullRelease_AfterPartialKeyUp)
        {
            Shortcut targetShortcut;
            targetShortcut.SetKey(VK_CONTROL);
            targetShortcut.SetKey(0x42);
            testState.AddSingleKeyRemap(0x41, targetShortcut);

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            bool truncateTargetRelease = true;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&truncateTargetRelease](const std::vector<INPUT>& inputs) {
                const bool isTargetRelease = std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG &&
                           (input.ki.dwFlags & KEYEVENTF_KEYUP) != 0;
                });
                if (truncateTargetRelease && isTargetRelease && inputs.size() > 1)
                {
                    truncateTargetRelease = false;
                    return static_cast<size_t>(1);
                }
                return inputs.size();
            });

            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);

            const auto* pendingPress = testState.GetSingleKeyRemapPressState(0x41);
            Assert::IsNotNull(pendingPress);
            Assert::IsTrue(pendingPress->releasePending);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));

            const std::vector<INPUT> unrelatedKeyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(unrelatedKeyDown);

            Assert::IsNull(testState.GetSingleKeyRemapPressState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldNotStartInactiveMappings)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            Shortcut sourceShortcut;
            sourceShortcut.SetKey(VK_CONTROL);
            sourceShortcut.SetKey(0x41);
            Shortcut targetShortcut;
            targetShortcut.SetKey(VK_MENU);
            targetShortcut.SetKey(0x56);
            testState.AddOSLevelShortcut(sourceShortcut, targetShortcut);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);

            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);

            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_MENU));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x56));
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.empty());
            Assert::IsFalse(testState.osLevelShortcutReMap.at(sourceShortcut).isShortcutInvoked);
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldHandleSingleKeyRepeatAndRelease)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            const std::vector<INPUT> keyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.contains(0x41));

            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            mockedInputHandler.SendVirtualInput(keyDown);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.contains(0x41));

            const std::vector<INPUT> keyUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(keyUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x41));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::IsTrue(testState.singleKeyRemapActiveKeys.empty());
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldNotStartAnotherSingleKeyRemap_WhileOneIsActive)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));
            testState.AddSingleKeyRemap(0x43, static_cast<DWORD>(0x44));

            const std::vector<INPUT> firstKeyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(firstKeyDown);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            const std::vector<INPUT> unrelatedMappedKeyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(unrelatedMappedKeyDown);

            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x43));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x44));
            Assert::IsFalse(testState.HasSingleKeyRemapPressState(0x43));
            Assert::IsTrue(testState.HasSingleKeyRemapPressState(0x41));
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldReleaseChainedShortcut_FromGeneratedSingleKeyUp)
        {
            testState.AddSingleKeyRemap(0x41, static_cast<DWORD>(0x42));

            Shortcut sourceShortcut;
            sourceShortcut.SetKey(VK_CONTROL);
            sourceShortcut.SetKey(0x42);
            testState.AddOSLevelShortcut(sourceShortcut, static_cast<DWORD>(0x43));

            // Normal routing is needed to establish the chain A -> B -> Ctrl+B -> C.
            mockedInputHandler.SetHookProc([this](LowlevelKeyboardEvent* event) {
                if (KeyboardEventHandlers::HandleSingleKeyRemapEvent(mockedInputHandler, event, testState) == 1)
                {
                    return static_cast<intptr_t>(1);
                }
                return KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(mockedInputHandler, event, testState);
            });

            const std::vector<INPUT> controlDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL } },
            };
            mockedInputHandler.SendVirtualInput(controlDown);

            const std::vector<INPUT> sourceDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };
            mockedInputHandler.SendVirtualInput(sourceDown);

            auto& chainedMapping = testState.osLevelShortcutReMap.at(sourceShortcut);
            Assert::IsTrue(chainedMapping.isShortcutInvoked);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x43));

            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            const std::vector<INPUT> sourceUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(sourceUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x43));
            Assert::IsFalse(testState.HasSingleKeyRemapPressState(0x41));

            const std::vector<INPUT> controlUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(controlUp);
            Assert::IsFalse(chainedMapping.isShortcutInvoked);
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldNotTransitionToFreshShortcut)
        {
            Shortcut activeSource;
            activeSource.SetKey(VK_CONTROL);
            activeSource.SetKey(0x41);
            Shortcut inactiveSource;
            inactiveSource.SetKey(VK_CONTROL);
            inactiveSource.SetKey(0x43);
            testState.AddOSLevelShortcut(activeSource, static_cast<DWORD>(0x42));
            testState.AddOSLevelShortcut(inactiveSource, static_cast<DWORD>(0x44));

            auto& activeMapping = testState.osLevelShortcutReMap.at(activeSource);
            activeMapping.isShortcutInvoked = true;
            activeMapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            auto& inactiveMapping = testState.osLevelShortcutReMap.at(inactiveSource);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(0x42, true);
            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            const std::vector<INPUT> inactiveActionDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(inactiveActionDown);

            Assert::IsFalse(inactiveMapping.isShortcutInvoked);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x44));
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldReleaseAllStaleOwners_OnOnePhysicalKeyUp)
        {
            const std::wstring appName = L"testprocess.exe";
            Shortcut sourceShortcut;
            sourceShortcut.SetKey(VK_CONTROL);
            sourceShortcut.SetKey(0x41);

            testState.AddAppSpecificShortcut(appName, sourceShortcut, static_cast<DWORD>(0x42));
            testState.AddOSLevelShortcut(sourceShortcut, static_cast<DWORD>(0x43));

            auto& appMapping = testState.appSpecificShortcutReMap.at(appName).at(sourceShortcut);
            appMapping.isShortcutInvoked = true;
            appMapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            auto& osMapping = testState.osLevelShortcutReMap.at(sourceShortcut);
            osMapping.isShortcutInvoked = true;
            osMapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            testState.SetActivatedApp(appName);

            mockedInputHandler.SetForegroundProcess(appName);
            mockedInputHandler.SetKeyboardState(0x42, true);
            mockedInputHandler.SetKeyboardState(0x43, true);
            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            const std::vector<INPUT> modifierUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(modifierUp);

            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x43));
            Assert::IsFalse(appMapping.isShortcutInvoked);
            Assert::IsFalse(osMapping.isShortcutInvoked);
            Assert::AreEqual(std::wstring(KeyboardManagerConstants::NoActivatedApp), testState.GetActivatedApp());
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldRetainShortcutOwnershipWhenReleaseInjectionFails)
        {
            const std::wstring appName = L"testprocess.exe";
            Shortcut sourceShortcut;
            sourceShortcut.SetKey(VK_CONTROL);
            sourceShortcut.SetKey(0x41);
            testState.AddAppSpecificShortcut(appName, sourceShortcut, static_cast<DWORD>(0x42));

            auto& mapping = testState.appSpecificShortcutReMap.at(appName).at(sourceShortcut);
            mapping.isShortcutInvoked = true;
            mapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            testState.SetActivatedApp(appName);
            mockedInputHandler.SetKeyboardState(0x42, true);
            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo != 0;
                });
            });

            const std::vector<INPUT> modifierUp{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_LCONTROL, .dwFlags = KEYEVENTF_KEYUP } },
            };
            mockedInputHandler.SendVirtualInput(modifierUp);

            Assert::IsTrue(mapping.isShortcutInvoked);
            Assert::IsTrue(mapping.modifierKeysInvoked.ctrlKey == ModifierKey::Left);
            Assert::AreEqual(appName, testState.GetActivatedApp());
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x42));

            mockedInputHandler.SetSendVirtualInputShouldFail(nullptr);
            mockedInputHandler.SendVirtualInput(modifierUp);

            Assert::IsFalse(mapping.isShortcutInvoked);
            Assert::AreEqual(std::wstring(KeyboardManagerConstants::NoActivatedApp), testState.GetActivatedApp());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x42));
        }

        TEST_METHOD (HandleActiveRemapEvent_ShouldRollbackShortcutStateWhenReprocessedInputFails)
        {
            Shortcut sourceShortcut;
            sourceShortcut.SetKey(VK_CONTROL);
            sourceShortcut.SetKey(0x41);
            Shortcut targetShortcut;
            targetShortcut.SetKey(VK_MENU);
            targetShortcut.SetKey(0x56);
            testState.AddOSLevelShortcut(sourceShortcut, targetShortcut);

            auto& mapping = testState.osLevelShortcutReMap.at(sourceShortcut);
            mapping.isShortcutInvoked = true;
            mapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            mockedInputHandler.SetKeyboardState(VK_MENU, true);
            mockedInputHandler.SetKeyboardState(0x56, true);
            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleActiveRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>& inputs) {
                return std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo != 0;
                });
            });

            const std::vector<INPUT> unrelatedKeyDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 0x42 } },
            };
            mockedInputHandler.SendVirtualInput(unrelatedKeyDown);

            Assert::IsTrue(mapping.isShortcutInvoked);
            Assert::IsTrue(mapping.modifierKeysInvoked.ctrlKey == ModifierKey::Left);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_MENU));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(0x56));
        }

        TEST_METHOD (ShortcutCase5_ShouldRollbackShortcutTransition_WhenSendInputIsPartial)
        {
            Shortcut activeSource;
            activeSource.SetKey(VK_CONTROL);
            activeSource.SetKey(0x41);
            Shortcut activeTarget;
            activeTarget.SetKey(VK_MENU);
            activeTarget.SetKey(0x42);
            Shortcut nextSource;
            nextSource.SetKey(VK_CONTROL);
            nextSource.SetKey(0x43);
            Shortcut nextTarget;
            nextTarget.SetKey(VK_SHIFT);
            nextTarget.SetKey(0x44);
            testState.AddOSLevelShortcut(activeSource, activeTarget);
            testState.AddOSLevelShortcut(nextSource, nextTarget);

            auto& activeMapping = testState.osLevelShortcutReMap.at(activeSource);
            activeMapping.isShortcutInvoked = true;
            activeMapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            activeMapping.isOriginalActionKeyPressed = true;
            const RemapShortcut activeSnapshot = activeMapping;
            const bool activeOriginalActionSnapshot = activeMapping.isOriginalActionKeyPressed;
            auto& nextMapping = testState.osLevelShortcutReMap.at(nextSource);
            const RemapShortcut nextSnapshot = nextMapping;

            mockedInputHandler.SetKeyboardState(VK_MENU, true);
            mockedInputHandler.SetKeyboardState(VK_LMENU, true);
            mockedInputHandler.SetKeyboardState(0x42, true);
            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            bool truncateTransition = true;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&truncateTransition](const std::vector<INPUT>& inputs) {
                const bool isTransition = std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
                });
                if (truncateTransition && isTransition && inputs.size() > 1)
                {
                    truncateTransition = false;
                    return (std::min)(static_cast<size_t>(3), inputs.size() - 1);
                }
                return inputs.size();
            });

            const std::vector<INPUT> nextActionDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(nextActionDown);

            Assert::IsTrue(activeMapping == activeSnapshot);
            Assert::AreEqual(activeOriginalActionSnapshot, activeMapping.isOriginalActionKeyPressed);
            Assert::IsTrue(nextMapping == nextSnapshot);
            Assert::IsFalse(nextMapping.isShortcutInvoked);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x43));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_SHIFT));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x44));
            Assert::IsFalse(testState.HasPendingInputCleanup());
        }

        TEST_METHOD (ShortcutCase5_ShouldRollbackKeyTransition_WhenSendInputIsPartial)
        {
            Shortcut activeSource;
            activeSource.SetKey(VK_CONTROL);
            activeSource.SetKey(0x41);
            Shortcut nextSource;
            nextSource.SetKey(VK_CONTROL);
            nextSource.SetKey(0x43);
            testState.AddOSLevelShortcut(activeSource, static_cast<DWORD>(0x42));
            testState.AddOSLevelShortcut(nextSource, static_cast<DWORD>(0x44));

            auto& activeMapping = testState.osLevelShortcutReMap.at(activeSource);
            activeMapping.isShortcutInvoked = true;
            activeMapping.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
            activeMapping.isOriginalActionKeyPressed = true;
            const RemapShortcut activeSnapshot = activeMapping;
            const bool activeOriginalActionSnapshot = activeMapping.isOriginalActionKeyPressed;
            auto& nextMapping = testState.osLevelShortcutReMap.at(nextSource);
            const RemapShortcut nextSnapshot = nextMapping;

            mockedInputHandler.SetKeyboardState(VK_CONTROL, false);
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, false);
            mockedInputHandler.SetKeyboardState(0x42, false);
            mockedInputHandler.SetHookProc(std::bind(
                &KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent,
                std::ref(mockedInputHandler),
                std::placeholders::_1,
                std::ref(testState)));

            bool truncateTransition = true;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&truncateTransition](const std::vector<INPUT>& inputs) {
                const bool isTransition = std::any_of(inputs.begin(), inputs.end(), [](const INPUT& input) {
                    return input.ki.dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
                });
                if (truncateTransition && isTransition && inputs.size() > 1)
                {
                    truncateTransition = false;
                    return static_cast<size_t>(1);
                }
                return inputs.size();
            });

            const std::vector<INPUT> nextActionDown{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'C' } },
            };
            mockedInputHandler.SendVirtualInput(nextActionDown);

            Assert::IsTrue(activeMapping == activeSnapshot);
            Assert::AreEqual(activeOriginalActionSnapshot, activeMapping.isOriginalActionKeyPressed);
            Assert::IsTrue(nextMapping == nextSnapshot);
            Assert::IsFalse(nextMapping.isShortcutInvoked);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x43));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_CONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_LCONTROL));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(0x44));
            Assert::IsFalse(testState.HasPendingInputCleanup());
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
        KeyboardEventHandlers::TextReplacementPreparationResult preparationResult = KeyboardEventHandlers::TextReplacementPreparationResult::Prepared;
        bool rollbackResult = true;
        size_t prepareCallCount = 0;
        size_t rollbackCallCount = 0;
        size_t isCurrentCallCount = 0;
        size_t currentCheckLimit = static_cast<size_t>(-1);
        size_t finishCallCount = 0;
        std::wstring preparedTrigger;
        bool preparedTargetContainsNewline = false;

        KeyboardEventHandlers::TextReplacementTransactionCallbacks TransactionCallbacks()
        {
            return {
                .prepare = [this](const std::wstring_view trigger, const bool targetContainsNewline) {
                    ++prepareCallCount;
                    preparedTrigger.assign(trigger);
                    preparedTargetContainsNewline = targetContainsNewline;
                    return preparationResult;
                },
                .rollback = [this]() {
                    ++rollbackCallCount;
                    return rollbackResult;
                },
                .isCurrent = [this]() {
                    ++isCurrentCallCount;
                    return isCurrentCallCount <= currentCheckLimit;
                },
                .finish = [this]() {
                    ++finishCallCount;
                },
            };
        }

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
            return KeyboardEventHandlers::HandleTextReplacementEvent(mockedInputHandler, &keyEvent, testState, TransactionCallbacks());
        }

        void PrimeTextReplacementContext()
        {
            testState.textReplacementContextTrackingEnabled.store(false, std::memory_order_relaxed);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SHIFT)));

            const uint64_t epoch = testState.textReplacementContextEpoch.load(std::memory_order_relaxed);
            testState.textReplacementContextWindow.store(testState.textReplacementWindow, std::memory_order_relaxed);
            testState.textReplacementContextProcessId.store(testState.textReplacementProcessId, std::memory_order_relaxed);
            testState.textReplacementContextStatus.store(TextReplacementContextStatus::Editable, std::memory_order_relaxed);
            testState.textReplacementClassifiedContextEpoch.store(epoch, std::memory_order_relaxed);
            testState.textReplacementObservedContextEpoch = epoch;
            testState.textReplacementContextTrackingEnabled.store(true, std::memory_order_relaxed);
        }

        void AddTextReplacement(const std::wstring& trigger, const std::wstring& replacement, const DWORD triggerKey = VK_SPACE)
        {
            Assert::IsTrue(testState.AddTextReplacement(trigger, replacement, triggerKey));
        }

        void UpdateTextReplacementToggleKey(const DWORD vkCode, const WPARAM message, const bool eventSuppressed)
        {
            KBDLLHOOKSTRUCT lParam{};
            lParam.vkCode = vkCode;
            LowlevelKeyboardEvent keyEvent{};
            keyEvent.wParam = message;
            keyEvent.lParam = &lParam;
            KeyboardEventHandlers::UpdateTextReplacementToggleKeyState(&keyEvent, eventSuppressed, testState);
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);
            preparationResult = KeyboardEventHandlers::TextReplacementPreparationResult::Prepared;
            rollbackResult = true;
            prepareCallCount = 0;
            rollbackCallCount = 0;
            isCurrentCallCount = 0;
            currentCheckLimit = static_cast<size_t>(-1);
            finishCallCount = 0;
            preparedTrigger.clear();
            preparedTargetContainsNewline = false;
        }

        TEST_METHOD (ResetTextReplacementRuntimeState_ShouldPreserveDeadAndSuppressedKeyState)
        {
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementProcessId = 42;
            testState.textReplacementWindow = reinterpret_cast<HWND>(1);
            testState.textReplacementPendingPacketHighSurrogate = static_cast<wchar_t>(0xD83D);
            testState.textReplacementDeadKeyPending = true;
            testState.textReplacementCapsLockOn = true;
            testState.textReplacementSuppressedTriggerKeys.insert(VK_SPACE);
            testState.textReplacementTriggerKeysDown.insert(VK_SPACE);
            testState.textReplacementObservedContextEpoch = 7;
            testState.textReplacementContextEpoch.store(8, std::memory_order_relaxed);

            KeyboardEventHandlers::ResetTextReplacementRuntimeState(testState);

            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<DWORD>(0), testState.textReplacementProcessId);
            Assert::IsTrue(testState.textReplacementWindow == nullptr);
            Assert::AreEqual(L'\0', testState.textReplacementPendingPacketHighSurrogate);
            Assert::IsTrue(testState.textReplacementDeadKeyPending);
            Assert::IsTrue(testState.textReplacementCapsLockOn);
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.find(VK_SPACE) != testState.textReplacementSuppressedTriggerKeys.end());
            Assert::IsTrue(testState.textReplacementTriggerKeysDown.find(VK_SPACE) != testState.textReplacementTriggerKeysDown.end());
            Assert::AreEqual(static_cast<uint64_t>(8), testState.textReplacementObservedContextEpoch);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldFailClosed_WhenContextTrackingIsUnavailable)
        {
            AddTextReplacement(L"a", L"expanded");
            testState.textReplacementBuffer = L"a";

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<size_t>(0), mockedInputHandler.GetSendVirtualInputBatchCount());
        }

        TEST_METHOD (UpdateTextReplacementToggleKeyState_ShouldTrackUnsuppressedCapsLockKeyDownOnly)
        {
            UpdateTextReplacementToggleKey(VK_CAPITAL, WM_KEYDOWN, false);
            Assert::IsTrue(testState.textReplacementCapsLockOn);
            UpdateTextReplacementToggleKey(VK_CAPITAL, WM_KEYUP, false);
            Assert::IsTrue(testState.textReplacementCapsLockOn);
            UpdateTextReplacementToggleKey(VK_CAPITAL, WM_KEYDOWN, true);
            Assert::IsTrue(testState.textReplacementCapsLockOn);
        }

        TEST_METHOD (InitializeTextReplacementToggleKeyState_ShouldRefreshCapsLockState)
        {
            const bool capsLockOn = (GetKeyState(VK_CAPITAL) & 0x1) != 0;
            testState.textReplacementCapsLockOn = !capsLockOn;
            KeyboardEventHandlers::InitializeTextReplacementToggleKeyState(testState);
            Assert::AreEqual(capsLockOn, testState.textReplacementCapsLockOn);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldWaitForConfiguredTriggerKey)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'a')));
            Assert::AreEqual(std::wstring(L"a"), testState.textReplacementBuffer);
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
            Assert::AreEqual(static_cast<size_t>(1), prepareCallCount);
            Assert::AreEqual(std::wstring(L"a"), preparedTrigger);
            Assert::IsFalse(preparedTargetContainsNewline);
            Assert::AreEqual(static_cast<size_t>(1), finishCallCount);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPassTrigger_WhenVerifiedSuffixDoesNotMatch)
        {
            AddTextReplacement(L"abc", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"abc";
            preparationResult = KeyboardEventHandlers::TextReplacementPreparationResult::NotPrepared;

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(1), prepareCallCount);
            Assert::AreEqual(std::wstring(L"abc"), preparedTrigger);
            Assert::AreEqual(static_cast<size_t>(0), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(0), finishCallCount);
            Assert::AreEqual(static_cast<size_t>(0), mockedInputHandler.GetSendVirtualInputBatchCount());
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.empty());

            // A failed ES_NUMBER/caret verification did not mutate the control, so the
            // physical trigger pair remains owned by the application.
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSuppressTrigger_WhenPreparationMayHaveChangedSelection)
        {
            AddTextReplacement(L"abc", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"abc";
            preparationResult = KeyboardEventHandlers::TextReplacementPreparationResult::CommittedFailure;

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(1), prepareCallCount);
            Assert::AreEqual(static_cast<size_t>(1), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(0), finishCallCount);
            Assert::AreEqual(static_cast<size_t>(0), mockedInputHandler.GetSendVirtualInputBatchCount());
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldUsePerMappingTriggerKeyAndPassNoMatch)
        {
            AddTextReplacement(L"a", L"expanded", VK_TAB);
            PrimeTextReplacementContext();
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'a')));

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.empty());
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());

            KeyboardEventHandlers::ResetTextReplacementRuntimeState(testState);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'a')));
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_TAB)));
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldAllowPrefixesAndChooseLongestEligibleSuffix)
        {
            AddTextReplacement(L"a", L"short");
            AddTextReplacement(L"ab", L"long");
            PrimeTextReplacementContext();

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'a')));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'b')));
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"long"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldContinueToShorterSuffixWhenLongestUsesAnotherTriggerKey)
        {
            AddTextReplacement(L"b", L"short", VK_SPACE);
            AddTextReplacement(L"ab", L"long", VK_TAB);
            PrimeTextReplacementContext();

            HandleTextReplacementKey(VK_PACKET, L'a');
            HandleTextReplacementKey(VK_PACKET, L'b');
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"short"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSuppressTriggerKeyDownRepeatsAndMatchingKeyUp)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldNotActivateFromAutoRepeat_AfterInitialTriggerWasPassed)
        {
            AddTextReplacement(L"foo ", L"expanded", VK_SPACE);
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"foo";

            // The first Space is ordinary trigger text, so it is passed through and added
            // to the prediction buffer. Its repeat must not retroactively consume this pair.
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"foo "), testState.textReplacementBuffer);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(0), prepareCallCount);
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.empty());
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));

            // Once the old physical pair has ended, a fresh down may activate an exact
            // suffix. Resetting the buffer models a new exact suffix after the repeated
            // spaces have been edited/retyped.
            testState.textReplacementBuffer = L"foo ";
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(1), prepareCallCount);
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPairNumpadEnterWithItsOwnKeyUp)
        {
            AddTextReplacement(L"a", L"expanded", VK_RETURN);
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            const DWORD numpadEnter = Helpers::EncodeKeyNumpadOrigin(VK_RETURN, true);

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(numpadEnter)));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_RETURN, 0, WM_KEYUP, LLKHF_UP)));
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(numpadEnter, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldKeepKeyUpSuppressedAcrossRuntimeReset)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));

            KeyboardEventHandlers::ResetTextReplacementRuntimeState(testState);
            testState.ClearTextReplacements();
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPassTriggerPair_WhenInjectionFailsBeforeMutation)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>&) { return true; });

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.empty());
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(static_cast<size_t>(1), prepareCallCount);
            Assert::AreEqual(static_cast<size_t>(1), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(0), finishCallCount);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSuppressTrigger_WhenBlockedInputCannotRollbackSelection)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            rollbackResult = false;
            mockedInputHandler.SetSendVirtualInputShouldFail([](const std::vector<INPUT>&) { return true; });

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(1), prepareCallCount);
            Assert::AreEqual(static_cast<size_t>(1), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(0), finishCallCount);
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldKeepTriggerPairSuppressed_WhenTargetInjectionIsPartial)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            bool truncateTarget = true;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&truncateTarget](const std::vector<INPUT>& inputs) {
                if (truncateTarget)
                {
                    truncateTarget = false;
                    return static_cast<size_t>(1);
                }
                return inputs.size();
            });

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
            Assert::AreEqual(static_cast<size_t>(0), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(1), finishCallCount);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldRollbackAndPassTrigger_WhenTransactionIsStaleBeforeFirstBatch)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            currentCheckLimit = 0;

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(1), isCurrentCallCount);
            Assert::AreEqual(static_cast<size_t>(1), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(0), finishCallCount);
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.empty());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldStopAndSuppress_WhenTransactionBecomesStaleAfterMutation)
        {
            const std::wstring replacement(20, L'x'); // More than one bounded input batch.
            AddTextReplacement(L"a", replacement);
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            currentCheckLimit = 1;

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(static_cast<size_t>(2), isCurrentCallCount);
            Assert::AreEqual(static_cast<size_t>(0), rollbackCallCount);
            Assert::AreEqual(static_cast<size_t>(1), finishCallCount);
            Assert::AreEqual(std::wstring(16, L'x'), mockedInputHandler.GetInjectedUnicodeText());
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_KEYUP, LLKHF_UP)));
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldNotActivateWithShiftOrAltGrHeld)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');
            mockedInputHandler.SetKeyboardState(VK_LSHIFT, true);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());

            mockedInputHandler.ResetKeyboardState();
            KeyboardEventHandlers::ResetTextReplacementRuntimeState(testState);
            HandleTextReplacementKey(VK_PACKET, L'a');
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_RMENU, true);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE, 0, WM_SYSKEYDOWN)));
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldCollectPacketTextWhileAltGrIsHeldAndActivateAfterRelease)
        {
            AddTextReplacement(L"x", L"expanded");
            PrimeTextReplacementContext();
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_RMENU, true);

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'x', WM_SYSKEYDOWN)));
            Assert::AreEqual(std::wstring(L"x"), testState.textReplacementBuffer);

            mockedInputHandler.SetKeyboardState(VK_LCONTROL, false);
            mockedInputHandler.SetKeyboardState(VK_RMENU, false);
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPassReturnAndTabWhenTheyDoNotMatch)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"different";

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_RETURN)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            testState.textReplacementBuffer = L"different";
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_TAB)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::IsTrue(testState.textReplacementSuppressedTriggerKeys.empty());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldNotActivateAcrossPendingDeadOrPacketState)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"a";
            testState.textReplacementDeadKeyPending = true;

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
            Assert::IsFalse(testState.textReplacementDeadKeyPending);

            testState.textReplacementBuffer = L"a";
            testState.textReplacementPendingPacketHighSurrogate = static_cast<wchar_t>(0xD83D);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
            Assert::AreEqual(L'\0', testState.textReplacementPendingPacketHighSurrogate);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldInjectExactUnicodePayload)
        {
            std::wstring replacement = L"h\u00E9llo ";
            replacement.push_back(static_cast<wchar_t>(0xD83D));
            replacement.push_back(static_cast<wchar_t>(0xDE00));
            replacement.append(L" \u6F22\u5B57");
            AddTextReplacement(L"a", replacement);
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(replacement, mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldUseBareEnterOnly_ForPreparedMultilineTarget)
        {
            AddTextReplacement(L"a", L"first line\nsecond line");
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');

            size_t shiftEventCount = 0;
            size_t enterEventCount = 0;
            mockedInputHandler.SetSendVirtualInputTestHandler([&shiftEventCount, &enterEventCount](LowlevelKeyboardEvent* event) {
                if (event->lParam->vkCode == VK_SHIFT)
                {
                    ++shiftEventCount;
                }
                if (event->lParam->vkCode == VK_RETURN)
                {
                    ++enterEventCount;
                }
                return false;
            });

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::IsTrue(preparedTargetContainsNewline);
            Assert::AreEqual(static_cast<size_t>(0), shiftEventCount);
            Assert::AreEqual(static_cast<size_t>(2), enterEventCount);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSplitLongReplacementAcrossBoundedInputBatches)
        {
            const std::wstring longReplacement(KeyboardManagerConstants::MaxTextReplacementTextLength, L'x');
            AddTextReplacement(L"a", longReplacement);
            PrimeTextReplacementContext();
            HandleTextReplacementKey(VK_PACKET, L'a');

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::IsTrue(mockedInputHandler.GetSendVirtualInputBatchCount() > 1);
            Assert::IsTrue(mockedInputHandler.GetLargestSendVirtualInputBatchSize() <= 32);
            Assert::AreEqual(longReplacement, mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldTrimBufferToLongestTriggerLength)
        {
            AddTextReplacement(L"abcd", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"abcd";

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'e')));
            Assert::AreEqual(std::wstring(L"bcde"), testState.textReplacementBuffer);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldPreserveBuffer_WhenToggleKeyIsPressed)
        {
            AddTextReplacement(L"placeholder", L"expanded");
            PrimeTextReplacementContext();
            constexpr DWORD toggleKeys[] = { VK_CAPITAL, VK_NUMLOCK, VK_SCROLL };
            for (const DWORD toggleKey : toggleKeys)
            {
                testState.textReplacementBuffer = L"partial";
                Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(toggleKey)));
                Assert::AreEqual(std::wstring(L"partial"), testState.textReplacementBuffer);
            }
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldClearDeadKeyBarrierOnBackspace)
        {
            AddTextReplacement(L"partial", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementDeadKeyPending = true;

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_BACK)));
            Assert::AreEqual(std::wstring(L"partial"), testState.textReplacementBuffer);
            Assert::IsFalse(testState.textReplacementDeadKeyPending);
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldFailOpenAndKeepDeadKeyBarrierForPacketInput)
        {
            AddTextReplacement(L"x", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementDeadKeyPending = true;

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'x')));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::IsTrue(testState.textReplacementDeadKeyPending);
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldKeepDeadKeyBarrierAcrossNonTextKey)
        {
            AddTextReplacement(L"x", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementDeadKeyPending = true;

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_RETURN)));
            Assert::IsTrue(testState.textReplacementDeadKeyPending);
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldCollectPacketTextBeforeActivation)
        {
            AddTextReplacement(L"x", L"expanded");
            PrimeTextReplacementContext();

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, L'x')));
            Assert::AreEqual(std::wstring(L"x"), testState.textReplacementBuffer);
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldCombinePacketSurrogatePairBeforeActivation)
        {
            const std::wstring emoji{ static_cast<wchar_t>(0xD83D), static_cast<wchar_t>(0xDE00) };
            AddTextReplacement(emoji, L"expanded");
            PrimeTextReplacementContext();
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* keyEvent) {
                return keyEvent->lParam->vkCode == VK_BACK;
            });

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xD83D)));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xDE00)));
            Assert::AreEqual(emoji, testState.textReplacementBuffer);
            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::AreEqual(std::wstring(L"expanded"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldSelectEmojiZwjSuffix_WithoutBackspaceFallback)
        {
            std::wstring familyEmoji;
            familyEmoji.push_back(static_cast<wchar_t>(0xD83D));
            familyEmoji.push_back(static_cast<wchar_t>(0xDC68));
            familyEmoji.push_back(static_cast<wchar_t>(0x200D));
            familyEmoji.push_back(static_cast<wchar_t>(0xD83D));
            familyEmoji.push_back(static_cast<wchar_t>(0xDC69));
            familyEmoji.push_back(static_cast<wchar_t>(0x200D));
            familyEmoji.push_back(static_cast<wchar_t>(0xD83D));
            familyEmoji.push_back(static_cast<wchar_t>(0xDC67));
            familyEmoji.push_back(static_cast<wchar_t>(0x200D));
            familyEmoji.push_back(static_cast<wchar_t>(0xD83D));
            familyEmoji.push_back(static_cast<wchar_t>(0xDC66));

            AddTextReplacement(familyEmoji, L"family");
            PrimeTextReplacementContext();
            for (const wchar_t unit : familyEmoji)
            {
                Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, unit)));
            }
            Assert::AreEqual(familyEmoji, testState.textReplacementBuffer);

            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* event) {
                return event->lParam->vkCode == VK_BACK;
            });

            Assert::AreEqual(1, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(familyEmoji, preparedTrigger);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::AreEqual(std::wstring(L"family"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldRejectMalformedPacketSurrogateSequence)
        {
            const std::wstring emoji{ static_cast<wchar_t>(0xD83D), static_cast<wchar_t>(0xDE00) };
            AddTextReplacement(emoji, L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"prefix";

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xD83D)));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xD834)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_PACKET, 0xDE00)));
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (HandleTextReplacementEvent_ShouldClearBuffer_WhenContextIsInvalidatedOrStale)
        {
            AddTextReplacement(L"a", L"expanded");
            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.InvalidateTextReplacementContext();

            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SHIFT)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);

            PrimeTextReplacementContext();
            testState.textReplacementBuffer = L"partial";
            testState.textReplacementClassifiedContextEpoch.store(
                testState.textReplacementContextEpoch.load(std::memory_order_relaxed) - 1,
                std::memory_order_relaxed);
            Assert::AreEqual(0, static_cast<int>(HandleTextReplacementKey(VK_SPACE)));
            Assert::AreEqual(std::wstring(), testState.textReplacementBuffer);
        }
    };
}
