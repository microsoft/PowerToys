#include "pch.h"
#include <common/utils/async_task.h>

#include <string>
#include <stdexcept>
#include <chrono>
#include <thread>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// ---------------------------------------------------------------------------
// Helper coroutines used by the tests
// ---------------------------------------------------------------------------
namespace
{
    // Awaitable that resumes the caller on a background std::thread after a delay.
    // Pure C++20 — no WinRT dependency.
    struct thread_delay_awaitable
    {
        std::chrono::milliseconds delay;

        bool await_ready() const noexcept { return false; }
        void await_suspend(std::coroutine_handle<> h) const
        {
            std::thread([h, d = delay]() {
                std::this_thread::sleep_for(d);
                h.resume();
            }).detach();
        }
        void await_resume() const noexcept {}
    };

    utils::async_task<int> return_immediate(int value)
    {
        co_return value;
    }

    utils::async_task<std::string> return_string(std::string s)
    {
        co_return s;
    }

    utils::async_task<int> throw_logic_error()
    {
        throw std::logic_error("test error");
        co_return 0; // unreachable, but needed to make it a coroutine
    }

    utils::async_task<int> return_after_delay(int value, std::chrono::milliseconds delay)
    {
        co_await thread_delay_awaitable{ delay };
        co_return value;
    }

    // A coroutine that co_awaits another async_task (chaining)
    utils::async_task<int> chain_add(int a, int b)
    {
        auto first = co_await return_immediate(a);
        auto second = co_await return_immediate(b);
        co_return first + second;
    }

    // A coroutine that co_awaits a delayed task
    utils::async_task<int> chain_delayed(int value, std::chrono::milliseconds delay)
    {
        auto result = co_await return_after_delay(value, delay);
        co_return result * 2;
    }

    // Move-only type to verify move semantics work
    struct MoveOnly
    {
        int value;
        explicit MoveOnly(int v) : value(v) {}
        MoveOnly(MoveOnly&& other) noexcept : value(other.value) { other.value = -1; }
        MoveOnly& operator=(MoveOnly&& other) noexcept
        {
            value = other.value;
            other.value = -1;
            return *this;
        }
        MoveOnly(const MoveOnly&) = delete;
        MoveOnly& operator=(const MoveOnly&) = delete;
    };

    utils::async_task<MoveOnly> return_move_only(int value)
    {
        co_return MoveOnly{ value };
    }
}

namespace UnitTestsCommonUtils
{
    TEST_CLASS (AsyncTaskTests)
    {
    public:
        // ----- Basic co_return + .get() tests -----

        TEST_METHOD (Get_ImmediateInt_ReturnsValue)
        {
            auto task = return_immediate(42);
            Assert::AreEqual(42, task.get());
        }

        TEST_METHOD (Get_ImmediateString_ReturnsValue)
        {
            auto task = return_string("hello");
            Assert::AreEqual(std::string("hello"), task.get());
        }

        TEST_METHOD (Get_ZeroValue_ReturnsZero)
        {
            auto task = return_immediate(0);
            Assert::AreEqual(0, task.get());
        }

        TEST_METHOD (Get_NegativeValue_ReturnsNegative)
        {
            auto task = return_immediate(-99);
            Assert::AreEqual(-99, task.get());
        }

        // ----- Exception propagation -----

        TEST_METHOD (Get_ThrowsInCoroutine_PropagatesException)
        {
            auto task = throw_logic_error();
            Assert::ExpectException<std::logic_error>([&task]() {
                task.get();
            });
        }

        // ----- Async delay (proves co_await actually suspends/resumes) -----

        TEST_METHOD (Get_DelayedResult_ReturnsAfterWait)
        {
            auto task = return_after_delay(7, std::chrono::milliseconds(50));
            Assert::AreEqual(7, task.get());
        }

        // ----- co_await chaining (async_task awaiting async_task) -----

        TEST_METHOD (CoAwait_ChainImmediate_ReturnsSummedValue)
        {
            auto task = chain_add(3, 4);
            Assert::AreEqual(7, task.get());
        }

        TEST_METHOD (CoAwait_ChainDelayed_ReturnsDoubledValue)
        {
            auto task = chain_delayed(5, std::chrono::milliseconds(50));
            Assert::AreEqual(10, task.get());
        }

        // ----- Move semantics -----

        TEST_METHOD (Get_MoveOnlyType_MovesCorrectly)
        {
            auto task = return_move_only(123);
            MoveOnly result = task.get();
            Assert::AreEqual(123, result.value);
        }

        TEST_METHOD (MoveConstruct_Task_OriginalIsEmpty)
        {
            auto task1 = return_immediate(10);
            auto task2 = std::move(task1);
            Assert::AreEqual(10, task2.get());
            // task1's handle should be null after move — destructor must not crash
        }

        TEST_METHOD (MoveAssign_Task_OriginalIsEmpty)
        {
            auto task1 = return_immediate(20);
            auto task2 = return_immediate(30);
            task2 = std::move(task1);
            Assert::AreEqual(20, task2.get());
        }

        // ----- Concurrent .get() from different threads -----

        TEST_METHOD (Get_CalledFromWorkerThread_ReturnsValue)
        {
            auto task = return_after_delay(99, std::chrono::milliseconds(30));
            int result = 0;
            std::thread t([&]() {
                result = task.get();
            });
            t.join();
            Assert::AreEqual(99, result);
        }

        // ----- Multiple independent tasks -----

        TEST_METHOD (Get_MultipleIndependentTasks_AllReturnCorrectValues)
        {
            constexpr int count = 10;
            std::vector<utils::async_task<int>> tasks;
            tasks.reserve(count);
            for (int i = 0; i < count; ++i)
            {
                tasks.push_back(return_immediate(i * i));
            }
            for (int i = 0; i < count; ++i)
            {
                Assert::AreEqual(i * i, tasks[i].get());
            }
        }
    };
}
