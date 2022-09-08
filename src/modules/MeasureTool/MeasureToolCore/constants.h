#pragma once

#include <chrono>

namespace consts
{
    constexpr inline size_t TARGET_FRAME_RATE = 90;
    constexpr inline auto TARGET_FRAME_DURATION = std::chrono::microseconds{ 1000000 } / TARGET_FRAME_RATE;

    constexpr inline float FONT_SIZE = 14.f;
    constexpr inline float TEXT_BOX_CORNER_RADIUS = 4.f;
    constexpr inline float FEET_HALF_LENGTH = 2.f;
    constexpr inline float SHADOW_OPACITY = .4f;
    constexpr inline float SHADOW_RADIUS = 6.f;
    constexpr inline float SHADOW_OFFSET = 5.f;
    constexpr inline float CROSS_OPACITY = .25f;
    constexpr inline int8_t MOUSE_WHEEL_TOLERANCE_STEP = 15;
    /* Offset to not try not to use the cursor immediate pixels in measuring, but it seems only necessary for continuous mode. */
    constexpr inline long CURSOR_OFFSET_AMOUNT_X = 4;
    constexpr inline long CURSOR_OFFSET_AMOUNT_Y = 4;
}
