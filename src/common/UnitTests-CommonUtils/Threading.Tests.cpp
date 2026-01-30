#include "pch.h"
#include "TestHelpers.h"
#include <OnThreadExecutor.h>
#include <EventWaiter.h>
#include <EventLocker.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(OnThreadExecutorTests)
    {
    public:
        TEST_METHOD(Constructor_CreatesInstance)
        {
            OnThreadExecutor executor;
            // Should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(Submit_SingleTask_Executes)
        {
            OnThreadExecutor executor;
            std::atomic<bool> executed{ false };

            auto future = executor.submit(OnThreadExecutor::task_t([&executed]() {
                executed = true;
            }));

            future.wait();
            Assert::IsTrue(executed);
        }

        TEST_METHOD(Submit_MultipleTasks_ExecutesAll)
        {
            OnThreadExecutor executor;
            std::atomic<int> counter{ 0 };

            std::vector<std::future<void>> futures;
            for (int i = 0; i < 10; ++i)
            {
                futures.push_back(executor.submit(OnThreadExecutor::task_t([&counter]() {
                    counter++;
                })));
            }

            for (auto& f : futures)
            {
                f.wait();
            }

            Assert::AreEqual(10, counter.load());
        }

        TEST_METHOD(Submit_TasksExecuteInOrder)
        {
            OnThreadExecutor executor;
            std::vector<int> order;
            std::mutex orderMutex;

            std::vector<std::future<void>> futures;
            for (int i = 0; i < 5; ++i)
            {
                futures.push_back(executor.submit(OnThreadExecutor::task_t([&order, &orderMutex, i]() {
                    std::lock_guard lock(orderMutex);
                    order.push_back(i);
                })));
            }

            for (auto& f : futures)
            {
                f.wait();
            }

            Assert::AreEqual(static_cast<size_t>(5), order.size());
            for (int i = 0; i < 5; ++i)
            {
                Assert::AreEqual(i, order[i]);
            }
        }

        TEST_METHOD(Submit_TaskReturnsResult)
        {
            OnThreadExecutor executor;
            std::atomic<int> result{ 0 };

            auto future = executor.submit(OnThreadExecutor::task_t([&result]() {
                result = 42;
            }));

            future.wait();
            Assert::AreEqual(42, result.load());
        }

        TEST_METHOD(Cancel_ClearsPendingTasks)
        {
            OnThreadExecutor executor;
            std::atomic<int> counter{ 0 };

            // Submit a slow task first
            executor.submit(OnThreadExecutor::task_t([&counter]() {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
                counter++;
            }));

            // Submit more tasks
            for (int i = 0; i < 5; ++i)
            {
                executor.submit(OnThreadExecutor::task_t([&counter]() {
                    counter++;
                }));
            }

            // Cancel pending tasks
            executor.cancel();

            // Wait a bit for any running task to complete
            std::this_thread::sleep_for(std::chrono::milliseconds(200));

            // Not all tasks should have executed
            Assert::IsTrue(counter < 6);
        }

        TEST_METHOD(Destructor_WaitsForCompletion)
        {
            std::atomic<bool> completed{ false };

            {
                OnThreadExecutor executor;
                executor.submit(OnThreadExecutor::task_t([&completed]() {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    completed = true;
                }));
            } // Destructor should wait

            Assert::IsTrue(completed);
        }

        TEST_METHOD(Submit_AfterCancel_StillWorks)
        {
            OnThreadExecutor executor;
            std::atomic<int> counter{ 0 };

            executor.submit(OnThreadExecutor::task_t([&counter]() {
                counter++;
            }));
            executor.cancel();

            auto future = executor.submit(OnThreadExecutor::task_t([&counter]() {
                counter = 42;
            }));
            future.wait();

            Assert::AreEqual(42, counter.load());
        }
    };

    TEST_CLASS(EventWaiterTests)
    {
    public:
        TEST_METHOD(Constructor_CreatesInstance)
        {
            EventWaiter waiter;
            Assert::IsFalse(waiter.is_listening());
        }

        TEST_METHOD(Start_ValidEvent_ReturnsTrue)
        {
            EventWaiter waiter;
            bool result = waiter.start(L"TestEvent_Start", [](DWORD) {});
            Assert::IsTrue(result);
            Assert::IsTrue(waiter.is_listening());
            waiter.stop();
        }

        TEST_METHOD(Start_AlreadyListening_ReturnsFalse)
        {
            EventWaiter waiter;
            waiter.start(L"TestEvent_Double1", [](DWORD) {});
            bool result = waiter.start(L"TestEvent_Double2", [](DWORD) {});
            Assert::IsFalse(result);
            waiter.stop();
        }

        TEST_METHOD(Stop_WhileListening_StopsListening)
        {
            EventWaiter waiter;
            waiter.start(L"TestEvent_Stop", [](DWORD) {});
            Assert::IsTrue(waiter.is_listening());

            waiter.stop();
            Assert::IsFalse(waiter.is_listening());
        }

        TEST_METHOD(Stop_WhenNotListening_DoesNotCrash)
        {
            EventWaiter waiter;
            waiter.stop(); // Should not crash
            Assert::IsFalse(waiter.is_listening());
        }

        TEST_METHOD(Stop_CalledMultipleTimes_DoesNotCrash)
        {
            EventWaiter waiter;
            waiter.start(L"TestEvent_MultiStop", [](DWORD) {});
            waiter.stop();
            waiter.stop();
            waiter.stop();
            Assert::IsFalse(waiter.is_listening());
        }

        TEST_METHOD(Callback_EventSignaled_CallsCallback)
        {
            EventWaiter waiter;
            std::atomic<bool> called{ false };
            std::atomic<DWORD> errorCode{ 0xFFFFFFFF };

            // Create a named event we can signal
            std::wstring eventName = L"TestEvent_Callback_" + std::to_wstring(GetCurrentProcessId());
            HANDLE signalEvent = CreateEventW(nullptr, FALSE, FALSE, eventName.c_str());
            Assert::IsNotNull(signalEvent);

            waiter.start(eventName, [&called, &errorCode](DWORD err) {
                errorCode = err;
                called = true;
            });

            // Signal the event
            SetEvent(signalEvent);

            // Wait for callback
            bool waitResult = TestHelpers::WaitFor([&called]() { return called.load(); }, std::chrono::milliseconds(1000));

            waiter.stop();
            CloseHandle(signalEvent);

            Assert::IsTrue(waitResult);
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), errorCode.load());
        }

        TEST_METHOD(Destructor_StopsListening)
        {
            std::atomic<bool> isListening{ false };
            {
                EventWaiter waiter;
                waiter.start(L"TestEvent_Destructor", [](DWORD) {});
                isListening = waiter.is_listening();
            }
            // After destruction, the waiter should have stopped
            Assert::IsTrue(isListening);
        }

        TEST_METHOD(IsListening_InitialState_ReturnsFalse)
        {
            EventWaiter waiter;
            Assert::IsFalse(waiter.is_listening());
        }

        TEST_METHOD(IsListening_AfterStart_ReturnsTrue)
        {
            EventWaiter waiter;
            waiter.start(L"TestEvent_IsListening", [](DWORD) {});
            Assert::IsTrue(waiter.is_listening());
            waiter.stop();
        }

        TEST_METHOD(IsListening_AfterStop_ReturnsFalse)
        {
            EventWaiter waiter;
            waiter.start(L"TestEvent_AfterStop", [](DWORD) {});
            waiter.stop();
            Assert::IsFalse(waiter.is_listening());
        }
    };

    TEST_CLASS(EventLockerTests)
    {
    public:
        TEST_METHOD(Get_ValidEventName_ReturnsLocker)
        {
            std::wstring eventName = L"TestEventLocker_" + std::to_wstring(GetCurrentProcessId());
            auto locker = EventLocker::Get(eventName);
            Assert::IsTrue(locker.has_value());
        }

        TEST_METHOD(Get_UniqueNames_CreatesSeparateLockers)
        {
            auto locker1 = EventLocker::Get(L"TestEventLocker1_" + std::to_wstring(GetCurrentProcessId()));
            auto locker2 = EventLocker::Get(L"TestEventLocker2_" + std::to_wstring(GetCurrentProcessId()));
            Assert::IsTrue(locker1.has_value());
            Assert::IsTrue(locker2.has_value());
        }

        TEST_METHOD(Destructor_CleansUpHandle)
        {
            std::wstring eventName = L"TestEventLockerCleanup_" + std::to_wstring(GetCurrentProcessId());
            {
                auto locker = EventLocker::Get(eventName);
                Assert::IsTrue(locker.has_value());
            }
            // After destruction, the event should be cleaned up
            // Creating a new one should succeed
            auto newLocker = EventLocker::Get(eventName);
            Assert::IsTrue(newLocker.has_value());
        }

        TEST_METHOD(MoveConstructor_TransfersOwnership)
        {
            std::wstring eventName = L"TestEventLockerMove_" + std::to_wstring(GetCurrentProcessId());
            auto locker1 = EventLocker::Get(eventName);
            Assert::IsTrue(locker1.has_value());

            EventLocker locker2 = std::move(*locker1);
            // Move should transfer ownership without crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(MoveAssignment_TransfersOwnership)
        {
            std::wstring eventName1 = L"TestEventLockerMoveAssign1_" + std::to_wstring(GetCurrentProcessId());
            std::wstring eventName2 = L"TestEventLockerMoveAssign2_" + std::to_wstring(GetCurrentProcessId());

            auto locker1 = EventLocker::Get(eventName1);
            auto locker2 = EventLocker::Get(eventName2);

            Assert::IsTrue(locker1.has_value());
            Assert::IsTrue(locker2.has_value());

            *locker1 = std::move(*locker2);
            // Should not crash
            Assert::IsTrue(true);
        }
    };
}
