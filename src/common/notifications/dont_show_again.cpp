#include "pch.h"
#include "dont_show_again.h"

namespace notifications
{
    bool disable_toast(const wchar_t* registry_path)
    {
        HKEY key{};
        if (RegCreateKeyExW(HKEY_CURRENT_USER,
                            registry_path,
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
        if (RegSetValueExW(key, nullptr, 0, REG_QWORD, reinterpret_cast<const BYTE*>(&now), buf_size) != ERROR_SUCCESS)
        {
            RegCloseKey(key);
            return false;
        }
        RegCloseKey(key);
        return true;
    }

    bool is_toast_disabled(const wchar_t* registry_path, const int64_t disable_interval_in_days)
    {
        HKEY key{};
        if (RegOpenKeyExW(HKEY_CURRENT_USER,
                          registry_path,
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
    }
}