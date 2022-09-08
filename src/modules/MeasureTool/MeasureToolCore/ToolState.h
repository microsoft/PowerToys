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
#include "BGRATextureView.h"
#include "Measurement.h"

struct OverlayBoxText
{
    std::array<wchar_t, 128> buffer = {};
};

struct CommonState
{
    std::function<void()> sessionCompletedCallback;
    D2D1::ColorF lineColor = D2D1::ColorF::OrangeRed;
    Box toolbarBoundingBox;

    Measurement::Unit units = Measurement::Unit::Pixel;

    mutable Serialized<OverlayBoxText> overlayBoxText;
    POINT cursorPosSystemSpace = {}; // updated atomically
    std::atomic_bool closeOnOtherMonitors = false;
};

struct CursorDrag
{
    D2D_POINT_2F startPos = {};
    D2D_POINT_2F currentPos = {};
    DWORD touchID = 0; // indicate whether the drag belongs to a touch input sequence
};

struct BoundsToolState
{
    struct PerScreen
    {
        std::optional<CursorDrag> currentBounds;
        std::vector<Measurement> measurements;
    };

    // TODO: refactor so we don't need unordered_map
    std::unordered_map<HWND, PerScreen> perScreen;

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

    struct Global
    {
        uint8_t pixelTolerance = 30;
        bool continuousCapture = false;
        bool drawFeetOnCross = true;
        bool perColorChannelEdgeDetection = false;
        Mode mode = Mode::Cross;
    } global;

    struct PerScreen
    {
        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        std::optional<Measurement> measuredEdges;
        // While not in a continuous capturing mode, we need to draw captured backgrounds. These are passed
        // directly from a capturing thread.
        const MappedTextureView* capturedScreenTexture = nullptr;
        // After the drawing thread finds its capturedScreenTexture, it converts it to
        // a Direct2D compatible bitmap and caches it here
        winrt::com_ptr<ID2D1Bitmap> capturedScreenBitmap;
    };
    std::unordered_map<HWND, PerScreen> perScreen;

    CommonState* commonState = nullptr; // required for WndProc
};
