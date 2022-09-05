#include "pch.h"
#include "BoundsToolOverlayUI.h"
#include "CoordinateSystemConversion.h"
#include "Clipboard.h"

#include <common/utils/window.h>

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
            PostMessageW(window, WM_CLOSE, {}, {});
        }
        break;
    case WM_LBUTTONDOWN:
    {
        for (; ShowCursor(false) >= 0;)
            ;
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;
        const POINT cursorPos = convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace);

        D2D_POINT_2F newRegionStart = { .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
        toolState->perScreen[window].currentRegionStart = newRegionStart;
        break;
    }
    case WM_CURSOR_LEFT_MONITOR:
    {
        for (; ShowCursor(true) < 0;)
            ;
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;
        toolState->perScreen[window].currentRegionStart = std::nullopt;
        break;
    }
    case WM_LBUTTONUP:
    {
        for (; ShowCursor(true) < 0;)
            ;

        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState || !toolState->perScreen[window].currentRegionStart)
            break;

        toolState->commonState->overlayBoxText.Read([](const OverlayBoxText& text) {
            SetClipBoardToText(text.buffer);
        });

        if (const bool shiftPress = GetKeyState(VK_SHIFT) & 0x8000; shiftPress)
        {
            const auto cursorPos = convert::FromSystemToWindow(window, toolState->commonState->cursorPosSystemSpace);

            D2D1_RECT_F rect;
            std::tie(rect.left, rect.right) = std::minmax(static_cast<float>(cursorPos.x), toolState->perScreen[window].currentRegionStart->x);
            std::tie(rect.top, rect.bottom) = std::minmax(static_cast<float>(cursorPos.y), toolState->perScreen[window].currentRegionStart->y);
            toolState->perScreen[window].measurements.push_back(Measurement{ rect });
        }

        toolState->perScreen[window].currentRegionStart = std::nullopt;
        break;
    }
    case WM_RBUTTONUP:
    {
        for (; ShowCursor(true) < 0;)
            ;

        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;

        if (toolState->perScreen[window].currentRegionStart)
            toolState->perScreen[window].currentRegionStart = std::nullopt;
        else
        {
            if (toolState->perScreen[window].measurements.empty())
                PostMessageW(window, WM_CLOSE, {}, {});
            else
                toolState->perScreen[window].measurements.clear();
        }
        break;
    }
    }

    return DefWindowProcW(window, message, wparam, lparam);
}

namespace
{
    void DrawMeasurement(const Measurement& measurement,
                         const bool alignTextBoxToCenter,
                         const CommonState& commonState,
                         HWND window,
                         const D2DState& d2dState,
                         float mouseX,
                         float mouseY)
    {
        const bool screenQuadrantAware = !alignTextBoxToCenter;
        d2dState.ToggleAliasedLinesMode(true);
        d2dState.dxgiWindowState.rt->DrawRectangle(measurement.rect, d2dState.solidBrushes[Brush::line].get());
        d2dState.ToggleAliasedLinesMode(false);

        OverlayBoxText text;
        const auto [crossSymbolPos, measureStringBufLen] =
            measurement.Print(text.buffer.data(),
                              text.buffer.size(),
                              true,
                              true,
                              commonState.units);

        commonState.overlayBoxText.Access([&](OverlayBoxText& v) {
            v = text;
        });

        if (alignTextBoxToCenter)
        {
            mouseX = measurement.rect.left + measurement.Width(Measurement::Unit::Pixel) / 2;
            mouseY = measurement.rect.top + measurement.Height(Measurement::Unit::Pixel) / 2;
        }

        d2dState.DrawTextBox(text.buffer.data(),
                             measureStringBufLen,
                             crossSymbolPos,
                             mouseX,
                             mouseY,
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
        DrawMeasurement(measure, true, commonState, window, d2dState, measure.rect.right, measure.rect.bottom);

    if (!perScreen.currentRegionStart.has_value())
        return;

    const auto cursorPos = convert::FromSystemToWindow(window, commonState.cursorPosSystemSpace);

    D2D1_RECT_F rect;
    const float cursorX = static_cast<float>(cursorPos.x);
    const float cursorY = static_cast<float>(cursorPos.y);
    std::tie(rect.left, rect.right) = std::minmax(cursorX, perScreen.currentRegionStart->x);
    std::tie(rect.top, rect.bottom) = std::minmax(cursorY, perScreen.currentRegionStart->y);
    DrawMeasurement(Measurement{ rect }, false, commonState, window, d2dState, cursorX, cursorY);
}
