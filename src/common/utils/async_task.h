#pragma once

#include <coroutine>
#include <exception>
#include <mutex>
#include <optional>
#include <semaphore>
#include <utility>

namespace utils
{

/// A minimal C++20 coroutine return type that can carry any value type.
///
/// Use this instead of WinRT IAsyncOperation<T> when T is not a
/// WinRT-projected type (e.g. std::expected, std::optional<path>).
///
/// Features:
///   - co_return of arbitrary move-only or copyable values
///   - Efficient synchronous blocking via .get() (uses a semaphore, no spin)
///   - co_await support so another coroutine can await the result
///   - Exception propagation from the coroutine body to the caller
///
/// Example (sync caller):
///   utils::async_task<int> compute_async() { co_return 42; }
///   int result = compute_async().get();
///
/// Example (async caller):
///   utils::async_task<int> caller() {
///       int r = co_await compute_async();
///       co_return r + 1;
///   }
template<typename T>
class async_task
{
public:
    struct promise_type
    {
        std::optional<T> result;
        std::exception_ptr exception;
        std::binary_semaphore ready{ 0 };

        // Continuation coroutine handle to resume when this task completes.
        std::coroutine_handle<> continuation;

        async_task get_return_object()
        {
            return async_task{ std::coroutine_handle<promise_type>::from_promise(*this) };
        }

        // Start executing immediately (eager).
        std::suspend_never initial_suspend() noexcept { return {}; }

        // Custom final awaiter: signals the semaphore for .get() callers
        // and resumes any co_await continuation, all after the coroutine
        // is fully suspended (safe for cross-thread destruction).
        struct final_awaiter
        {
            bool await_ready() noexcept { return false; }
            void await_suspend(std::coroutine_handle<promise_type> h) noexcept
            {
                auto& p = h.promise();
                // Signal synchronous waiters.
                p.ready.release();
                // Resume any co_await continuation.
                if (p.continuation)
                {
                    p.continuation.resume();
                }
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

    /// Block until the coroutine completes and return the result.
    /// Rethrows if the coroutine encountered an unhandled exception.
    /// Must be called at most once.
    T get()
    {
        handle_.promise().ready.acquire();
        return get_result();
    }

    /// Awaiter returned by co_await on an async_task<T>.
    /// Allows one coroutine to await another async_task's result.
    struct awaiter
    {
        std::coroutine_handle<promise_type> handle;

        bool await_ready() const noexcept
        {
            return handle.done();
        }

        // Symmetric transfer: suspend the caller and install it as the continuation.
        std::coroutine_handle<> await_suspend(std::coroutine_handle<> caller) noexcept
        {
            handle.promise().continuation = caller;
            // If already done, resume caller immediately.
            if (handle.done())
            {
                return caller;
            }
            // Otherwise, suspend caller; final_awaiter will resume it.
            return std::noop_coroutine();
        }

        T await_resume()
        {
            auto& p = handle.promise();
            if (p.exception)
            {
                std::rethrow_exception(p.exception);
            }
            return std::move(*p.result);
        }
    };

    awaiter operator co_await() noexcept
    {
        return awaiter{ handle_ };
    }

private:
    T get_result()
    {
        auto& promise = handle_.promise();
        if (promise.exception)
        {
            std::rethrow_exception(promise.exception);
        }
        return std::move(*promise.result);
    }

    std::coroutine_handle<promise_type> handle_;
};

} // namespace utils
