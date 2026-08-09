#include "pch.h"
#include "BoundsToolOverlayUI.h"
#include "CoordinateSystemConversion.h"
#include "Clipboard.h"
#include "constants.h"

#include <common/utils/window.h>
#include <vector>

namespace
{
    Measurement GetMeasurement(const CursorDrag& currentBounds, float px2mmRatio)
    {
        D2D1_RECT_F rect;
        std::tie(rect.left, rect.right) =
            std::minmax(currentBounds.currentPos.x, currentBounds.startPos.x);
        std::tie(rect.top, rect.bottom) =
            std::minmax(currentBounds.currentPos.y, currentBounds.startPos.y);

        return Measurement(rect, px2mmRatio);
    }

    void CopyToClipboard(HWND window, const BoundsToolState& toolState)
    {
        std::vector<Measurement> allMeasurements;
        for (const auto& [handle, perScreen] : toolState.perScreen)
        {
            allMeasurements.append_range(perScreen.measurements);

            if (handle == window && perScreen.currentBounds)
            {
                auto px2mmRatio = toolState.commonState->GetPhysicalPx2MmRatio(window);
                allMeasurements.push_back(GetMeasurement(*perScreen.currentBounds, px2mmRatio));
            }
        }

        SetClipboardToMeasurements(allMeasurements, true, true, toolState.commonState->units);
    }

    void ToggleCursor(const bool show)
    {
        if (show)
        {
            for (; ShowCursor(show) < 0;)
                ;
        }
        else
        {
            for (; ShowCursor(show) >= 0;)
                ;
        }
    }

    CursorDrag ApplySnappedBounds(const CursorDrag& rawBounds, const RECT& snappedBounds)
    {
        const bool reverseX = rawBounds.currentPos.x < rawBounds.startPos.x;
        const bool reverseY = rawBounds.currentPos.y < rawBounds.startPos.y;
        return CursorDrag{
            .startPos = D2D_POINT_2F{
                .x = static_cast<float>(reverseX ? snappedBounds.right : snappedBounds.left),
                .y = static_cast<float>(reverseY ? snappedBounds.bottom : snappedBounds.top),
            },
            .currentPos = D2D_POINT_2F{
                .x = static_cast<float>(reverseX ? snappedBounds.left : snappedBounds.right),
                .y = static_cast<float>(reverseY ? snappedBounds.top : snappedBounds.bottom),
            },
            .touchID = rawBounds.touchID,
        };
    }

    bool IsAltPressed()
    {
        constexpr SHORT Pressed = static_cast<SHORT>(0x8000);
        return (GetKeyState(VK_MENU) & Pressed) != 0 ||
               (GetAsyncKeyState(VK_MENU) & Pressed) != 0 ||
               (GetAsyncKeyState(VK_LMENU) & Pressed) != 0 ||
               (GetAsyncKeyState(VK_RMENU) & Pressed) != 0;
    }

    void CommitCurrentBounds(HWND window, BoundsToolState* toolState)
    {
        auto& perScreen = toolState->perScreen[window];
        if (!perScreen.rawBounds)
        {
            return;
        }

        const auto& rawDrag = *perScreen.rawBounds;
        CursorDrag completedBounds = rawDrag;
        if (perScreen.fitSelectionOnCommit && perScreen.snapFrame)
        {
            const RECT rawBounds = BoundsSnapModel::NormalizeBounds(
                POINT{
                    .x = static_cast<LONG>(rawDrag.startPos.x),
                    .y = static_cast<LONG>(rawDrag.startPos.y),
                },
                POINT{
                    .x = static_cast<LONG>(rawDrag.currentPos.x),
                    .y = static_cast<LONG>(rawDrag.currentPos.y),
                });
            const auto fittedBounds = BoundsSnapModel::FitSelectionToContent(
                perScreen.snapFrame->view,
                rawBounds,
                toolState->global.perColorChannelEdgeDetection,
                toolState->global.pixelTolerance);
            if (fittedBounds)
            {
                completedBounds = ApplySnappedBounds(rawDrag, *fittedBounds);
            }
        }

        perScreen.currentBounds = completedBounds;
        const auto px2mmRatio = toolState->commonState->GetPhysicalPx2MmRatio(window);
        const Measurement measurement = GetMeasurement(completedBounds, px2mmRatio);
        if (!perScreen.appendMeasurement)
        {
            for (auto& screen : toolState->perScreen)
            {
                screen.second.measurements.clear();
            }
        }
        perScreen.measurements.push_back(measurement);

        perScreen.rawBounds.reset();
        perScreen.currentBounds.reset();
        perScreen.snapFrame.reset();
        perScreen.waitingForSnapFrame = false;
        perScreen.snapFrameReady = false;
        perScreen.appendMeasurement = false;
        perScreen.fitSelectionOnCommit = false;
        CopyToClipboard(window, *toolState);
    }

    void HandleCursorMove(HWND window, BoundsToolState* toolState, const POINT cursorPos, const DWORD touchID = 0)
    {
        auto& perScreen = toolState->perScreen[window];
        if (!perScreen.rawBounds ||
            perScreen.rawBounds->touchID != touchID ||
            perScreen.waitingForSnapFrame)
        {
            return;
        }

        perScreen.rawBounds->currentPos =
            D2D_POINT_2F{ .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
        perScreen.currentBounds = perScreen.rawBounds;
    }

    void HandleCursorDown(HWND window, BoundsToolState* toolState, const POINT cursorPos, const DWORD touchID = 0)
    {
        ToggleCursor(false);

        RECT windowRect;
        if (GetWindowRect(window, &windowRect))
            ClipCursor(&windowRect);

        const D2D_POINT_2F newBoundsStart = { .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
        auto& perScreen = toolState->perScreen[window];
        perScreen.snapFrame.reset();
        perScreen.waitingForSnapFrame = false;
        perScreen.snapFrameReady = false;
        perScreen.appendMeasurement = false;
        perScreen.fitSelectionOnCommit = false;
        perScreen.rawBounds = CursorDrag{
            .startPos = newBoundsStart,
            .currentPos = newBoundsStart,
            .touchID = touchID
        };
        perScreen.currentBounds = perScreen.rawBounds;
        if (touchID == 0 && perScreen.requestSnapFrame)
        {
            perScreen.requestSnapFrame(++perScreen.snapCaptureGeneration);
        }
    }

    void HandleCursorUp(HWND window, BoundsToolState* toolState)
    {
        ToggleCursor(true);
        ClipCursor(nullptr);

        auto& perScreen = toolState->perScreen[window];
        if (!perScreen.rawBounds)
        {
            return;
        }

        perScreen.appendMeasurement = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
        perScreen.fitSelectionOnCommit =
            perScreen.rawBounds->touchID == 0 &&
            !IsAltPressed();
        if (perScreen.fitSelectionOnCommit && !perScreen.snapFrameReady)
        {
            perScreen.waitingForSnapFrame = true;
            return;
        }

        CommitCurrentBounds(window, toolState);
    }
}

LRESULT CALLBACK BoundsToolWndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_CREATE:
    {
        auto toolState = GetWindowCreateParam<BoundsToolState*>(lparam);
        StoreWindowParam(window, toolState);
        break;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_BOUNDS_SNAP_FRAME_READY:
    {
        const auto frameMessage = reinterpret_cast<const BoundsSnapFrameMessage*>(lparam);
        auto* toolState = GetWindowParam<BoundsToolState*>(window);
        if (!frameMessage || !toolState)
        {
            return FALSE;
        }

        auto& perScreen = toolState->perScreen[window];
        if (frameMessage->generation != perScreen.snapCaptureGeneration)
        {
            return FALSE;
        }

        perScreen.snapFrame = frameMessage->frame;
        perScreen.snapFrameReady = true;
        if (perScreen.waitingForSnapFrame)
        {
            CommitCurrentBounds(window, toolState);
        }
        return TRUE;
    }
    case WM_KEYUP:
        if (wparam == VK_ESCAPE)
        {
            if (const auto* toolState = GetWindowParam<BoundsToolState*>(window))
            {
                CopyToClipboard(window, *toolState);
            }

            PostMessageW(window, WM_CLOSE, {}, {});
        }
        break;
    case WM_LBUTTONDOWN:
    {
        const bool touchEvent = (GetMessageExtraInfo() & consts::MOUSEEVENTF_FROMTOUCH) == consts::MOUSEEVENTF_FROMTOUCH;
        if (touchEvent)
            break;

        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;

        HandleCursorDown(window,
                         toolState,
                         convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace));
        break;
    }
    case WM_CURSOR_LEFT_MONITOR:
    {
        ToggleCursor(true);

        ClipCursor(nullptr);
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;
        auto& perScreen = toolState->perScreen[window];
        perScreen.rawBounds.reset();
        perScreen.currentBounds.reset();
        perScreen.waitingForSnapFrame = false;
        perScreen.snapFrameReady = false;
        perScreen.fitSelectionOnCommit = false;
        break;
    }
    case WM_TOUCH:
    {
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;
        std::array<TOUCHINPUT, 8> inputs;
        const size_t nInputs = std::min(static_cast<size_t>(LOWORD(wparam)), inputs.size());
        const auto inputHandle = std::bit_cast<HTOUCHINPUT>(lparam);
        GetTouchInputInfo(inputHandle, static_cast<UINT>(nInputs), inputs.data(), sizeof(TOUCHINPUT));

        for (UINT i = 0; i < nInputs; ++i)
        {
            const auto& input = inputs[i];

            if (const bool down = (input.dwFlags & TOUCHEVENTF_DOWN) && (input.dwFlags & TOUCHEVENTF_PRIMARY); down)
            {
                HandleCursorDown(
                    window,
                    toolState,
                    POINT{ TOUCH_COORD_TO_PIXEL(input.x), TOUCH_COORD_TO_PIXEL(input.y) },
                    input.dwID);
                continue;
            }

            if (const bool up = input.dwFlags & TOUCHEVENTF_UP; up)
            {
                HandleCursorMove(
                    window,
                    toolState,
                    POINT{ TOUCH_COORD_TO_PIXEL(input.x), TOUCH_COORD_TO_PIXEL(input.y) },
                    input.dwID);
                HandleCursorUp(window, toolState);
                continue;
            }

            if (const bool move = input.dwFlags & TOUCHEVENTF_MOVE; move)
            {
                HandleCursorMove(window,
                                 toolState,
                                 POINT{ TOUCH_COORD_TO_PIXEL(input.x), TOUCH_COORD_TO_PIXEL(input.y) },
                                 input.dwID);
                continue;
            }
        }

        CloseTouchInputHandle(inputHandle);
        break;
    }

    case WM_MOUSEMOVE:
    {
        const bool touchEvent = (GetMessageExtraInfo() & consts::MOUSEEVENTF_FROMTOUCH) == consts::MOUSEEVENTF_FROMTOUCH;
        if (touchEvent)
            break;

        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;

        HandleCursorMove(window,
                         toolState,
                         convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace));
        break;
    }

    case WM_LBUTTONUP:
    {
        const bool touchEvent = (GetMessageExtraInfo() & consts::MOUSEEVENTF_FROMTOUCH) == consts::MOUSEEVENTF_FROMTOUCH;
        if (touchEvent)
            break;

        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;

        HandleCursorMove(
            window,
            toolState,
            convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace));
        HandleCursorUp(window, toolState);
        break;
    }
    case WM_RBUTTONUP:
    {
        const bool touchEvent = (GetMessageExtraInfo() & consts::MOUSEEVENTF_FROMTOUCH) == consts::MOUSEEVENTF_FROMTOUCH;
        if (touchEvent)
            break;

        ToggleCursor(true);

        auto* toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;

        auto& perScreen = toolState->perScreen[window];

        if (perScreen.currentBounds)
        {
            perScreen.rawBounds.reset();
            perScreen.currentBounds.reset();
            perScreen.waitingForSnapFrame = false;
            perScreen.snapFrameReady = false;
            perScreen.fitSelectionOnCommit = false;
        }
        else
        {
            if (perScreen.measurements.empty())
            {
                PostMessageW(window, WM_CLOSE, {}, {});
            }
            else
            {
                perScreen.measurements.clear();
            }
        }
        break;
    }
    }

    return DefWindowProcW(window, message, wparam, lparam);
}

namespace
{
    void DrawMeasurement(const Measurement& measurement,
                         const CommonState& commonState,
                         HWND window,
                         const D2DState& d2dState,
                         std::optional<D2D_POINT_2F> textBoxCenter)
    {
        const bool screenQuadrantAware = textBoxCenter.has_value();
        d2dState.ToggleAliasedLinesMode(true);
        d2dState.dxgiWindowState.rt->DrawRectangle(measurement.rect, d2dState.solidBrushes[Brush::line].get());
        d2dState.ToggleAliasedLinesMode(false);

        OverlayBoxText text;
        const auto [crossSymbolPos, measureStringBufLen] =
            measurement.Print(text.buffer.data(),
                              text.buffer.size(),
                              true,
                              true,
                              commonState.units | Measurement::Unit::Pixel); // Always show pixels.

        D2D_POINT_2F textBoxPos;
        if (textBoxCenter)
            textBoxPos = *textBoxCenter;
        else
        {
            textBoxPos.x = measurement.rect.left + measurement.Width(Measurement::Unit::Pixel) / 2;
            textBoxPos.y = measurement.rect.top + measurement.Height(Measurement::Unit::Pixel) / 2;
        }

        d2dState.DrawTextBox(text.buffer.data(),
                             measureStringBufLen,
                             crossSymbolPos,
                             textBoxPos,
                             screenQuadrantAware ? TextBoxPlacement::CursorQuadrant :
                                                   TextBoxPlacement::OutsideRectangle,
                             window,
                             screenQuadrantAware ? std::nullopt :
                                                   std::optional<D2D1_RECT_F>{ measurement.rect });
    }
}

void DrawBoundsToolTick(const CommonState& commonState,
                        const BoundsToolState& toolState,
                        const HWND window,
                        const D2DState& d2dState)
{
    const auto it = toolState.perScreen.find(window);
    if (it == end(toolState.perScreen))
        return;

    d2dState.dxgiWindowState.rt->Clear();

    const auto& perScreen = it->second;
    for (const auto& measure : perScreen.measurements)
        DrawMeasurement(measure, commonState, window, d2dState, {});

    if (perScreen.currentBounds.has_value())
    {
        D2D1_RECT_F rect;
        std::tie(rect.left, rect.right) = std::minmax(perScreen.currentBounds->startPos.x, perScreen.currentBounds->currentPos.x);
        std::tie(rect.top, rect.bottom) = std::minmax(perScreen.currentBounds->startPos.y, perScreen.currentBounds->currentPos.y);
        auto px2mmRatio = toolState.commonState->GetPhysicalPx2MmRatio(window);
        DrawMeasurement(Measurement{ rect, px2mmRatio }, commonState, window, d2dState, perScreen.currentBounds->currentPos);
    }
}
