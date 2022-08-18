#pragma once

#include <array>
#include <functional>
#include <mutex>
#include <vector>

#include <windef.h>
#include <d2d1helper.h>
#include <dCommon.h>

#include <common/utils/serialized.h>

struct OverlayBoxText
{
    std::array<wchar_t, 32> buffer = {};
};

struct CommonState
{
    std::function<void()> sessionCompletedCallback;
    D2D1::ColorF lineColor = D2D1::ColorF::OrangeRed;
    RECT toolbarBoundingBox = {};

    mutable Serialized<OverlayBoxText> overlayBoxText;
    POINT cursorPos = {}; // updated atomically
};

struct BoundsToolState
{
    std::optional<D2D_POINT_2F> currentRegionStart;
    CommonState* commonState = nullptr; // backreference for WndProc
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
    bool cursorInLeftScreenHalf = false;
    bool cursorInTopScreenHalf = false;
    Mode mode = Mode::Cross;
};