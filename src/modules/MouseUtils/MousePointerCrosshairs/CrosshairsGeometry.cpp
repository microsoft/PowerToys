#include "CrosshairsGeometry.h"

namespace
{
    constexpr CrosshairsVisualBounds MakeBounds(
        float offsetX,
        float offsetY,
        float width,
        float height) noexcept
    {
        return { offsetX, offsetY, width, height };
    }
}

CrosshairsLayout CalculateCrosshairsLayout(const CrosshairsLayoutInput& input) noexcept
{
    CrosshairsLayout layout{};

    const float cursorX = static_cast<float>(input.cursorPosition.x);
    const float cursorY = static_cast<float>(input.cursorPosition.y);
    const float monitorLeft = static_cast<float>(input.monitorUpperLeft.x);
    const float monitorTop = static_cast<float>(input.monitorUpperLeft.y);
    const float monitorRight = static_cast<float>(input.monitorBottomRight.x);
    const float monitorBottom = static_cast<float>(input.monitorBottomRight.y);
    const float radius = static_cast<float>(input.crosshairsRadius);
    const float thickness = static_cast<float>(input.crosshairsThickness);
    const float borderSize = static_cast<float>(input.crosshairsBorderSize);
    const float halfPixelAdjustment = input.crosshairsThickness % 2 == 1 ? 0.5f : 0.0f;
    const float borderSizePadding = borderSize * 2.0f;
    const float fixedLength = static_cast<float>(input.crosshairsFixedLength);

    if (input.crosshairsOrientation == CrosshairsOrientation::Both || input.crosshairsOrientation == CrosshairsOrientation::HorizontalOnly)
    {
        const float leftCrosshairsFullScreenLength = cursorX - monitorLeft - radius + halfPixelAdjustment * 2.0f;
        const float leftCrosshairsLength = input.crosshairsIsFixedLengthEnabled ? fixedLength : leftCrosshairsFullScreenLength;
        const float leftCrosshairsBorderLength = input.crosshairsIsFixedLengthEnabled ? fixedLength + borderSizePadding : leftCrosshairsFullScreenLength + borderSize;
        layout.left.border = MakeBounds(
            cursorX - radius + borderSize + halfPixelAdjustment * 2.0f,
            cursorY + halfPixelAdjustment,
            leftCrosshairsBorderLength,
            thickness + borderSizePadding);
        layout.left.line = MakeBounds(
            cursorX - radius + halfPixelAdjustment * 2.0f,
            cursorY + halfPixelAdjustment,
            leftCrosshairsLength,
            thickness);

        const float rightCrosshairsFullScreenLength = monitorRight - cursorX - radius;
        const float rightCrosshairsLength = input.crosshairsIsFixedLengthEnabled ? fixedLength : rightCrosshairsFullScreenLength;
        const float rightCrosshairsBorderLength = input.crosshairsIsFixedLengthEnabled ? fixedLength + borderSizePadding : rightCrosshairsFullScreenLength + borderSize;
        layout.right.border = MakeBounds(
            cursorX + radius - borderSize,
            cursorY + halfPixelAdjustment,
            rightCrosshairsBorderLength,
            thickness + borderSizePadding);
        layout.right.line = MakeBounds(
            cursorX + radius,
            cursorY + halfPixelAdjustment,
            rightCrosshairsLength,
            thickness);
    }

    if (input.crosshairsOrientation == CrosshairsOrientation::Both || input.crosshairsOrientation == CrosshairsOrientation::VerticalOnly)
    {
        const float topCrosshairsFullScreenLength = cursorY - monitorTop - radius + halfPixelAdjustment * 2.0f;
        const float topCrosshairsLength = input.crosshairsIsFixedLengthEnabled ? fixedLength : topCrosshairsFullScreenLength;
        const float topCrosshairsBorderLength = input.crosshairsIsFixedLengthEnabled ? fixedLength + borderSizePadding : topCrosshairsFullScreenLength + borderSize;
        layout.top.border = MakeBounds(
            cursorX + halfPixelAdjustment,
            cursorY - radius + borderSize + halfPixelAdjustment * 2.0f,
            thickness + borderSizePadding,
            topCrosshairsBorderLength);
        layout.top.line = MakeBounds(
            cursorX + halfPixelAdjustment,
            cursorY - radius + halfPixelAdjustment * 2.0f,
            thickness,
            topCrosshairsLength);

        const float bottomCrosshairsFullScreenLength = monitorBottom - cursorY - radius;
        const float bottomCrosshairsLength = input.crosshairsIsFixedLengthEnabled ? fixedLength : bottomCrosshairsFullScreenLength;
        const float bottomCrosshairsBorderLength = input.crosshairsIsFixedLengthEnabled ? fixedLength + borderSizePadding : bottomCrosshairsFullScreenLength + borderSize;
        layout.bottom.border = MakeBounds(
            cursorX + halfPixelAdjustment,
            cursorY + radius - borderSize,
            thickness + borderSizePadding,
            bottomCrosshairsBorderLength);
        layout.bottom.line = MakeBounds(
            cursorX + halfPixelAdjustment,
            cursorY + radius,
            thickness,
            bottomCrosshairsLength);
    }

    return layout;
}
