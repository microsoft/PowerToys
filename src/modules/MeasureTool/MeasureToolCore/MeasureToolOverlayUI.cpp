#include "pch.h"
#include "MeasureToolOverlayUI.h"

namespace
{
    inline std::pair<D2D_POINT_2F, D2D_POINT_2F> ComputeCrossFeetLine(D2D_POINT_2F center, const bool horizontal)
    {
        constexpr float FEET_HALF_LENGTH = 2.f;

        D2D_POINT_2F start = center, end = center;
        // Computing in this way to achieve pixel-perfect axial symmetry.
        // TODO: investigate why we need 1.f offset.
        if (horizontal)
        {
            start.x -= FEET_HALF_LENGTH + 1.f;
            end.x += FEET_HALF_LENGTH;

            start.y += 1.f;
            end.y += 1.f;
        }
        else
        {
            start.y -= FEET_HALF_LENGTH + 1.f;
            end.y += FEET_HALF_LENGTH;

            start.x += 1.f;
            end.x += 1.f;
        }

        return { start, end };
    }
}

void DrawMeasureToolTick(Serialized<MeasureToolState>& toolState, HWND overlayWindow, D2DState& d2dState)
{
    MeasureToolState mts;
    toolState.Access([&mts](MeasureToolState& state) {
        mts = state;
    });
    bool drawHorizontalCrossLine = true;
    bool drawVerticalCrossLine = true;

    const bool continuousCapture = mts.continuousCapture;
    const bool drawFeetOnCross = mts.drawFeetOnCross;
    switch (mts.mode)
    {
    case MeasureToolState::Mode::Cross:
        drawHorizontalCrossLine = true;
        drawVerticalCrossLine = true;
        break;
    case MeasureToolState::Mode::Vertical:
        drawHorizontalCrossLine = false;
        drawVerticalCrossLine = true;
        break;
    case MeasureToolState::Mode::Horizontal:
        drawHorizontalCrossLine = true;
        drawVerticalCrossLine = false;
        break;
    }

    // Add 1px to each dim, since the range we obtain from measuredEdges is inclusive.
    const float hMeasure = static_cast<float>(mts.measuredEdges.right - mts.measuredEdges.left + 1);
    const float vMeasure = static_cast<float>(mts.measuredEdges.bottom - mts.measuredEdges.top + 1);

    // Prevent drawing until we get the first capture
    const bool hasMeasure = (mts.measuredEdges.right != mts.measuredEdges.left) && (mts.measuredEdges.bottom != mts.measuredEdges.top);
    if (!hasMeasure)
    {
        return;
    }

    const auto previousAliasingMode = d2dState.rt->GetAntialiasMode();
    // Anti-aliasing is creating artifacts. Aliasing is for drawing straight lines.
    d2dState.rt->SetAntialiasMode(D2D1_ANTIALIAS_MODE_ALIASED);

    if (drawHorizontalCrossLine)
    {
        const D2D_POINT_2F hLineStart{ .x = static_cast<float>(mts.measuredEdges.left), .y = static_cast<float>(mts.cursorPos.y) };
        D2D_POINT_2F hLineEnd{ .x = hLineStart.x + hMeasure, .y = hLineStart.y };
        d2dState.rt->DrawLine(hLineStart, hLineEnd, d2dState.solidBrushes[Brush::line].get());

        if (drawFeetOnCross && !continuousCapture)
        {
            // To fill all pixels which are close, we call DrawLine with end point one pixel too far, since
            // it doesn't get filled, i.e. end point of the range is excluded. However, we want to draw cross
            // feet *on* the last pixel row, so we must subtract 1px from the corresponding axis.
            hLineEnd.x -= 1.f;
            auto [left_start, left_end] = ComputeCrossFeetLine(hLineStart, false);
            auto [right_start, right_end] = ComputeCrossFeetLine(hLineEnd, false);
            d2dState.rt->DrawLine(left_start, left_end, d2dState.solidBrushes[Brush::line].get());
            d2dState.rt->DrawLine(right_start, right_end, d2dState.solidBrushes[Brush::line].get());
        }
    }

    if (drawVerticalCrossLine)
    {
        const D2D_POINT_2F vLineStart{ .x = static_cast<float>(mts.cursorPos.x), .y = static_cast<float>(mts.measuredEdges.top) };
        D2D_POINT_2F vLineEnd{ .x = vLineStart.x, .y = vLineStart.y + vMeasure };
        d2dState.rt->DrawLine(vLineStart, vLineEnd, d2dState.solidBrushes[Brush::line].get());

        if (drawFeetOnCross && !continuousCapture)
        {
            vLineEnd.y -= 1.f;
            auto [top_start, top_end] = ComputeCrossFeetLine(vLineStart, true);
            auto [bottom_start, bottom_end] = ComputeCrossFeetLine(vLineEnd, true);
            d2dState.rt->DrawLine(top_start, top_end, d2dState.solidBrushes[Brush::line].get());
            d2dState.rt->DrawLine(bottom_start, bottom_end, d2dState.solidBrushes[Brush::line].get());
        }
    }

    // After drawing the lines, restore anti aliasing to draw the measurement tooltip.
    d2dState.rt->SetAntialiasMode(previousAliasingMode);

    uint32_t measureStringBufLen = 0;

    // TODO: fix
    wchar_t measureStringBuf[32] = {};

    switch (mts.mode)
    {
    case MeasureToolState::Mode::Cross:
        measureStringBufLen = swprintf_s(measureStringBuf,
                                         L"%.0f x %.0f",
                                         hMeasure,
                                         vMeasure);
        break;
    case MeasureToolState::Mode::Vertical:
        measureStringBufLen = swprintf_s(measureStringBuf,
                                         L"%.0f",
                                         vMeasure);
        break;
    case MeasureToolState::Mode::Horizontal:
        measureStringBufLen = swprintf_s(measureStringBuf,
                                         L"%.0f",
                                         hMeasure);
        break;
    }

    d2dState.DrawTextBox(measureStringBuf,
                         measureStringBufLen,
                         static_cast<float>(mts.cursorPos.x),
                         static_cast<float>(mts.cursorPos.y),
                         overlayWindow);
}
