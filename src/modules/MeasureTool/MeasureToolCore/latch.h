#pragma once

#if __has_include(<latch>)
#include <latch>
using Latch = std::latch;
#else
#include <atomic>
#include <cstddef>
#include <limits.h>

class Latch
{
public:
    [[nodiscard]] static constexpr ptrdiff_t(max)() noexcept
    {
        return (1ULL << (sizeof(ptrdiff_t) * CHAR_BIT - 1)) - 1;
    }

    constexpr explicit Latch(const std::ptrdiff_t _Expected) noexcept :
        _Counter{ _Expected }
    {
    }

    Latch(const Latch&) = delete;
    Latch& operator=(const Latch&) = delete;

    void count_down(const ptrdiff_t _Update = 1) noexcept
    {
        const ptrdiff_t _Current = _Counter.fetch_sub(_Update) - _Update;
        if (_Current == 0)
        {
            _Counter.notify_all();
        }
    }

    [[nodiscard]] bool try_wait() const noexcept
    {
        return _Counter.load() == 0;
    }

    void wait() const noexcept
    {
        for (;;)
        {
            const ptrdiff_t _Current = _Counter.load();
            if (_Current == 0)
            {
                return;
            }
            else
                _Counter.wait(_Current, std::memory_order_relaxed);
        }
    }

    void arrive_and_wait(const ptrdiff_t _Update = 1) noexcept
    {
        const ptrdiff_t _Current = _Counter.fetch_sub(_Update) - _Update;
        if (_Current == 0)
        {
            _Counter.notify_all();
        }
        else
        {
            _Counter.wait(_Current, std::memory_order_relaxed);
            wait();
        }
    }

private:
    std::atomic<std::ptrdiff_t> _Counter;
};

#endif