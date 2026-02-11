#pragma once

#include <atomic>
#include <coroutine>
#include <exception>
#include <optional>
#include <thread>
#include <utility>

namespace utils
{

// A minimal C++20 coroutine return type that can carry any value type.
// Use this instead of IAsyncOperation<T> when T is not a WinRT-projected type.
// Supports co_return of arbitrary values and synchronous .get() for the caller.
//
// Example:
//   utils::async_task<int> compute_async() { co_return 42; }
//   int result = compute_async().get();
template<typename T>
class async_task
{
public:
    struct promise_type
    {
        std::optional<T> result;
        std::exception_ptr exception;
        std::atomic<bool> completed{ false };

        async_task get_return_object()
        {
            return async_task{ std::coroutine_handle<promise_type>::from_promise(*this) };
        }

        std::suspend_never initial_suspend() noexcept { return {}; }

        // Custom final awaiter that signals completion AFTER the coroutine
        // is fully suspended, ensuring safe destruction from another thread.
        struct final_awaiter
        {
            bool await_ready() noexcept { return false; }
            void await_suspend(std::coroutine_handle<promise_type> h) noexcept
            {
                h.promise().completed.store(true, std::memory_order_release);
            }
            void await_resume() noexcept {}
        };

        final_awaiter final_suspend() noexcept { return {}; }

        template<typename U>
        void return_value(U&& value)
        {
            result.emplace(std::forward<U>(value));
        }

        void unhandled_exception()
        {
            exception = std::current_exception();
        }
    };

    explicit async_task(std::coroutine_handle<promise_type> h) : handle_(h) {}

    async_task(async_task&& other) noexcept : handle_(std::exchange(other.handle_, {})) {}

    async_task& operator=(async_task&& other) noexcept
    {
        if (this != &other)
        {
            if (handle_)
                handle_.destroy();
            handle_ = std::exchange(other.handle_, {});
        }
        return *this;
    }

    ~async_task()
    {
        if (handle_)
            handle_.destroy();
    }

    async_task(const async_task&) = delete;
    async_task& operator=(const async_task&) = delete;

    // Block until the coroutine completes and return the result.
    // Throws if the coroutine encountered an unhandled exception.
    T get()
    {
        while (!handle_.promise().completed.load(std::memory_order_acquire))
        {
            std::this_thread::yield();
        }
        auto& promise = handle_.promise();
        if (promise.exception)
        {
            std::rethrow_exception(promise.exception);
        }
        return std::move(*promise.result);
    }

private:
    std::coroutine_handle<promise_type> handle_;
};

} // namespace utils
