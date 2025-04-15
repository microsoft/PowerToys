#include "pch.h"
#include "BoundsToolOverlayUI.h"
#include "CoordinateSystemConversion.h"
#include "Clipboard.h"
#include "constants.h"

#include <common/utils/window.h>
#include <vector>

namespace
{
    Measurement GetMeasurement(const CursorDrag& currentBounds, POINT cursorPos, float px2mmRatio)
    {
        D2D1_RECT_F rect;
        std::tie(rect.left, rect.right) =
            std::minmax(static_cast<float>(cursorPos.x), currentBounds.startPos.x);
        std::tie(rect.top, rect.bottom) =
            std::minmax(static_cast<float>(cursorPos.y), currentBounds.startPos.y);

        return Measurement(rect, px2mmRatio);
    }

    void CopyToClipboard(HWND window, const BoundsToolState& toolState, POINT cursorPos)
    {
        std::vector<Measurement> allMeasurements;
        for (const auto& [handle, perScreen] : toolState.perScreen)
        {
            allMeasurements.append_range(perScreen.measurements);

            if (handle == window && perScreen.currentBounds)
            {
                auto px2mmRatio = toolState.commonState->GetPhysicalPx2MmRatio(window);
                allMeasurements.push_back(GetMeasurement(*perScreen.currentBounds, cursorPos, px2mmRatio));
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

    void HandleCursorMove(HWND window, BoundsToolState* toolState, const POINT cursorPos, const DWORD touchID = 0)
    {
        if (!toolState->perScreen[window].currentBounds || (toolState->perScreen[window].currentBounds->touchID != touchID))
            return;

        toolState->perScreen[window].currentBounds->currentPos =
            D2D_POINT_2F{ .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
    }

    void HandleCursorDown(HWND window, BoundsToolState* toolState, const POINT cursorPos, const DWORD touchID = 0)
    {
        ToggleCursor(false);

        RECT windowRect;
        if (GetWindowRect(window, &windowRect))
            ClipCursor(&windowRect);

        const D2D_POINT_2F newBoundsStart = { .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
        toolState->perScreen[window].currentBounds = CursorDrag{
            .startPos = newBoundsStart,
            .currentPos = newBoundsStart,
            .touchID = touchID
        };
    }

    void HandleCursorUp(HWND window, BoundsToolState* toolState, const POINT cursorPos)
    {
        ToggleCursor(true);
        ClipCursor(nullptr);
        CopyToClipboard(window, *toolState, cursorPos);

        auto& perScreen = toolState->perScreen[window];

        if (const bool shiftPress = GetKeyState(VK_SHIFT) & 0x80000; shiftPress && perScreen.currentBounds)
        {
            auto px2mmRatio = toolState->commonState->GetPhysicalPx2MmRatio(window);
            perScreen.measurements.push_back(GetMeasurement(*perScreen.currentBounds, cursorPos, px2mmRatio));
        }

        perScreen.currentBounds = std::nullopt;
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
    case WM_KEYUP:
        if (wparam == VK_ESCAPE)
        {
            if (const auto* toolState = GetWindowParam<BoundsToolState*>(window))
            {
                CopyToClipboard(window, *toolState, convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace));
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
        toolState->perScreen[window].currentBounds = std::nullopt;
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
                HandleCursorUp(
                    window,
                    toolState,
                    POINT{ TOUCH_COORD_TO_PIXEL(input.x), TOUCH_COORD_TO_PIXEL(input.y) });
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

        HandleCursorUp(window,
                       toolState,
                       convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace));
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
            perScreen.currentBounds = std::nullopt;
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
                             screenQuadrantAware,
                             window);
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
