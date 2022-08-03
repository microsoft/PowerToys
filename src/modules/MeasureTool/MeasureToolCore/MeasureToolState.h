#pragma once

#include <windef.h>
#include <dCommon.h>

#include <array>
#include <functional>
#include <mutex>
#include <vector>

struct BoundsToolState
{
    std::optional<D2D_POINT_2F> currentRegionStart;
    D2D1::ColorF lineColor = D2D1::ColorF::OrangeRed;
};

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
        uint8_t pixelTolerance = 5;
        bool continuousCapture = false;
        D2D1::ColorF crossColor = D2D1::ColorF::OrangeRed;
        struct CrossCoords
        {
            D2D_POINT_2F hLineStart, hLineEnd;
            D2D_POINT_2F vLineStart, vLineEnd;
        } cross = {};
        POINT cursorPos = {};
        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        Mode mode = Mode::Cross;

        bool stopCapturing = false;
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