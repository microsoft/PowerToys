#pragma once

#include <windef.h>
#include <dcommon.h>

#include <mutex>
#include <functional>
#include <array>

struct MeasureToolState
{
    enum class Mode
    {
        Horizontal,
        Vertical,
        Cross
    };
    struct State
    {
        struct CrossCoords
        {
            D2D_POINT_2F hLineStart, hLineEnd;
            D2D_POINT_2F vLineStart, vLineEnd;
        } cross = {};
        POINT cursorPos = {};
        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        Mode mode = Mode::Cross;

        bool shouldExit = false;
    };

private:
    std::mutex m;
    State s;

public:
    void Access(std::function<void(State&)> fn)
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