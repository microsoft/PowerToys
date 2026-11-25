#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "..\AdvancedPasteModuleInterface\HotkeyWork.h"
#include <atomic>
#include <future>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace std::chrono_literals;

namespace AdvancedPasteModuleInterfaceUnitTests
{
    TEST_CLASS (HotkeyWorkTests)
    {
    public:
        TEST_METHOD (RepeatsAreDiscardedAndStopCancelsPendingWorkAndJoinsActiveWork)
        {
            std::promise<void> entered;
            auto started = entered.get_future();
            std::atomic_int completed = 0;
            advanced_paste::HotkeyWork worker;
            worker.start();
            Assert::IsTrue(worker.submit(0, [&](std::stop_token token) {
                entered.set_value();
                while (!token.stop_requested())
                {
                    std::this_thread::sleep_for(1ms);
                }
                ++completed;
            }));
            Assert::IsTrue(started.wait_for(5s) == std::future_status::ready);

            for (int repeat = 0; repeat < 1000; ++repeat)
            {
                Assert::IsFalse(worker.submit(0, [&](std::stop_token) { ++completed; }));
            }

            Assert::IsTrue(worker.submit(1, [&](std::stop_token) { ++completed; }));
            Assert::IsFalse(worker.submit(2, [&](std::stop_token) { ++completed; }));
            worker.stop();
            Assert::AreEqual(1, completed.load());
            Assert::IsFalse(worker.submit(0, [&](std::stop_token) { ++completed; }));
        }

        TEST_METHOD (DisableAndReenableDoNotReplayOldWork)
        {
            advanced_paste::HotkeyWork worker;
            worker.start();
            worker.stop();
            Assert::IsFalse(worker.submit(0, [](std::stop_token) {}));

            std::promise<void> entered;
            auto started = entered.get_future();
            worker.start();
            Assert::IsTrue(worker.submit(0, [&](std::stop_token) { entered.set_value(); }));
            Assert::IsTrue(started.wait_for(5s) == std::future_status::ready);
            worker.stop();
        }

        TEST_METHOD (DestructionCancelsAndJoinsTheWorker)
        {
            std::promise<void> entered;
            auto started = entered.get_future();
            std::atomic_bool cancelled = false;
            {
                advanced_paste::HotkeyWork worker;
                worker.start();
                Assert::IsTrue(worker.submit(0, [&](std::stop_token token) {
                    entered.set_value();
                    cancelled = !advanced_paste::wait_for_modifiers_release(token, [] { return true; }, [] { return true; });
                }));
                Assert::IsTrue(started.wait_for(5s) == std::future_status::ready);
            }
            Assert::IsTrue(cancelled.load());
        }

        TEST_METHOD (PendingRequestRunsAfterActiveWorkAndDoesNotOverlap)
        {
            std::promise<void> entered;
            auto started = entered.get_future();
            std::promise<void> release;
            auto released = release.get_future();
            std::promise<void> finished;
            auto completed = finished.get_future();
            std::atomic_int order = 0;
            std::atomic_int observed_order = -1;
            advanced_paste::HotkeyWork worker;
            worker.start();
            Assert::IsTrue(worker.submit(0, [&](std::stop_token token) {
                entered.set_value();
                while (released.wait_for(1ms) != std::future_status::ready && !token.stop_requested())
                {
                }
                order = 1;
            }));
            Assert::IsTrue(started.wait_for(5s) == std::future_status::ready);
            Assert::IsTrue(worker.submit(1, [&](std::stop_token) {
                observed_order = order.load();
                finished.set_value();
            }));
            Assert::IsFalse(worker.submit(1, [](std::stop_token) {}));
            Assert::IsFalse(worker.submit(2, [](std::stop_token) {}));

            release.set_value();
            Assert::IsTrue(completed.wait_for(5s) == std::future_status::ready);
            worker.stop();
            Assert::AreEqual(1, observed_order.load());
        }

        TEST_METHOD (HeldModifiersCancelOnTimeout)
        {
            Assert::IsFalse(advanced_paste::wait_for_modifiers_release(
                {}, [] { return true; }, [] { return true; }, 20ms));
        }

        TEST_METHOD (ReleasedModifiersAllowTheRequest)
        {
            int polls = 0;
            Assert::IsTrue(advanced_paste::wait_for_modifiers_release(
                {}, [] { return true; }, [&] { return ++polls < 2; }));
            Assert::AreEqual(2, polls);
        }

        TEST_METHOD (FocusChangeWhileWaitingCancelsTheRequest)
        {
            int polls = 0;
            Assert::IsFalse(advanced_paste::wait_for_modifiers_release(
                {}, [&] { return ++polls < 2; }, [] { return true; }));
            Assert::AreEqual(2, polls);
        }

        TEST_METHOD (StopCancelsEvenWhenModifiersAreReleased)
        {
            std::stop_source stop;
            stop.request_stop();
            Assert::IsFalse(advanced_paste::wait_for_modifiers_release(
                stop.get_token(), [] { return true; }, [] { return false; }));
        }
    };
}
