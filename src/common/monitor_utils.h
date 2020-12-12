#pragma once

#include "gdiplus.h"

template<RECT MONITORINFO::*member>
std::vector<std::pair<HMONITOR, RECT>> GetAllMonitorRects()
{
    using result_t = std::vector<std::pair<HMONITOR, RECT>>;
    result_t result;

    auto enumMonitors = [](HMONITOR monitor, HDC hdc, LPRECT pRect, LPARAM param) -> BOOL {
        MONITORINFOEX mi;
        mi.cbSize = sizeof(mi);
        result_t& result = *reinterpret_cast<result_t*>(param);
        if (GetMonitorInfo(monitor, &mi))
        {
            result.push_back({ monitor, mi.*member });
        }

        return TRUE;
    };

    EnumDisplayMonitors(NULL, NULL, enumMonitors, reinterpret_cast<LPARAM>(&result));
    return result;
}

template<RECT MONITORINFO::*member>
std::vector<std::pair<HMONITOR, MONITORINFOEX>> GetAllMonitorInfo()
{
    using result_t = std::vector<std::pair<HMONITOR, MONITORINFOEX>>;
    result_t result;

    auto enumMonitors = [](HMONITOR monitor, HDC hdc, LPRECT pRect, LPARAM param) -> BOOL {
        MONITORINFOEX mi;
        mi.cbSize = sizeof(mi);
        result_t& result = *reinterpret_cast<result_t*>(param);
        if (GetMonitorInfo(monitor, &mi))
        {
            result.push_back({ monitor, mi });
        }

        return TRUE;
    };

    EnumDisplayMonitors(NULL, NULL, enumMonitors, reinterpret_cast<LPARAM>(&result));
    return result;
}