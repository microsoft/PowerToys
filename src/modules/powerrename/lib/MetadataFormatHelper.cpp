// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MetadataFormatHelper.h"
#include <format>
#include <cmath>
#include <cstring>

using namespace PowerRenameLib;

// Formatting functions

std::wstring MetadataFormatHelper::FormatAperture(double aperture)
{
    return std::format(L"f/{:.1f}", aperture);
}

std::wstring MetadataFormatHelper::FormatShutterSpeed(double speed)
{
    if (speed <= 0.0)
    {
        return L"0";
    }

    if (speed >= 1.0)
    {
        return std::format(L"{:.1f}s", speed);
    }

    const double reciprocal = std::round(1.0 / speed);
    if (reciprocal <= 1.0)
    {
        return std::format(L"{:.3f}s", speed);
    }

    return std::format(L"1/{:.0f}s", reciprocal);
}

std::wstring MetadataFormatHelper::FormatISO(int64_t iso)
{
    if (iso <= 0)
    {
        return L"ISO";
    }

    return std::format(L"ISO {}", iso);
}

std::wstring MetadataFormatHelper::FormatFlash(int64_t flashValue)
{
    switch (flashValue & 0x1)
    {
    case 0:
        return L"Flash Off";
    case 1:
        return L"Flash On";
    default:
        break;
    }

    return std::format(L"Flash 0x{:X}", static_cast<unsigned int>(flashValue));
}

std::wstring MetadataFormatHelper::FormatCoordinate(double coord, bool isLatitude)
{
    wchar_t direction = isLatitude ? (coord >= 0.0 ? L'N' : L'S') : (coord >= 0.0 ? L'E' : L'W');
    double absolute = std::abs(coord);
    int degrees = static_cast<int>(absolute);
    double minutes = (absolute - static_cast<double>(degrees)) * 60.0;

    return std::format(L"{:d}°{:.2f}'{}", degrees, minutes, direction);
}

std::wstring MetadataFormatHelper::FormatSystemTime(const SYSTEMTIME& st)
{
    return std::format(L"{:04d}-{:02d}-{:02d} {:02d}:{:02d}:{:02d}",
        st.wYear,
        st.wMonth,
        st.wDay,
        st.wHour,
        st.wMinute,
        st.wSecond);
}

// Parsing functions

double MetadataFormatHelper::ParseGPSRational(const PROPVARIANT& pv)
{
    if ((pv.vt & VT_VECTOR) && pv.caub.cElems >= 8)
    {
        return ParseSingleRational(pv.caub.pElems, 0);
    }
    return 0.0;
}

double MetadataFormatHelper::ParseSingleRational(const uint8_t* bytes, size_t offset)
{
    // Parse a single rational number (8 bytes: numerator + denominator)
    if (!bytes)
        return 0.0;

    // Note: Callers are responsible for ensuring the buffer is large enough.
    // This function assumes offset points to at least 8 bytes of valid data.
    // All current callers perform cElems >= required_size checks before calling.
    const uint8_t* rationalBytes = bytes + offset;

    // Parse as little-endian uint32_t values
    uint32_t numerator = static_cast<uint32_t>(rationalBytes[0]) |
                         (static_cast<uint32_t>(rationalBytes[1]) << 8) |
                         (static_cast<uint32_t>(rationalBytes[2]) << 16) |
                         (static_cast<uint32_t>(rationalBytes[3]) << 24);

    uint32_t denominator = static_cast<uint32_t>(rationalBytes[4]) |
                           (static_cast<uint32_t>(rationalBytes[5]) << 8) |
                           (static_cast<uint32_t>(rationalBytes[6]) << 16) |
                           (static_cast<uint32_t>(rationalBytes[7]) << 24);

    if (denominator != 0)
    {
        return static_cast<double>(numerator) / static_cast<double>(denominator);
    }

    return 0.0;
}

double MetadataFormatHelper::ParseSingleSRational(const uint8_t* bytes, size_t offset)
{
    // Parse a single signed rational number (8 bytes: signed numerator + signed denominator)
    if (!bytes)
        return 0.0;

    // Note: Callers are responsible for ensuring the buffer is large enough.
    // This function assumes offset points to at least 8 bytes of valid data.
    // All current callers perform cElems >= required_size checks before calling.
    const uint8_t* rationalBytes = bytes + offset;

    // Parse as little-endian int32_t values (signed)
    // First construct as unsigned, then reinterpret as signed
    uint32_t numerator_uint = static_cast<uint32_t>(rationalBytes[0]) |
                              (static_cast<uint32_t>(rationalBytes[1]) << 8) |
                              (static_cast<uint32_t>(rationalBytes[2]) << 16) |
                              (static_cast<uint32_t>(rationalBytes[3]) << 24);

    uint32_t denominator_uint = static_cast<uint32_t>(rationalBytes[4]) |
                                (static_cast<uint32_t>(rationalBytes[5]) << 8) |
                                (static_cast<uint32_t>(rationalBytes[6]) << 16) |
                                (static_cast<uint32_t>(rationalBytes[7]) << 24);

    // Reinterpret as signed
    int32_t numerator = static_cast<int32_t>(numerator_uint);
    int32_t denominator = static_cast<int32_t>(denominator_uint);

    if (denominator != 0)
    {
        return static_cast<double>(numerator) / static_cast<double>(denominator);
    }

    return 0.0;
}

std::pair<double, double> MetadataFormatHelper::ParseGPSCoordinates(
    const PROPVARIANT& latitude,
    const PROPVARIANT& longitude,
    const PROPVARIANT& latRef,
    const PROPVARIANT& lonRef)
{
    double lat = 0.0, lon = 0.0;

    // Parse latitude - typically stored as 3 rationals (degrees, minutes, seconds)
    if ((latitude.vt & VT_VECTOR) && latitude.caub.cElems >= 24) // 3 rationals * 8 bytes each
    {
        const uint8_t* bytes = latitude.caub.pElems;

        // degrees, minutes, seconds (each rational is 8 bytes)
        double degrees = ParseSingleRational(bytes, 0);
        double minutes = ParseSingleRational(bytes, 8);
        double seconds = ParseSingleRational(bytes, 16);

        lat = degrees + minutes / 60.0 + seconds / 3600.0;
    }

    // Parse longitude
    if ((longitude.vt & VT_VECTOR) && longitude.caub.cElems >= 24)
    {
        const uint8_t* bytes = longitude.caub.pElems;

        double degrees = ParseSingleRational(bytes, 0);
        double minutes = ParseSingleRational(bytes, 8);
        double seconds = ParseSingleRational(bytes, 16);

        lon = degrees + minutes / 60.0 + seconds / 3600.0;
    }

    // Apply direction references (N/S for latitude, E/W for longitude)
    if (latRef.vt == VT_LPSTR && latRef.pszVal)
    {
        if (strcmp(latRef.pszVal, "S") == 0)
            lat = -lat;
    }

    if (lonRef.vt == VT_LPSTR && lonRef.pszVal)
    {
        if (strcmp(lonRef.pszVal, "W") == 0)
            lon = -lon;
    }

    return { lat, lon };
}

std::wstring MetadataFormatHelper::SanitizeForFileName(const std::wstring& str)
{
    // Windows illegal filename characters: < > : " / \ | ? *
    // Also control characters (0-31) and some others
    std::wstring sanitized = str;
    
    // Replace illegal characters with underscore
    for (auto& ch : sanitized)
    {
        // Check for illegal characters
        if (ch == L'<' || ch == L'>' || ch == L':' || ch == L'"' ||
            ch == L'/' || ch == L'\\' || ch == L'|' || ch == L'?' || ch == L'*' ||
            ch < 32)  // Control characters
        {
            ch = L'_';
        }
    }
    
    // Also remove trailing dots and spaces (Windows doesn't like them at end of filename)
    while (!sanitized.empty() && (sanitized.back() == L'.' || sanitized.back() == L' '))
    {
        sanitized.pop_back();
    }
    
    return sanitized;
}
