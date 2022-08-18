#pragma once

#include <windef.h>
#include <d2d1helper.h>
#include <dCommon.h>

#include <array>
#include <functional>
#include <mutex>
#include <vector>

struct BoundsToolState
{
    std::optional<D2D_POINT_2F> currentRegionStart;
};

struct CommonState
{
    std::function<void()> sessionCompletedCallback;
    D2D1::ColorF lineColor = D2D1::ColorF::OrangeRed;
    HMONITOR monitor = {};
    RECT toolbarBoundingBox = {};
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
        bool drawFeetOnCross = true;
        RECT measuredEdges = {};
        POINT cursorPos = {};
        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        Mode mode = Mode::Cross;
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