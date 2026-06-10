// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>

// Shared lightweight types for LightSwitch — no heavy dependencies.
// Consumed by both the service (LightSwitchSettings.h) and unit tests.

enum class ScheduleMode
{
    Off,
    FixedHours,
    SunsetToSunrise,
    FollowNightLight,
};

inline std::wstring ToString(ScheduleMode mode)
{
    switch (mode)
    {
    case ScheduleMode::FixedHours:
        return L"FixedHours";
    case ScheduleMode::SunsetToSunrise:
        return L"SunsetToSunrise";
    case ScheduleMode::FollowNightLight:
        return L"FollowNightLight";
    default:
        return L"Off";
    }
}

inline ScheduleMode FromString(const std::wstring& str)
{
    if (str == L"SunsetToSunrise")
        return ScheduleMode::SunsetToSunrise;
    if (str == L"FixedHours")
        return ScheduleMode::FixedHours;
    if (str == L"FollowNightLight")
        return ScheduleMode::FollowNightLight;
    else
        return ScheduleMode::Off;
}

struct LightSwitchConfig
{
    ScheduleMode scheduleMode = ScheduleMode::FixedHours;

    std::wstring latitude = L"0.0";
    std::wstring longitude = L"0.0";

    // Stored as minutes since midnight
    int lightTime = 8 * 60; // 08:00 default
    int darkTime = 20 * 60; // 20:00 default

    int sunrise_offset = 0;
    int sunset_offset = 0;

    bool changeSystem = false;
    bool changeApps = false;

    bool enableDarkModeProfile = false;
    bool enableLightModeProfile = false;
    std::wstring darkModeProfile = L"";
    std::wstring lightModeProfile = L"";
};
