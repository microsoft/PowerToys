#pragma once

namespace MeasurementTooltipStyle
{
    inline constexpr float SecondaryTextOpacity = 0.25f;
    inline constexpr float OutsideSelectionGap = 8.0f;

    struct Color
    {
        float red;
        float green;
        float blue;
        float alpha;
    };

    struct Palette
    {
        Color foreground;
        Color secondaryForeground;
        Color background;
        Color border;
    };

    enum class PlacementSide
    {
        Top,
        Bottom,
        Right,
        Left,
    };

    struct Rect
    {
        float left;
        float top;
        float right;
        float bottom;
    };

    struct Size
    {
        float width;
        float height;
    };

    struct Point
    {
        float x;
        float y;
    };

    struct Placement
    {
        Point center;
        PlacementSide side;
    };

    constexpr float ClampCenter(float value, float contentLength, float viewportLength) noexcept
    {
        if (contentLength >= viewportLength)
        {
            return viewportLength / 2.0f;
        }

        const float halfLength = contentLength / 2.0f;
        return value < halfLength ? halfLength :
               value > viewportLength - halfLength ? viewportLength - halfLength :
                                                      value;
    }

    constexpr Placement PlaceOutsideSelection(
        Rect selection,
        Size tooltip,
        Size viewport,
        float gap = OutsideSelectionGap) noexcept
    {
        const float topSpace = selection.top;
        const float bottomSpace = viewport.height - selection.bottom;
        const float rightSpace = viewport.width - selection.right;
        const float leftSpace = selection.left;
        const float verticalRequirement = tooltip.height + gap;
        const float horizontalRequirement = tooltip.width + gap;

        PlacementSide side = PlacementSide::Top;
        if (topSpace >= verticalRequirement)
        {
            side = PlacementSide::Top;
        }
        else if (bottomSpace >= verticalRequirement)
        {
            side = PlacementSide::Bottom;
        }
        else if (rightSpace >= horizontalRequirement)
        {
            side = PlacementSide::Right;
        }
        else if (leftSpace >= horizontalRequirement)
        {
            side = PlacementSide::Left;
        }
        else
        {
            float bestRemainingSpace = topSpace - verticalRequirement;
            const auto consider = [&side, &bestRemainingSpace](
                                      PlacementSide candidate,
                                      float remainingSpace) constexpr {
                if (remainingSpace > bestRemainingSpace)
                {
                    side = candidate;
                    bestRemainingSpace = remainingSpace;
                }
            };
            consider(PlacementSide::Bottom, bottomSpace - verticalRequirement);
            consider(PlacementSide::Right, rightSpace - horizontalRequirement);
            consider(PlacementSide::Left, leftSpace - horizontalRequirement);
        }

        const float selectionCenterX = (selection.left + selection.right) / 2.0f;
        const float selectionCenterY = (selection.top + selection.bottom) / 2.0f;
        switch (side)
        {
        case PlacementSide::Top:
            return {
                .center = {
                    .x = ClampCenter(selectionCenterX, tooltip.width, viewport.width),
                    .y = ClampCenter(
                        selection.top - gap - tooltip.height / 2.0f,
                        tooltip.height,
                        viewport.height),
                },
                .side = side,
            };
        case PlacementSide::Bottom:
            return {
                .center = {
                    .x = ClampCenter(selectionCenterX, tooltip.width, viewport.width),
                    .y = ClampCenter(
                        selection.bottom + gap + tooltip.height / 2.0f,
                        tooltip.height,
                        viewport.height),
                },
                .side = side,
            };
        case PlacementSide::Right:
            return {
                .center = {
                    .x = ClampCenter(
                        selection.right + gap + tooltip.width / 2.0f,
                        tooltip.width,
                        viewport.width),
                    .y = ClampCenter(selectionCenterY, tooltip.height, viewport.height),
                },
                .side = side,
            };
        case PlacementSide::Left:
            return {
                .center = {
                    .x = ClampCenter(
                        selection.left - gap - tooltip.width / 2.0f,
                        tooltip.width,
                        viewport.width),
                    .y = ClampCenter(selectionCenterY, tooltip.height, viewport.height),
                },
                .side = side,
            };
        }

        return {};
    }

    constexpr Palette PaletteForTheme(bool darkMode) noexcept
    {
        constexpr Color border{ 0.44f, 0.44f, 0.44f, 0.4f };
        if (darkMode)
        {
            return {
                .foreground = { 1.0f, 1.0f, 1.0f, 1.0f },
                .secondaryForeground = { 1.0f, 1.0f, 1.0f, SecondaryTextOpacity },
                .background = { 0.17f, 0.17f, 0.17f, 0.93f },
                .border = border,
            };
        }

        return {
            .foreground = { 0.0f, 0.0f, 0.0f, 1.0f },
            .secondaryForeground = { 0.0f, 0.0f, 0.0f, SecondaryTextOpacity },
            .background = { 0.96f, 0.96f, 0.96f, 0.93f },
            .border = border,
        };
    }
}
