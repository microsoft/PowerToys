#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>
#include <keyboardmanager/common/KeyboardEventHandlers.h>
#include "TestHelpers.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for MockedInput test helper - to ensure simulated keyboard input behaves as expected
    TEST_CLASS (MockedInputSanityTests)
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

        // Test if mocked input is working
        TEST_METHOD (MockedInput_ShouldSetKeyboardState_OnKeyEvent)
        {
            // Send key down and key up for A key (0x41) and check keyboard state both times
            std::vector<INPUT> inputs1{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            };

            // Send A keydown
            mockedInputHandler.SendVirtualInput(inputs1);

            // A key state should be true
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), true);

            std::vector<INPUT> inputs2{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Send A keyup
            mockedInputHandler.SendVirtualInput(inputs2);

            // A key state should be false
            Assert::AreEqual(mockedInputHandler.GetVirtualKeyState(0x41), false);
        }

        TEST_METHOD (MockedInput_ShouldReportAndDeliverExactInjectedPrefix)
        {
            mockedInputHandler.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) {
                return static_cast<size_t>(1);
            });
            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'B' } },
            };

            const auto result = mockedInputHandler.SendVirtualInput(inputs);

            Assert::AreEqual(
                static_cast<int>(KeyboardManagerInput::SendVirtualInputStatus::Partial),
                static_cast<int>(result.status));
            Assert::AreEqual(static_cast<UINT>(1), result.injectedEventCount);
            Assert::IsTrue(mockedInputHandler.GetVirtualKeyState('A'));
            Assert::IsFalse(mockedInputHandler.GetVirtualKeyState('B'));
        }

        TEST_METHOD (MockedInput_ShouldTranslateScanCodeInputForHook)
        {
            KBDLLHOOKSTRUCT observed{};
            mockedInputHandler.SetHookProc([&](LowlevelKeyboardEvent* event) {
                if (event && event->lParam)
                {
                    observed = *event->lParam;
                }
                return 0;
            });
            const std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wScan = 0x1E, .dwFlags = KEYEVENTF_SCANCODE } },
            };

            const auto result = mockedInputHandler.SendVirtualInput(inputs);

            Assert::IsTrue(result.IsComplete());
            Assert::AreEqual(static_cast<DWORD>('A'), observed.vkCode);
            Assert::AreEqual(static_cast<DWORD>(0x1E), observed.scanCode);
            Assert::IsFalse((observed.flags & LLKHF_EXTENDED) != 0);
            Assert::IsTrue(mockedInputHandler.GetVirtualKeyState('A'));
        }

        TEST_METHOD (MockedInput_ShouldExposeUnicodeInputAsPacketToHook)
        {
            KBDLLHOOKSTRUCT observed{};
            mockedInputHandler.SetHookProc([&](LowlevelKeyboardEvent* event) {
                if (event && event->lParam)
                {
                    observed = *event->lParam;
                }
                return 0;
            });
            const std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wScan = static_cast<WORD>(L'x'), .dwFlags = KEYEVENTF_UNICODE } },
            };

            const auto result = mockedInputHandler.SendVirtualInput(inputs);

            Assert::IsTrue(result.IsComplete());
            Assert::AreEqual(static_cast<DWORD>(VK_PACKET), observed.vkCode);
            Assert::AreEqual(static_cast<DWORD>(L'x'), observed.scanCode);
            Assert::AreEqual(std::wstring(L"x"), mockedInputHandler.GetInjectedUnicodeText());
        }

        TEST_METHOD (MockedInput_ShouldReportNoneWhenNoEventsAreInjected)
        {
            mockedInputHandler.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) {
                return static_cast<size_t>(0);
            });

            const auto result = mockedInputHandler.SendVirtualInput({
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
            });

            Assert::AreEqual(
                static_cast<int>(KeyboardManagerInput::SendVirtualInputStatus::None),
                static_cast<int>(result.status));
            Assert::AreEqual(static_cast<UINT>(0), result.injectedEventCount);
            Assert::IsFalse(mockedInputHandler.GetVirtualKeyState('A'));
        }
    };
}
