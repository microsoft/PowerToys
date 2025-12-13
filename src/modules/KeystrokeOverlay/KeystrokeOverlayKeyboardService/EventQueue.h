// EventQueue.h
// Custom SPSC queue to store KeyStrokeEvent objects between producer and consumer threads.
#pragma once
#include <atomic> // atomic type for head and tail
#include <vector>

template <typename T, size_t N>
class SpscRing
{
public:
    // Attempts to push an item into the queue. Returns false if the queue is full.
    bool try_push(const T &v)
    {       
        auto head = _head.load(std::memory_order_relaxed); // maybe fix pointer types later?
        auto next = (head + 1) % N;
        if (next == _tail.load(std::memory_order_acquire))
            return false; // full case
        _buf[head] = v;
        _head.store(next, std::memory_order_release);
        return true;
    }
    // Attempts to pop an item from the queue. Returns false if the queue is empty.
    bool try_pop(T &out)
    {
        auto tail = _tail.load(std::memory_order_relaxed);
        if (tail == _head.load(std::memory_order_acquire))
            return false; // empty case
        out = _buf[tail];
        _tail.store((tail + 1) % N, std::memory_order_release);
        return true;
    }

private:
    std::array<T, N> _buf{};
    std::atomic<size_t> _head{0}, _tail{0};
};
