#pragma once
#include <windows.h>
#include <string>

// Validates that lat/lon strings represent non-sentinel, in-range coordinates.
// Rejects (0,0) as a sentinel for "not configured".
inline bool CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon)
{
    try
    {
        double latVal = std::stod(lat);
        double lonVal = std::stod(lon);
        return !(latVal == 0 && lonVal == 0) &&
               (latVal >= -90.0 && latVal <= 90.0) &&
               (lonVal >= -180.0 && lonVal <= 180.0);
    }
    catch (...)
    {
        return false;
    }
}

constexpr bool ShouldBeLight(int nowMinutes, int lightTime, int darkTime)
{
    // Normalize values into [0, 1439]
    int normalizedLightTime = (lightTime % 1440 + 1440) % 1440;
    int normalizedDarkTime = (darkTime % 1440 + 1440) % 1440;
    int normalizedNowMinutes = (nowMinutes % 1440 + 1440) % 1440;

    // Case 1: Normal range, e.g. light mode comes before dark mode in the same day
    if (normalizedLightTime < normalizedDarkTime)
        return normalizedNowMinutes >= normalizedLightTime && normalizedNowMinutes < normalizedDarkTime;

    // Case 2: Wrap-around range, e.g. light mode starts in the evening and dark mode starts in the morning
    return normalizedNowMinutes >= normalizedLightTime || normalizedNowMinutes < normalizedDarkTime;
}

inline int GetNowMinutes()
{
    SYSTEMTIME st;
    GetLocalTime(&st);
    return st.wHour * 60 + st.wMinute;
}
