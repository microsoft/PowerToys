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
    uint8_t pixelTolerance = 5;
    bool continuousCapture = false;
    bool drawFeetOnCross = true;
    RECT measuredEdges = {};
    POINT cursorPos = {};
    bool cursorInLeftScreenHalf = false;
    bool cursorInTopScreenHalf = false;
    Mode mode = Mode::Cross;
};