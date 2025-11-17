#pragma once

#include <Windows.h>
#include <string>
#include <vector>

namespace DisplayUtils
{
    struct DisplayData
    {
        HMONITOR monitor{};
        std::wstring id;
        std::wstring instanceId;
        unsigned int number{};
        unsigned int dpi{};
        RECT monitorRectDpiAware{};
        RECT monitorRectDpiUnaware{};
    };

    std::pair<bool, std::vector<DisplayData>> GetDisplays();
};
