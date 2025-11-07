#pragma once
#include <windows.h>

constexpr bool ShouldBeLight(int nowMinutes, int lightTime, int darkTime)
{
    // Normalize values into [0, 1439]
    lightTime = (lightTime % 1440 + 1440) % 1440;
    darkTime = (darkTime % 1440 + 1440) % 1440;
    nowMinutes = (nowMinutes % 1440 + 1440) % 1440;

    // Case 1: Normal range, e.g. light mode comes before dark mode in the same day
    if (lightTime < darkTime)
        return nowMinutes >= lightTime && nowMinutes < darkTime;

    // Case 2: Wrap-around range, e.g. light mode starts in the evening and dark mode starts in the morning
    return nowMinutes >= lightTime || nowMinutes < darkTime;
}


inline int GetNowMinutes()
{
    SYSTEMTIME st;
    GetLocalTime(&st);
    return st.wHour * 60 + st.wMinute;
}
