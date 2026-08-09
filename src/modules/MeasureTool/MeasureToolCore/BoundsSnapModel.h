#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

#include "BGRATextureView.h"

#include <algorithm>
#include <optional>

namespace BoundsSnapModel
{
    constexpr RECT NormalizeBounds(POINT start, POINT current)
    {
        return RECT{
            .left = std::min(start.x, current.x),
            .top = std::min(start.y, current.y),
            .right = std::max(start.x, current.x),
            .bottom = std::max(start.y, current.y),
        };
    }

    std::optional<RECT> FitSelectionToContent(
        const BGRATextureView& texture,
        const RECT& selection,
        bool perColorChannel,
        uint8_t tolerance);
}
