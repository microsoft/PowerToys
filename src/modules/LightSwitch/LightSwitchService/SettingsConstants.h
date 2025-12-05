#pragma once

enum class SettingId
{
    ScheduleMode = 0,
    Latitude,
    Longitude,
    LightTime,
    DarkTime,
    Sunrise_Offset,
    Sunset_Offset,
    ChangeSystem,
    ChangeApps
};

constexpr wchar_t PERSONALIZATION_REGISTRY_PATH[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
constexpr wchar_t NIGHT_LIGHT_REGISTRY_PATH[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore\\Store\\DefaultAccount\\Current\\default$windows.data.bluelightreduction.bluelightreductionstate\\windows.data.bluelightreduction.bluelightreductionstate";
