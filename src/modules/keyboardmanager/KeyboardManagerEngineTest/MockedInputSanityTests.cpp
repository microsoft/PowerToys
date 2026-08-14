#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include <keyboardmanager/common/KeyboardEventHandlers.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
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

        TEST_METHOD (MockedInput_ShouldReportAndDeliverOnlyPartialPrefix)
        {
            mockedInputHandler.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) {
                return static_cast<size_t>(1);
            });
            const std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'B' } },
            };

            const auto result = mockedInputHandler.SendVirtualInput(inputs);

            Assert::IsTrue(result.status == KeyboardManagerInput::SendVirtualInputStatus::Partial);
            Assert::AreEqual(static_cast<size_t>(1), result.injectedEventCount);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState('A'));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState('B'));
        }

        TEST_METHOD (SendTextInput_ShouldStopAndReleaseHeldPrefix_OnPartialInjection)
        {
            bool truncateFirstBatch = true;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&truncateFirstBatch](const std::vector<INPUT>& inputs) {
                if (truncateFirstBatch)
                {
                    truncateFirstBatch = false;
                    return static_cast<size_t>(1);
                }
                return inputs.size();
            });

            std::vector<INPUT> pendingInputCleanup;
            const auto result = Helpers::SendTextInput(L"\ntext that must not follow", mockedInputHandler, pendingInputCleanup);

            Assert::IsTrue(result.status == KeyboardManagerInput::SendVirtualInputStatus::Partial);
            Assert::AreEqual(static_cast<size_t>(1), result.injectedEventCount);
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_SHIFT));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState(VK_RETURN));
            Assert::AreEqual(std::wstring(), mockedInputHandler.GetInjectedUnicodeText());
            Assert::IsTrue(pendingInputCleanup.empty());
        }

        TEST_METHOD (SendTextInput_ShouldReturnExactCleanupSuffix_WhenImmediateCleanupIsBlocked)
        {
            size_t attempt = 0;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&attempt](const std::vector<INPUT>& inputs) {
                ++attempt;
                if (attempt == 1)
                {
                    return static_cast<size_t>(1); // Shift-down only.
                }
                if (attempt == 2)
                {
                    return static_cast<size_t>(0); // Immediate Shift-up cleanup blocked.
                }
                return inputs.size();
            });

            std::vector<INPUT> pendingInputCleanup;
            const auto result = Helpers::SendTextInput(L"\ntext that must not follow", mockedInputHandler, pendingInputCleanup);

            Assert::IsTrue(result.status == KeyboardManagerInput::SendVirtualInputStatus::Partial);
            Assert::AreEqual(static_cast<size_t>(1), pendingInputCleanup.size());
            Assert::AreEqual(static_cast<WORD>(VK_SHIFT), pendingInputCleanup.front().ki.wVk);
            Assert::IsTrue((pendingInputCleanup.front().ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState(VK_SHIFT));
        }

        TEST_METHOD (PendingInputCleanup_ShouldAdvanceExactlyAcrossNonePartialAndCompleteRetries)
        {
            std::vector<INPUT> cleanupEvents;
            for (const WORD key : { static_cast<WORD>('A'), static_cast<WORD>('B'), static_cast<WORD>('C') })
            {
                mockedInputHandler.SetKeyboardState(key, true);
                INPUT keyUp{};
                keyUp.type = INPUT_KEYBOARD;
                keyUp.ki.wVk = key;
                keyUp.ki.dwFlags = KEYEVENTF_KEYUP;
                keyUp.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
                cleanupEvents.push_back(keyUp);
            }
            testState.QueuePendingInputCleanup(std::move(cleanupEvents));

            size_t attempt = 0;
            mockedInputHandler.SetSendVirtualInputInjectedCount([&attempt](const std::vector<INPUT>& inputs) {
                ++attempt;
                if (attempt == 1)
                {
                    return static_cast<size_t>(0);
                }
                if (attempt == 2)
                {
                    return static_cast<size_t>(1);
                }
                return inputs.size();
            });

            const auto noneResult = KeyboardEventHandlers::RetryPendingInputCleanup(mockedInputHandler, testState);
            Assert::IsTrue(noneResult.status == KeyboardManagerInput::SendVirtualInputStatus::None);
            Assert::AreEqual(static_cast<size_t>(0), noneResult.injectedEventCount);
            Assert::IsTrue(testState.HasPendingInputCleanup());
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState('A'));

            const auto partialResult = KeyboardEventHandlers::RetryPendingInputCleanup(mockedInputHandler, testState);
            Assert::IsTrue(partialResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial);
            Assert::AreEqual(static_cast<size_t>(1), partialResult.injectedEventCount);
            Assert::IsTrue(testState.HasPendingInputCleanup());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState('A'));
            Assert::AreEqual(true, mockedInputHandler.GetVirtualKeyState('B'));

            const auto completeResult = KeyboardEventHandlers::RetryPendingInputCleanup(mockedInputHandler, testState);
            Assert::IsTrue(completeResult.status == KeyboardManagerInput::SendVirtualInputStatus::Complete);
            Assert::AreEqual(static_cast<size_t>(2), completeResult.injectedEventCount);
            Assert::IsFalse(testState.HasPendingInputCleanup());
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState('B'));
            Assert::AreEqual(false, mockedInputHandler.GetVirtualKeyState('C'));
        }
    };
}
