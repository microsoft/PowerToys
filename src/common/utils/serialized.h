#pragma once

#include <functional>
#include <shared_mutex>

template<typename StateT>
class Serialized
{
    mutable std::shared_mutex m;
    StateT s;

public:
    void Read(std::function<void(const StateT&)> fn) const
    {
        std::shared_lock lock{ m };
        fn(s);
    }

    void Access(std::function<void(StateT&)> fn)
    {
        std::unique_lock lock{ m };
        fn(s);
    }

    void Reset()
    {
        std::unique_lock lock{ m };
        s = {};
    }
};
