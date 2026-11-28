// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../hotkey_dispatcher.h"
#include <interface/powertoy_module_interface.h>

#include <atomic>
#include <future>
#include <thread>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace std::chrono_literals;
using HotkeyDispatch::Dispatcher;
using HotkeyDispatch::WorkItem;
using HotkeyDisposition = PowertoyModuleIface::HotkeyDisposition;

namespace
{
    WorkItem MakeItem(std::function<void()> action)
    {
        return WorkItem{ std::move(action), std::make_shared<const HotkeyDispatch::HotkeyLabel>() };
    }

    // Implements only what the interface requires, so the defaults are exercised.
    class FakeModule : public PowertoyModuleIface
    {
    public:
        bool enabled = false;
        std::vector<size_t> onHotkeyCalls;

        const wchar_t* get_name() override { return L"Fake"; }
        const wchar_t* get_key() override { return L"Fake"; }
        bool get_config(wchar_t*, int*) override { return false; }
        void set_config(const wchar_t*) override {}
        void enable() override { enabled = true; }
        void disable() override { enabled = false; }
        bool is_enabled() override { return enabled; }
        void destroy() override {}

        bool on_hotkey(size_t hotkeyId) override
        {
            onHotkeyCalls.push_back(hotkeyId);
            return enabled;
        }
    };
}

namespace RunnerUnitTests
{
    TEST_CLASS (HotkeyDispatcherTests)
    {
    public:
        TEST_METHOD (ActionsRunInFifoOrder)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, nullptr);

            std::vector<int> order;
            for (int i = 0; i < 5; ++i)
            {
                Assert::AreNotEqual(Dispatcher::InvalidTicket, dispatcher.Enqueue(MakeItem([&order, i] { order.push_back(i); })));
            }

            Assert::AreEqual(size_t{ 5 }, dispatcher.PendingCount());
            Assert::AreEqual(size_t{ 5 }, dispatcher.DrainPending());
            Assert::AreEqual(size_t{ 0 }, dispatcher.PendingCount());
            Assert::IsTrue(order == std::vector<int>{ 0, 1, 2, 3, 4 });
        }

        TEST_METHOD (ActionsEnqueuedFromAnotherThreadRunInOrder)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, nullptr);

            std::vector<int> order;
            std::thread producer([&] {
                for (int i = 0; i < 100; ++i)
                {
                    dispatcher.Enqueue(MakeItem([&order, i] { order.push_back(i); }));
                }
            });

            while (order.size() < 100)
            {
                dispatcher.DrainPending();
                std::this_thread::yield();
            }

            producer.join();
            for (int i = 0; i < 100; ++i)
            {
                Assert::AreEqual(i, order[i]);
            }
        }

        TEST_METHOD (ThrowingActionDoesNotStopLaterActions)
        {
            std::vector<bool> errors;
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, [&errors](const WorkItem&, HotkeyDispatch::Clock::duration, std::exception_ptr error) {
                errors.push_back(error != nullptr);
            });

            bool laterRan = false;
            dispatcher.Enqueue(MakeItem([] { throw std::runtime_error("boom"); }));
            dispatcher.Enqueue(MakeItem([] { throw 42; }));
            dispatcher.Enqueue(MakeItem([&laterRan] { laterRan = true; }));

            Assert::AreEqual(size_t{ 3 }, dispatcher.DrainPending());
            Assert::IsTrue(laterRan);
            Assert::IsTrue(errors == std::vector<bool>{ true, true, false });
        }

        TEST_METHOD (ThrowingObserverDoesNotStopLaterActions)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, [](const WorkItem&, HotkeyDispatch::Clock::duration, std::exception_ptr) {
                throw std::runtime_error("observer");
            });

            int ran = 0;
            dispatcher.Enqueue(MakeItem([&ran] { ++ran; }));
            dispatcher.Enqueue(MakeItem([&ran] { ++ran; }));

            Assert::AreEqual(size_t{ 2 }, dispatcher.DrainPending());
            Assert::AreEqual(2, ran);
        }

        TEST_METHOD (ObserverReceivesLabelAndElapsedTime)
        {
            std::wstring seenModule;
            HotkeyDispatch::Clock::duration seenElapsed{};
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, [&](const WorkItem& item, HotkeyDispatch::Clock::duration elapsed, std::exception_ptr) {
                seenModule = item.label->moduleName;
                seenElapsed = elapsed;
            });

            auto label = std::make_shared<const HotkeyDispatch::HotkeyLabel>(HotkeyDispatch::HotkeyLabel{ L"keyboard hook", L"Peek", L"ctrl+space" });
            dispatcher.Enqueue(WorkItem{ [] { std::this_thread::sleep_for(20ms); }, label });
            dispatcher.DrainPending();

            Assert::AreEqual(std::wstring{ L"Peek" }, seenModule);
            Assert::IsTrue(seenElapsed >= 15ms);
        }

        TEST_METHOD (EnqueueCallsWake)
        {
            int wakes = 0;
            Dispatcher dispatcher;
            dispatcher.Start([&wakes] { ++wakes; }, nullptr);

            dispatcher.Enqueue(MakeItem([] {}));
            dispatcher.Enqueue(MakeItem([] {}));

            Assert::AreEqual(2, wakes);
        }

        TEST_METHOD (EnqueueBeforeStartIsRejected)
        {
            Dispatcher dispatcher;
            Assert::AreEqual(Dispatcher::InvalidTicket, dispatcher.Enqueue(MakeItem([] {})));
            Assert::AreEqual(size_t{ 0 }, dispatcher.PendingCount());
        }

        TEST_METHOD (ShutdownDropsPendingAndRejectsNewWork)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, nullptr);

            bool ran = false;
            auto ticket = dispatcher.Enqueue(MakeItem([&ran] { ran = true; }));
            Assert::AreNotEqual(Dispatcher::InvalidTicket, ticket);

            dispatcher.Shutdown();

            Assert::AreEqual(size_t{ 0 }, dispatcher.PendingCount());
            Assert::AreEqual(size_t{ 0 }, dispatcher.DrainPending());
            Assert::IsFalse(ran);
            Assert::AreEqual(Dispatcher::InvalidTicket, dispatcher.Enqueue(MakeItem([&ran] { ran = true; })));
            Assert::IsFalse(dispatcher.WaitForCompletion(ticket, 0ms));
        }

        TEST_METHOD (ShutdownReleasesWaiter)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, nullptr);
            auto ticket = dispatcher.Enqueue(MakeItem([] {}));

            auto waiter = std::async(std::launch::async, [&] { return dispatcher.WaitForCompletion(ticket, 10s); });
            std::this_thread::sleep_for(20ms);
            const auto start = std::chrono::steady_clock::now();
            dispatcher.Shutdown();

            Assert::IsFalse(waiter.get());
            Assert::IsTrue(std::chrono::steady_clock::now() - start < 5s);
        }

        TEST_METHOD (WaitForCompletionTimesOutWhenNotDrained)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, nullptr);
            auto ticket = dispatcher.Enqueue(MakeItem([] {}));

            Assert::IsFalse(dispatcher.WaitForCompletion(ticket, 10ms));
            Assert::IsFalse(dispatcher.WaitForCompletion(Dispatcher::InvalidTicket, 10ms));
        }

        TEST_METHOD (WaitForCompletionReturnsAfterWorkerDrains)
        {
            Dispatcher dispatcher;
            std::promise<void> woken;
            dispatcher.Start([&woken] { woken.set_value(); }, nullptr);

            auto ticket = dispatcher.Enqueue(MakeItem([] {}));
            std::thread worker([&] {
                woken.get_future().wait();
                dispatcher.DrainPending();
            });

            Assert::IsTrue(dispatcher.WaitForCompletion(ticket, 10s));
            worker.join();
        }

        TEST_METHOD (NestedDrainDoesNotRunActions)
        {
            Dispatcher dispatcher;
            dispatcher.Start(nullptr, nullptr);

            std::vector<int> order;
            size_t nestedRan = 1;
            dispatcher.Enqueue(MakeItem([&] {
                order.push_back(0);
                nestedRan = dispatcher.DrainPending();
                order.push_back(1);
            }));
            dispatcher.Enqueue(MakeItem([&] { order.push_back(2); }));

            Assert::AreEqual(size_t{ 2 }, dispatcher.DrainPending());
            Assert::AreEqual(size_t{ 0 }, nestedRan);
            Assert::IsTrue(order == std::vector<int>{ 0, 1, 2 });
        }

        TEST_METHOD (DefaultShimSwallowsOnlyWhenEnabled)
        {
            FakeModule module;
            Assert::IsTrue(module.should_handle_hotkey(0) == HotkeyDisposition::PassThrough);

            module.enable();
            Assert::IsTrue(module.should_handle_hotkey(0) == HotkeyDisposition::InvokeAndSwallow);
            Assert::IsTrue(module.should_handle_hotkey(3) == HotkeyDisposition::InvokeAndSwallow);

            // The hook-side check must not run the module's action.
            Assert::IsTrue(module.onHotkeyCalls.empty());
        }

        TEST_METHOD (DefaultInvokeCallsOnHotkeyWithId)
        {
            FakeModule module;
            module.enable();

            module.invoke_hotkey(2);
            module.invoke_hotkey(PowertoyModuleIface::WIN_KEY_HOLD_HOTKEY_ID);

            Assert::IsTrue(module.onHotkeyCalls == std::vector<size_t>{ 2, PowertoyModuleIface::WIN_KEY_HOLD_HOTKEY_ID });
        }
    };
}
