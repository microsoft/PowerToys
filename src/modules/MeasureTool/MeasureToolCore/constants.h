#pragma once

#include <chrono>

namespace consts
{
    constexpr inline size_t TARGET_FRAME_RATE = 120;
    constexpr inline auto TARGET_FRAME_DURATION = std::chrono::milliseconds{ 1000 } / TARGET_FRAME_RATE;

    constexpr inline float FONT_SIZE = 14.f;
    constexpr inline float TEXT_BOX_CORNER_RADIUS = 4.f;
    constexpr inline float FEET_HALF_LENGTH = 2.f;

}