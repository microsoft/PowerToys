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
        const POINT cursorPos = convert::FromSystemToRelativeForDirect2D(window, toolState->commonState->cursorPosSystemSpace);

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
            const auto cursorPos = convert::FromSystemToRelativeForDirect2D(window, toolState->commonState->cursorPosSystemSpace);

            D2D1_RECT_F rect;
            std::tie(rect.left, rect.right) = std::minmax(static_cast<float>(cursorPos.x), toolState->perScreen[window].currentRegionStart->x);
            std::tie(rect.top, rect.bottom) = std::minmax(static_cast<float>(cursorPos.y), toolState->perScreen[window].currentRegionStart->y);
            toolState->perScreen[window].measurements.push_back(rect);
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
    void DrawMeasurement(const D2D1_RECT_F rect,
                         const bool alignTextBoxToCenter,
                         const CommonState& commonState,
                         HWND window,
                         const D2DState& d2dState)
    {
        const bool screenQuadrantAware = !alignTextBoxToCenter;
        const auto prevMode = d2dState.rt->GetAntialiasMode();
        d2dState.rt->SetAntialiasMode(D2D1_ANTIALIAS_MODE_ALIASED);
        d2dState.rt->DrawRectangle(rect, d2dState.solidBrushes[Brush::line].get());
        d2dState.rt->SetAntialiasMode(prevMode);

        OverlayBoxText text;
        const auto width = std::abs(rect.right - rect.left + 1);
        const auto height = std::abs(rect.top - rect.bottom + 1);
        const uint32_t textLen = swprintf_s(text.buffer.data(),
                                            text.buffer.size(),
                                            L"%.0f × %.0f",
                                            width,
                                            height);
        std::optional<size_t> crossSymbolPos = wcschr(text.buffer.data(), L' ') - text.buffer.data() + 1;

        commonState.overlayBoxText.Access([&](OverlayBoxText& v) {
            v = text;
        });

        float cornerX = rect.right;
        float cornerY = rect.bottom;
        if (alignTextBoxToCenter)
        {
            cornerX = rect.left + width / 2;
            cornerY = rect.top + height / 2;
        }

        d2dState.DrawTextBox(text.buffer.data(),
                             textLen,
                             crossSymbolPos,
                             cornerX,
                             cornerY,
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

    d2dState.rt->Clear();

    const auto& perScreen = it->second;
    for (const auto& measure : perScreen.measurements)
        DrawMeasurement(measure, true, commonState, window, d2dState);

    if (!perScreen.currentRegionStart.has_value())
        return;

    const auto cursorPos = convert::FromSystemToRelativeForDirect2D(window, commonState.cursorPosSystemSpace);

    const D2D1_RECT_F rect{ .left = perScreen.currentRegionStart->x,
                            .top = perScreen.currentRegionStart->y,
                            .right = static_cast<float>(cursorPos.x),
                            .bottom = static_cast<float>(cursorPos.y) };
    DrawMeasurement(rect, false, commonState, window, d2dState);
}
