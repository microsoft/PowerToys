// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include "Geometry.h"

#include <array>
#include <cstdint>
#include <limits>
#include <optional>

namespace RegionMirror
{
    inline constexpr LONG MaximumWindowFrameMargin = 256;
    inline constexpr LONG MaximumWindowDimension = MaximumRegionDimension + 2 * MaximumWindowFrameMargin;

    inline bool ObserveMaximized(bool& wasMaximized, bool maximized, bool dragging) noexcept
    {
        const bool previous = wasMaximized;
        wasMaximized = maximized;
        return !previous && maximized && !dragging;
    }

    namespace detail
    {
        inline bool IsValidWindowRectangle(const RECT& rectangle) noexcept
        {
            const auto width = static_cast<std::int64_t>(rectangle.right) - rectangle.left;
            const auto height = static_cast<std::int64_t>(rectangle.bottom) - rectangle.top;
            return width > 0 && height > 0 && width <= MaximumWindowDimension && height <= MaximumWindowDimension;
        }
    }

    // Measure each signed physical-pixel frame margin independently. Some window styles report
    // visible bounds outside their outer bounds, so bounded negative margins are supported too.
    inline std::optional<RECT> ComputeOuterBoundsForVisibleRect(const RECT& desiredVisible, const RECT& currentOuter, const RECT& currentVisible) noexcept
    {
        if (!IsValidRegion(desiredVisible) || !detail::IsValidWindowRectangle(currentOuter) || !detail::IsValidWindowRectangle(currentVisible))
        {
            return std::nullopt;
        }

        const auto leftMargin = static_cast<std::int64_t>(currentVisible.left) - currentOuter.left;
        const auto topMargin = static_cast<std::int64_t>(currentVisible.top) - currentOuter.top;
        const auto rightMargin = static_cast<std::int64_t>(currentOuter.right) - currentVisible.right;
        const auto bottomMargin = static_cast<std::int64_t>(currentOuter.bottom) - currentVisible.bottom;
        for (const auto margin : std::array{ leftMargin, topMargin, rightMargin, bottomMargin })
        {
            if (margin < -MaximumWindowFrameMargin || margin > MaximumWindowFrameMargin)
            {
                return std::nullopt;
            }
        }

        const auto left = static_cast<std::int64_t>(desiredVisible.left) - leftMargin;
        const auto top = static_cast<std::int64_t>(desiredVisible.top) - topMargin;
        const auto right = static_cast<std::int64_t>(desiredVisible.right) + rightMargin;
        const auto bottom = static_cast<std::int64_t>(desiredVisible.bottom) + bottomMargin;
        for (const auto edge : std::array{ left, top, right, bottom })
        {
            if (edge < (std::numeric_limits<LONG>::min)() || edge > (std::numeric_limits<LONG>::max)())
            {
                return std::nullopt;
            }
        }

        const RECT outer{ static_cast<LONG>(left), static_cast<LONG>(top), static_cast<LONG>(right), static_cast<LONG>(bottom) };
        return detail::IsValidWindowRectangle(outer) ? std::optional<RECT>{ outer } : std::nullopt;
    }
}
