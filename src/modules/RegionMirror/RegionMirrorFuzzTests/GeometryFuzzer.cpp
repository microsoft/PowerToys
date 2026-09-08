// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "../RegionMirror/Geometry.h"

#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <string>

namespace
{
    void Require(bool condition)
    {
        // assert() is disabled in Release, the configuration used by the fuzz target.
        if (!condition)
        {
            std::abort();
        }
    }

    bool RectanglesEqual(const RECT& first, const RECT& second)
    {
        return first.left == second.left && first.top == second.top &&
               first.right == second.right && first.bottom == second.bottom;
    }

    class ByteReader
    {
    public:
        ByteReader(const std::uint8_t* data, std::size_t size) :
            m_data(data),
            m_size(size)
        {
        }

        std::uint32_t ReadUnsigned()
        {
            std::uint32_t result = 0;
            for (unsigned int i = 0; i < sizeof(result); ++i)
            {
                if (m_position < m_size)
                {
                    result |= static_cast<std::uint32_t>(m_data[m_position++]) << (8U * i);
                }
            }

            return result;
        }

        LONG ReadCoordinate()
        {
            return static_cast<LONG>(std::bit_cast<std::int32_t>(ReadUnsigned()));
        }

    private:
        const std::uint8_t* m_data;
        std::size_t m_size;
        std::size_t m_position = 0;
    };

    RECT MakeValidRegion(ByteReader& reader)
    {
        const auto width = static_cast<std::int64_t>(reader.ReadUnsigned() % static_cast<std::uint32_t>(RegionMirror::MaximumRegionDimension)) + 1;
        const auto height = static_cast<std::int64_t>(reader.ReadUnsigned() % static_cast<std::uint32_t>(RegionMirror::MaximumRegionDimension)) + 1;
        const auto left = (std::min)(static_cast<std::int64_t>(reader.ReadCoordinate()), (std::numeric_limits<LONG>::max)() - width);
        const auto top = (std::min)(static_cast<std::int64_t>(reader.ReadCoordinate()), (std::numeric_limits<LONG>::max)() - height);
        return { static_cast<LONG>(left), static_cast<LONG>(top), static_cast<LONG>(left + width), static_cast<LONG>(top + height) };
    }

    RECT MakeOverlappingRegion(const RECT& region, ByteReader& reader)
    {
        const auto width = static_cast<std::int64_t>(region.right) - region.left;
        const auto height = static_cast<std::int64_t>(region.bottom) - region.top;
        const auto shiftX = static_cast<std::int64_t>(reader.ReadUnsigned() % static_cast<std::uint32_t>(2 * width - 1)) - (width - 1);
        const auto shiftY = static_cast<std::int64_t>(reader.ReadUnsigned() % static_cast<std::uint32_t>(2 * height - 1)) - (height - 1);
        const auto minimum = static_cast<std::int64_t>((std::numeric_limits<LONG>::min)());
        const auto maximum = static_cast<std::int64_t>((std::numeric_limits<LONG>::max)());
        const auto left = std::clamp(static_cast<std::int64_t>(region.left) + shiftX, minimum, maximum - width);
        const auto top = std::clamp(static_cast<std::int64_t>(region.top) + shiftY, minimum, maximum - height);
        return { static_cast<LONG>(left), static_cast<LONG>(top), static_cast<LONG>(left + width), static_cast<LONG>(top + height) };
    }

    std::wstring SerializeRegion(const RECT& region)
    {
        return std::to_wstring(region.left) + L"," + std::to_wstring(region.top) + L"," +
               std::to_wstring(static_cast<std::int64_t>(region.right) - region.left) + L"," +
               std::to_wstring(static_cast<std::int64_t>(region.bottom) - region.top);
    }

    void VerifyParser(std::wstring_view text)
    {
        const RECT sentinel{ -17, -19, 123, 321 };
        RECT parsed = sentinel;
        if (!RegionMirror::TryParseRegion(text, parsed))
        {
            Require(RectanglesEqual(sentinel, parsed));
            return;
        }

        Require(RegionMirror::IsValidRegion(parsed));
        Require(RegionMirror::IsContained(parsed, parsed));
        Require(RectanglesEqual(parsed, RegionMirror::FitAspect(parsed, parsed)));

        RECT roundTrip{};
        Require(RegionMirror::TryParseRegion(SerializeRegion(parsed), roundTrip));
        Require(RectanglesEqual(parsed, roundTrip));

        const auto withExtraComponent = std::wstring(text) + L",1";
        roundTrip = sentinel;
        Require(!RegionMirror::TryParseRegion(withExtraComponent, roundTrip));
        Require(RectanglesEqual(sentinel, roundTrip));
    }

    void VerifyFit(const RECT& content, const RECT& target)
    {
        const auto fitted = RegionMirror::FitAspect(content, target);
        if (!RegionMirror::IsValidRegion(content) || !RegionMirror::IsValidRegion(target))
        {
            Require(RectanglesEqual({}, fitted));
            return;
        }

        const auto contentWidth = static_cast<std::int64_t>(content.right) - content.left;
        const auto contentHeight = static_cast<std::int64_t>(content.bottom) - content.top;
        const auto targetWidth = static_cast<std::int64_t>(target.right) - target.left;
        const auto targetHeight = static_cast<std::int64_t>(target.bottom) - target.top;
        if (RectanglesEqual({}, fitted))
        {
            Require(targetWidth * contentHeight < contentWidth || targetHeight * contentWidth < contentHeight);
            return;
        }

        Require(RegionMirror::IsValidRegion(fitted));
        Require(RegionMirror::IsContained(fitted, target));
        const auto width = static_cast<std::int64_t>(fitted.right) - fitted.left;
        const auto height = static_cast<std::int64_t>(fitted.bottom) - fitted.top;
        Require(width == targetWidth || height == targetHeight);

        const auto leftPadding = static_cast<std::int64_t>(fitted.left) - target.left;
        const auto rightPadding = static_cast<std::int64_t>(target.right) - fitted.right;
        const auto topPadding = static_cast<std::int64_t>(fitted.top) - target.top;
        const auto bottomPadding = static_cast<std::int64_t>(target.bottom) - fitted.bottom;
        Require(rightPadding == leftPadding || rightPadding == leftPadding + 1);
        Require(bottomPadding == topPadding || bottomPadding == topPadding + 1);

        const auto aspectError = width * contentHeight - height * contentWidth;
        const auto absoluteError = aspectError < 0 ? -aspectError : aspectError;
        Require(absoluteError < (std::max)(contentWidth, contentHeight));

        const RECT sameSizeAtOrigin{ 0, 0, static_cast<LONG>(contentWidth), static_cast<LONG>(contentHeight) };
        Require(RectanglesEqual(fitted, RegionMirror::FitAspect(sameSizeAtOrigin, target)));
    }

    void VerifyCaptureTile(const RECT& region, const RECT& monitor)
    {
        const auto tile = RegionMirror::MakeCaptureTile(region, monitor);
        if (!RegionMirror::IsValidRegion(region) || !RegionMirror::IsValidRegion(monitor))
        {
            Require(!tile.has_value());
            return;
        }
        if (region.right <= monitor.left || region.left >= monitor.right ||
            region.bottom <= monitor.top || region.top >= monitor.bottom)
        {
            Require(!tile.has_value());
            return;
        }

        Require(tile.has_value());
        const auto monitorWidth = static_cast<std::int64_t>(monitor.right) - monitor.left;
        const auto monitorHeight = static_cast<std::int64_t>(monitor.bottom) - monitor.top;
        const RECT texture{ 0, 0, static_cast<LONG>(monitorWidth), static_cast<LONG>(monitorHeight) };
        Require(RegionMirror::IsContained(tile->source, texture));

        const auto regionWidth = static_cast<std::int64_t>(region.right) - region.left;
        const auto regionHeight = static_cast<std::int64_t>(region.bottom) - region.top;
        const auto tileWidth = static_cast<std::int64_t>(tile->source.right) - tile->source.left;
        const auto tileHeight = static_cast<std::int64_t>(tile->source.bottom) - tile->source.top;
        Require(tile->destination.x >= 0 && tile->destination.y >= 0);
        Require(static_cast<std::int64_t>(tile->destination.x) + tileWidth <= regionWidth);
        Require(static_cast<std::int64_t>(tile->destination.y) + tileHeight <= regionHeight);

        // Both coordinate systems must describe exactly the same physical pixels, including all
        // four edges of the intersection. Padding/gaps must not move or stretch a monitor's tile.
        const auto physicalLeft = static_cast<std::int64_t>(monitor.left) + tile->source.left;
        const auto physicalTop = static_cast<std::int64_t>(monitor.top) + tile->source.top;
        const auto physicalRight = static_cast<std::int64_t>(monitor.left) + tile->source.right;
        const auto physicalBottom = static_cast<std::int64_t>(monitor.top) + tile->source.bottom;
        Require(physicalLeft == static_cast<std::int64_t>(region.left) + tile->destination.x);
        Require(physicalTop == static_cast<std::int64_t>(region.top) + tile->destination.y);
        Require(physicalRight == static_cast<std::int64_t>(region.left) + tile->destination.x + tileWidth);
        Require(physicalBottom == static_cast<std::int64_t>(region.top) + tile->destination.y + tileHeight);
        Require(physicalLeft == (std::max)(region.left, monitor.left));
        Require(physicalTop == (std::max)(region.top, monitor.top));
        Require(physicalRight == (std::min)(region.right, monitor.right));
        Require(physicalBottom == (std::min)(region.bottom, monitor.bottom));
    }
}

extern "C" int LLVMFuzzerTestOneInput(const std::uint8_t* data, std::size_t size)
{
    if (size > 4096)
    {
        return 0;
    }

    // Exercise ASCII CLI strings and arbitrary UTF-16 code units without unaligned pointer casts.
    std::wstring ascii;
    ascii.reserve(size);
    for (std::size_t i = 0; i < size; ++i)
    {
        ascii.push_back(static_cast<wchar_t>(data[i]));
    }

    VerifyParser(ascii);

    std::wstring utf16;
    utf16.reserve(size / 2);
    for (std::size_t i = 0; i + 1 < size; i += 2)
    {
        const auto codeUnit = static_cast<std::uint16_t>(data[i]) | (static_cast<std::uint16_t>(data[i + 1]) << 8U);
        utf16.push_back(static_cast<wchar_t>(codeUnit));
    }

    VerifyParser(utf16);

    ByteReader reader(data, size);
    const POINT first{ reader.ReadCoordinate(), reader.ReadCoordinate() };
    const POINT second{ reader.ReadCoordinate(), reader.ReadCoordinate() };
    const auto normalized = RegionMirror::NormalizeSelection(first, second);
    Require(normalized.left <= normalized.right && normalized.top <= normalized.bottom);
    Require(RectanglesEqual(normalized, RegionMirror::NormalizeSelection(second, first)));
    Require(RegionMirror::IsContained(normalized, normalized) == RegionMirror::IsValidRegion(normalized));

    const RECT rawTarget{ reader.ReadCoordinate(), reader.ReadCoordinate(), reader.ReadCoordinate(), reader.ReadCoordinate() };
    VerifyFit(normalized, rawTarget);
    VerifyCaptureTile(normalized, rawTarget);

    // Random coordinates alone almost never form valid small regions. Generate bounded dimensions
    // as well so every input also exercises successful parsing and aspect fitting at arbitrary origins.
    const auto content = MakeValidRegion(reader);
    const auto target = MakeValidRegion(reader);
    VerifyParser(SerializeRegion(content));
    VerifyFit(content, target);
    VerifyCaptureTile(content, target);
    VerifyCaptureTile(content, content);
    const auto overlapping = MakeOverlappingRegion(content, reader);
    VerifyCaptureTile(content, overlapping);
    VerifyCaptureTile(overlapping, content);
    return 0;
}
