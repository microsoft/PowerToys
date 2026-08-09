#pragma once

#include <array>
#include <chrono>
#include <functional>
#include <memory>
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
#include "BoundsSnapModel.h"
#include "Measurement.h"

struct OverlayBoxText
{
    std::array<wchar_t, 128> buffer = {};
};

struct CommonState
{
    std::function<void()> sessionCompletedCallback;
    D2D1::ColorF lineColor = D2D1::ColorF::OrangeRed;

    Measurement::Unit units = Measurement::Unit::Pixel;

    #pragma warning(push)
    #pragma warning(disable : 4324)
    alignas(8) POINT cursorPosSystemSpace = {}; // updated atomically
    #pragma warning(pop)

    std::atomic_bool closeOnOtherMonitors = false;

    float GetPhysicalPx2MmRatio(HWND window) const
    {
        auto ratio = -1.0f;
        auto size = MonitorInfo::GetFromWindow(window).GetSize();
        if (size.width_physical > 0u)
        {
            ratio = size.width_mm / static_cast<float>(size.width_physical);
        }
        return ratio;
    }

    // The toolbar's current visual bounds, including its input-transparent shadow companion
    // (physical pixels, absolute screen coordinates). Used to (1) skip drawing/measuring under the
    // toolbar and (2) punch a hole in each overlay window's region so the toolbar and shadow stay
    // visible while input in the shadow padding reaches the app underneath. The toolbar can be
    // dragged live on the WinUI thread while the D3D overlay threads read this every frame, so
    // reads/writes are locked. Coordinates may be negative on monitors left of/above the primary.
    Box GetToolbarBoundingBox() const
    {
        std::scoped_lock lock{ toolbarBoundingBoxMutex };
        return toolbarBoundingBox;
    }

    void SetToolbarBoundingBox(const Box& box)
    {
        std::scoped_lock lock{ toolbarBoundingBoxMutex };
        toolbarBoundingBox = box;
    }

private:
    mutable std::mutex toolbarBoundingBoxMutex;
    Box toolbarBoundingBox;
};

struct CursorDrag
{
    D2D_POINT_2F startPos = {};
    D2D_POINT_2F currentPos = {};
    DWORD touchID = 0; // indicate whether the drag belongs to a touch input sequence
};

struct BoundsToolState
{
    struct Global
    {
        uint8_t pixelTolerance = 30;
        bool perColorChannelEdgeDetection = false;
    } global;

    struct PerScreen
    {
        std::optional<CursorDrag> rawBounds;
        std::optional<CursorDrag> currentBounds;
        std::vector<Measurement> measurements;
        std::shared_ptr<const OwnedBGRATextureView> snapFrame;
        std::function<void(uint64_t)> requestSnapFrame;
        uint64_t snapCaptureGeneration = 0;
        bool waitingForSnapFrame = false;
        bool snapFrameReady = false;
        bool appendMeasurement = false;
        bool fitSelectionOnCommit = false;
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
        using PrevMeasurement = std::pair<POINT, Measurement>;

        struct ToleranceFeedback
        {
            OverlayBoxText text;
            size_t textLength = 0;
            POINT cursorPos{};
            std::chrono::steady_clock::time_point expiresAt{};
        };

        bool cursorInLeftScreenHalf = false;
        bool cursorInTopScreenHalf = false;
        std::optional<Measurement> measuredEdges;
        std::vector<PrevMeasurement> prevMeasurements;
        std::optional<ToleranceFeedback> toleranceFeedback;

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
