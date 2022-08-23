#pragma once

#include <array>
#include <functional>
#include <mutex>
#include <vector>
#include <thread>
#include <unordered_map>

#include <windef.h>
#include <d2d1helper.h>
#include <dCommon.h>

#include <common/Display/monitors.h>
#include <common/utils/serialized.h>

//#define DEBUG_OVERLAY

struct OverlayBoxText
{
    std::array<wchar_t, 32> buffer = {};
};

struct CommonState
{
    std::function<void()> sessionCompletedCallback;
    D2D1::ColorF lineColor = D2D1::ColorF::OrangeRed;
    Box toolbarBoundingBox;

    mutable Serialized<OverlayBoxText> overlayBoxText;
    POINT cursorPos = {}; // updated atomically
    std::atomic_bool closeOnOtherMonitors = false;
};

struct BoundsToolState
{
    std::optional<D2D_POINT_2F> currentRegionStart;
    std::unordered_map<HWND, std::vector<D2D1_RECT_F>> measurementsByScreen;
    CommonState* commonState = nullptr; // required for WndProc
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
    bool perColorChannelEdgeDetection = false;
    Mode mode = Mode::Cross;
    CommonState* commonState = nullptr; // required for WndProc
};