// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <string_view>

namespace RegionMirror
{
    inline constexpr LONG MaximumRegionDimension = 16384;

    // All rectangles use physical screen pixels and exclusive right/bottom edges.
    inline RECT NormalizeSelection(POINT first, POINT second) noexcept
    {
        return {
            (std::min)(first.x, second.x),
            (std::min)(first.y, second.y),
            (std::max)(first.x, second.x),
            (std::max)(first.y, second.y)
        };
    }

    inline bool IsValidRegion(const RECT& region) noexcept
    {
        const auto width = static_cast<std::int64_t>(region.right) - region.left;
        const auto height = static_cast<std::int64_t>(region.bottom) - region.top;
        return width > 0 && height > 0 && width <= MaximumRegionDimension && height <= MaximumRegionDimension;
    }

    inline bool IsContained(const RECT& region, const RECT& bounds) noexcept
    {
        return IsValidRegion(region) && bounds.left < bounds.right && bounds.top < bounds.bottom &&
               region.left >= bounds.left && region.top >= bounds.top &&
               region.right <= bounds.right && region.bottom <= bounds.bottom;
    }

    // Invalid rectangles, or fits smaller than one pixel on either axis, return an empty rectangle.
    // Rounding down keeps the content inside the target; odd padding leaves the extra pixel at right/bottom.
    inline RECT FitAspect(const RECT& content, const RECT& target) noexcept
    {
        if (!IsValidRegion(content) || !IsValidRegion(target))
        {
            return {};
        }

        const auto contentWidth = static_cast<std::int64_t>(content.right) - content.left;
        const auto contentHeight = static_cast<std::int64_t>(content.bottom) - content.top;
        const auto targetWidth = static_cast<std::int64_t>(target.right) - target.left;
        const auto targetHeight = static_cast<std::int64_t>(target.bottom) - target.top;

        auto width = targetWidth;
        auto height = targetHeight;
        if (targetWidth * contentHeight <= targetHeight * contentWidth)
        {
            height = targetWidth * contentHeight / contentWidth;
        }
        else
        {
            width = targetHeight * contentWidth / contentHeight;
        }

        if (width == 0 || height == 0)
        {
            return {};
        }

        const auto left = static_cast<std::int64_t>(target.left) + (targetWidth - width) / 2;
        const auto top = static_cast<std::int64_t>(target.top) + (targetHeight - height) / 2;
        return {
            static_cast<LONG>(left),
            static_cast<LONG>(top),
            static_cast<LONG>(left + width),
            static_cast<LONG>(top + height)
        };
    }

    namespace detail
    {
        inline bool TryParseCoordinate(std::wstring_view text, std::size_t& position, LONG& result) noexcept
        {
            if (position == text.size())
            {
                return false;
            }

            const bool negative = text[position] == L'-';
            if (negative)
            {
                ++position;
            }

            if (position == text.size() || text[position] < L'0' || text[position] > L'9')
            {
                return false;
            }

            const std::int64_t limit = negative ? -static_cast<std::int64_t>((std::numeric_limits<LONG>::min)()) : (std::numeric_limits<LONG>::max)();
            std::int64_t value = 0;
            while (position < text.size() && text[position] >= L'0' && text[position] <= L'9')
            {
                const auto digit = text[position] - L'0';
                if (value > (limit - digit) / 10)
                {
                    return false;
                }

                value = value * 10 + digit;
                ++position;
            }

            result = static_cast<LONG>(negative ? -value : value);
            return true;
        }
    }

    // The CLI grammar is x,y,width,height: ASCII decimal integers, optional '-', no whitespace or '+'.
    // Width and height must be positive. Failure leaves the caller's rectangle unchanged.
    inline bool TryParseRegion(std::wstring_view text, RECT& out) noexcept
    {
        std::array<LONG, 4> values{};
        std::size_t position = 0;
        for (std::size_t i = 0; i < values.size(); ++i)
        {
            if (!detail::TryParseCoordinate(text, position, values[i]))
            {
                return false;
            }

            if (i + 1 < values.size())
            {
                if (position == text.size() || text[position] != L',')
                {
                    return false;
                }

                ++position;
            }
        }

        if (position != text.size() || values[2] <= 0 || values[3] <= 0 ||
            values[2] > MaximumRegionDimension || values[3] > MaximumRegionDimension)
        {
            return false;
        }

        const auto right = static_cast<std::int64_t>(values[0]) + values[2];
        const auto bottom = static_cast<std::int64_t>(values[1]) + values[3];
        if (right > (std::numeric_limits<LONG>::max)() || bottom > (std::numeric_limits<LONG>::max)())
        {
            return false;
        }

        const RECT region{ values[0], values[1], static_cast<LONG>(right), static_cast<LONG>(bottom) };
        if (!IsValidRegion(region))
        {
            return false;
        }

        out = region;
        return true;
    }
}
