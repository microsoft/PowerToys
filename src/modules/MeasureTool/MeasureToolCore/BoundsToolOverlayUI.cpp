#include "pch.h"
#include "BoundsToolOverlayUI.h"
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
    case WM_CLOSE:
        DestroyWindow(window);
        break;
    case WM_KEYUP:
        if (wparam == VK_ESCAPE)
        {
            PostMessageW(window, WM_CLOSE, {}, {});
        }
        break;
    case WM_LBUTTONDOWN:
    {
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;
        POINT cursorPos = toolState->commonState->cursorPos;
        ScreenToClient(window, &cursorPos);

        D2D_POINT_2F newRegionStart = { .x = static_cast<float>(cursorPos.x), .y = static_cast<float>(cursorPos.y) };
        toolState->currentRegionStart = newRegionStart;
        break;
    }
    // We signal this when active monitor has changed -> must reset the state
    case WM_USER:
    {
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;
        toolState->currentRegionStart = std::nullopt;
        break;
    }
    case WM_LBUTTONUP:
    {
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState || !toolState->currentRegionStart)
            break;

        toolState->commonState->overlayBoxText.Read([](const OverlayBoxText& text) {
            SetClipBoardToText(text.buffer);
        });

        if (const bool shiftPress = GetKeyState(VK_SHIFT) & 0x8000; shiftPress)
        {
            auto cursorPos = toolState->commonState->cursorPos;
            ScreenToClient(window, &cursorPos);

            D2D1_RECT_F rect;
            std::tie(rect.left, rect.right) = std::minmax(static_cast<float>(cursorPos.x), toolState->currentRegionStart->x);
            std::tie(rect.top, rect.bottom) = std::minmax(static_cast<float>(cursorPos.y), toolState->currentRegionStart->y);
            toolState->measurementsByScreen[window].push_back(rect);
        }

        toolState->currentRegionStart = std::nullopt;
        break;
    }
    case WM_RBUTTONUP:
    {
        auto toolState = GetWindowParam<BoundsToolState*>(window);
        if (!toolState)
            break;

        if (toolState->currentRegionStart)
            toolState->currentRegionStart = std::nullopt;
        else
        {
            if (toolState->measurementsByScreen[window].empty())
                PostMessageW(window, WM_CLOSE, {}, {});
            else
                toolState->measurementsByScreen[window].clear();
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
                         HWND overlayWindow,
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
        std::optional<size_t> crossSymbolPos = wcsstr(text.buffer.data(), L" ") - text.buffer.data() + 1;

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
                             overlayWindow);
    }
}

void DrawBoundsToolTick(const CommonState& commonState,
                        const BoundsToolState& toolState,
                        HWND overlayWindow,
                        const D2DState& d2dState)
{
    if (const auto it = toolState.measurementsByScreen.find(overlayWindow); it != end(toolState.measurementsByScreen))
    {
        for (const auto& measure : it->second)
            DrawMeasurement(measure, true, commonState, overlayWindow, d2dState);
    }

    if (!toolState.currentRegionStart.has_value())
        return;

    POINT cursorPos = commonState.cursorPos;
    ScreenToClient(overlayWindow, &cursorPos);

    const D2D1_RECT_F rect{ .left = toolState.currentRegionStart->x,
                            .top = toolState.currentRegionStart->y,
                            .right = static_cast<float>(cursorPos.x),
                            .bottom = static_cast<float>(cursorPos.y) };
    DrawMeasurement(rect, false, commonState, overlayWindow, d2dState);
}
