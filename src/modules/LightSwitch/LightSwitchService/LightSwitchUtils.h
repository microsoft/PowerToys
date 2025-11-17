#pragma once
#include <windows.h>

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
