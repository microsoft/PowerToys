#pragma once

#include <functional>
#include <mutex>

template<typename StateT> class Serialized
{
    std::mutex m;
    StateT s;

public:
    void Access(std::function<void(StateT&)> fn)
    {
        std::scoped_lock lock{ m };
        fn(s);
    }

    void Reset()
    {
        std::scoped_lock lock{ m };
        s = {};
    }
};
