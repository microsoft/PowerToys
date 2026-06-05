#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>

enum struct CrosshairsOrientation : int
{
    Both = 0,
    VerticalOnly = 1,
    HorizontalOnly = 2,
};

struct CrosshairsVisualBounds
{
    float offsetX = 0.0f;
    float offsetY = 0.0f;
    float width = 0.0f;
    float height = 0.0f;
};

struct CrosshairsLineBounds
{
    CrosshairsVisualBounds border;
    CrosshairsVisualBounds line;
};

struct CrosshairsLayout
{
    CrosshairsLineBounds left;
    CrosshairsLineBounds right;
    CrosshairsLineBounds top;
    CrosshairsLineBounds bottom;
};

struct CrosshairsLayoutInput
{
    POINT cursorPosition{};
    POINT monitorUpperLeft{};
    POINT monitorBottomRight{};
    int crosshairsRadius = 0;
    int crosshairsThickness = 0;
    int crosshairsBorderSize = 0;
    bool crosshairsIsFixedLengthEnabled = false;
    int crosshairsFixedLength = 0;
    CrosshairsOrientation crosshairsOrientation = CrosshairsOrientation::Both;
};

CrosshairsLayout CalculateCrosshairsLayout(const CrosshairsLayoutInput& input) noexcept;
