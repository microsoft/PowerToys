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

        // Test that the hook sees the scan code and the extended-key flag, and that only the events it lets through are recorded
        TEST_METHOD (MockedInput_ShouldPassScanCodeAndExtendedFlag_AndRecordDeliveredEvents)
        {
            DWORD seenScanCode = 0;
            DWORD seenFlags = 0;
            mockedInputHandler.SetHookProc([&](LowlevelKeyboardEvent* data) -> intptr_t {
                seenScanCode = data->lParam->scanCode;
                seenFlags = data->lParam->flags;

                // Suppress B, let everything else through
                return data->lParam->vkCode == 'B' ? 1 : 0;
            });

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'B', .wScan = 0x30 } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = VK_RIGHT, .wScan = 0x4D, .dwFlags = KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, .dwExtraInfo = 0x1234 } },
            };
            mockedInputHandler.SendVirtualInput(inputs);

            Assert::AreEqual((DWORD)0x4D, seenScanCode);
            Assert::IsTrue((seenFlags & LLKHF_EXTENDED) != 0);

            const auto& delivered = mockedInputHandler.GetDeliveredEvents();
            Assert::AreEqual((size_t)1, delivered.size());
            Assert::AreEqual((DWORD)VK_RIGHT, delivered[0].vkCode);
            Assert::AreEqual(true, delivered[0].isKeyUp);
            Assert::AreEqual((DWORD)0x4D, delivered[0].scanCode);
            Assert::IsTrue(delivered[0].dwExtraInfo == 0x1234);
        }

        // Test that input sent from inside the hook is nested by default, and goes after the event being handled in queued mode
        TEST_METHOD (MockedInput_ShouldDeliverInputSentFromTheHookAfterTheCurrentEvent_InQueuedMode)
        {
            // On an A key down, the hook sends B and lets A through
            mockedInputHandler.SetHookProc([&](LowlevelKeyboardEvent* data) -> intptr_t {
                if (data->lParam->vkCode == 'A' && data->wParam == WM_KEYDOWN)
                {
                    std::vector<INPUT> fromHook{
                        { .type = INPUT_KEYBOARD, .ki = { .wVk = 'B' } },
                    };
                    mockedInputHandler.SendVirtualInput(fromHook);
                }
                return 0;
            });

            std::vector<INPUT> inputs{
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A' } },
                { .type = INPUT_KEYBOARD, .ki = { .wVk = 'A', .dwFlags = KEYEVENTF_KEYUP } },
            };

            // Nested (default): B reaches the system before the A key down that caused it
            mockedInputHandler.SendVirtualInput(inputs);
            {
                const auto& delivered = mockedInputHandler.GetDeliveredEvents();
                Assert::AreEqual((size_t)3, delivered.size());
                Assert::AreEqual((DWORD)'B', delivered[0].vkCode);
                Assert::AreEqual((DWORD)'A', delivered[1].vkCode);
                Assert::AreEqual((DWORD)'A', delivered[2].vkCode);
            }

            // Queued: B comes after everything that was already in the queue, like SendInput
            mockedInputHandler.ClearDeliveredEvents();
            mockedInputHandler.SetQueuedDelivery(true);
            mockedInputHandler.SendVirtualInput(inputs);
            {
                const auto& delivered = mockedInputHandler.GetDeliveredEvents();
                Assert::AreEqual((size_t)3, delivered.size());
                Assert::AreEqual((DWORD)'A', delivered[0].vkCode);
                Assert::AreEqual(false, delivered[0].isKeyUp);
                Assert::AreEqual((DWORD)'A', delivered[1].vkCode);
                Assert::AreEqual(true, delivered[1].isKeyUp);
                Assert::AreEqual((DWORD)'B', delivered[2].vkCode);
            }
        }
    };
}
