#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>
#include <limits>

#include "../timeutil.h"
namespace
{
    const inline wchar_t CANT_DRAG_ELEVATED_DONT_SHOW_AGAIN_REGISTRY_PATH[] = LR"(SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DontShowMeThisDialogAgain\{e16ea82f-6d94-4f30-bb02-d6d911588afd})";
    const inline int64_t disable_interval_in_days = 30;
}

inline bool disable_cant_drag_elevated_warning()
{
    HKEY key{};
    if (RegCreateKeyExW(HKEY_CURRENT_USER,
                        CANT_DRAG_ELEVATED_DONT_SHOW_AGAIN_REGISTRY_PATH,
                        0,
                        nullptr,
                        REG_OPTION_NON_VOLATILE,
                        KEY_ALL_ACCESS,
                        nullptr,
                        &key,
                        nullptr) != ERROR_SUCCESS)
    {
        return false;
    }
    const auto now = timeutil::now();
    const size_t buf_size = sizeof(now);
    if (RegSetValueExW(key, nullptr, 0, REG_QWORD, reinterpret_cast<const BYTE*>(&now), sizeof(now)) != ERROR_SUCCESS)
    {
        RegCloseKey(key);
        return false;
    }
    RegCloseKey(key);
    return true;
}

inline bool is_cant_drag_elevated_warning_disabled()
{
    HKEY key{};
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
                      CANT_DRAG_ELEVATED_DONT_SHOW_AGAIN_REGISTRY_PATH,
                      0,
                      KEY_READ,
                      &key) != ERROR_SUCCESS)
    {
        return false;
    }
    std::wstring buffer(std::numeric_limits<time_t>::digits10 + 2, L'\0');
    time_t last_disabled_time{};
    DWORD time_size = static_cast<DWORD>(sizeof(last_disabled_time));
    if (RegGetValueW(
            key,
            nullptr,
            nullptr,
            RRF_RT_REG_QWORD,
            nullptr,
            &last_disabled_time,
            &time_size) != ERROR_SUCCESS)
    {
        RegCloseKey(key);
        return false;
    }
    RegCloseKey(key);
    return timeutil::diff::in_days(timeutil::now(), last_disabled_time) < disable_interval_in_days;
    return false;
}