// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <limits>
#include <memory>
#include <optional>
#include <type_traits>
#include <vector>

namespace mouse_pointer_crosshairs
{
    struct BitmapDeleter
    {
        void operator()(HBITMAP bitmap) const noexcept
        {
            DeleteObject(bitmap);
        }
    };

    inline std::optional<bool> IsCursorVisible(const CURSORINFO& cursorInfo)
    {
        if (!(cursorInfo.flags & CURSOR_SHOWING))
        {
            return false;
        }

        ICONINFO iconInfo{};
        if (!GetIconInfo(cursorInfo.hCursor, &iconInfo))
        {
            return std::nullopt;
        }

        const std::unique_ptr<std::remove_pointer_t<HBITMAP>, BitmapDeleter> mask{ iconInfo.hbmMask };
        const std::unique_ptr<std::remove_pointer_t<HBITMAP>, BitmapDeleter> color{ iconInfo.hbmColor };
        if (color)
        {
            return true;
        }

        // Auto Hide Cursor replaces the system cursors with transparent monochrome images;
        // this does not clear CURSOR_SHOWING. Their AND mask is 1 and XOR mask is 0.
        BITMAP bitmap{};
        if (!GetObjectW(mask.get(), sizeof(bitmap), &bitmap) ||
            bitmap.bmWidth <= 0 || bitmap.bmHeight <= 0 || bitmap.bmWidthBytes <= 0 ||
            bitmap.bmHeight % 2 != 0 || bitmap.bmPlanes != 1 || bitmap.bmBitsPixel != 1)
        {
            return std::nullopt;
        }

        const auto stride = static_cast<size_t>(bitmap.bmWidthBytes);
        const auto fullHeight = static_cast<size_t>(bitmap.bmHeight);
        if (fullHeight > static_cast<size_t>((std::numeric_limits<LONG>::max)()) / stride)
        {
            return std::nullopt;
        }

        const auto size = static_cast<LONG>(stride * fullHeight);
        std::vector<BYTE> bits(size);
        if (GetBitmapBits(mask.get(), size, bits.data()) != size)
        {
            return std::nullopt;
        }

        const auto height = fullHeight / 2;
        for (size_t y = 0; y < height; ++y)
        {
            for (size_t x = 0; x < static_cast<size_t>(bitmap.bmWidth); ++x)
            {
                const auto bit = 0x80 >> (x % 8);
                const auto andByte = bits[y * stride + x / 8];
                const auto xorByte = bits[(y + height) * stride + x / 8];
                if (!(andByte & bit) || (xorByte & bit))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
