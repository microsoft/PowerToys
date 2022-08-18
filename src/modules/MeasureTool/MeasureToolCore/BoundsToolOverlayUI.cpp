#include "pch.h"
#include "BoundsToolOverlayUI.h"

void DrawBoundsToolTick(const CommonState& commonState,
                        const BoundsToolState& toolState,
                        HWND overlayWindow,
                        const D2DState& d2dState)
{
    if (!toolState.currentRegionStart.has_value())
    {
        return;
    }

    POINT cursorPos = commonState.cursorPos;
    ScreenToClient(overlayWindow, &cursorPos);

    const D2D1_RECT_F rect{ .left = toolState.currentRegionStart->x,
                            .top = toolState.currentRegionStart->y,
                            .right = static_cast<float>(cursorPos.x),
                            .bottom = static_cast<float>(cursorPos.y) };
    d2dState.rt->DrawRectangle(rect, d2dState.solidBrushes[Brush::line].get());

    OverlayBoxText text;
    const uint32_t textLen = swprintf_s(text.buffer.data(),
                                        text.buffer.size(),
                                        L"%.0f x %.0f",
                                        std::abs(rect.right - rect.left + 1),
                                        std::abs(rect.top - rect.bottom + 1));

    commonState.overlayBoxText.Access([&](OverlayBoxText& v) {
        v = text;
    });
    d2dState.DrawTextBox(text.buffer.data(),
                         textLen,
                         toolState.currentRegionStart->x,
                         toolState.currentRegionStart->y,
                         overlayWindow);
}
