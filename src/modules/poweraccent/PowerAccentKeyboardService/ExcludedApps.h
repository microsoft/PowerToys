// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>

#include <algorithm>
#include <string>
#include <string_view>
#include <vector>

#include <common/utils/string_utils.h>

namespace winrt::PowerToys::PowerAccentKeyboardService::implementation
{
    inline std::vector<std::wstring> ParseExcludedApps(std::wstring_view excludedAppsView)
    {
        std::vector<std::wstring> excludedApps;
        auto excludedUppercase = std::wstring(excludedAppsView);
        CharUpperBuffW(excludedUppercase.data(), static_cast<DWORD>(excludedUppercase.length()));
        std::wstring_view view(excludedUppercase);
        view = left_trim<wchar_t>(trim<wchar_t>(view));

        while (!view.empty())
        {
            const auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
            const auto app = trim<wchar_t>(view.substr(0, pos));
            if (!app.empty())
            {
                excludedApps.emplace_back(app);
            }

            view.remove_prefix(pos);
            view = left_trim<wchar_t>(trim<wchar_t>(view));
        }

        return excludedApps;
    }
}
