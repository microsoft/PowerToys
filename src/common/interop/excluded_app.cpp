#include "pch.h"
#include "excluded_app.h"
#include <common/utils/excluded_apps.h>
#include <../utils/string_utils.h>

namespace
{
    // This shared interop file exposes Shortcut Guide's exclusion check.
    // Atlantis Word Processor can hard-lock input when Shortcut Guide is invoked over it.
    constexpr int maxTitleLength = 255;

    const std::vector<std::wstring> builtInExcludedProcessNames(
        shortcut_guide::interop::built_in_excluded_process_names.begin(),
        shortcut_guide::interop::built_in_excluded_process_names.end());

    bool isShortcutGuideBuiltInProcessExcluded(const std::wstring& processPath)
    {
        return find_app_name_in_path(processPath, builtInExcludedProcessNames);
    }

    bool isShortcutGuideBuiltInWindowTitleExcluded(HWND hwnd)
    {
        WCHAR title[maxTitleLength + 1]{};
        int len = GetWindowTextW(hwnd, title, maxTitleLength + 1);
        if (len <= 0)
        {
            return false;
        }

        title[len] = L'\0';
        CharUpperBuffW(title, static_cast<DWORD>(len));
        std::wstring titleUpper(title, len);

        for (const auto& excludedTitle : shortcut_guide::interop::built_in_excluded_window_titles)
        {
            if (titleUpper.contains(excludedTitle))
            {
                return true;
            }
        }

        return false;
    }
}

extern "C"
{
    __declspec(dllexport) bool IsCurrentWindowExcludedFromShortcutGuide()
    {
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(L"Shortcut Guide");
        auto settingsObject = settings.get_raw_json();
        std::wstring apps = settingsObject.GetNamedObject(L"properties").GetNamedObject(L"disabled_apps").GetNamedString(L"value").c_str();
        auto excludedUppercase = apps;
        CharUpperBuffW(excludedUppercase.data(), static_cast<DWORD>(excludedUppercase.length()));
        std::wstring_view view(excludedUppercase);
        view = left_trim<wchar_t>(trim<wchar_t>(view));

        std::vector<std::wstring> excludedApps;

        while (!view.empty())
        {
            auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
            excludedApps.emplace_back(view.substr(0, pos));
            view.remove_prefix(pos);
            view = left_trim<wchar_t>(trim<wchar_t>(view));
        }

        if (HWND foregroundApp{ GetForegroundWindow() })
        {
            auto processPath = get_process_path(foregroundApp);
            CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

            if (check_excluded_app(foregroundApp, processPath, excludedApps))
            {
                return true;
            }

            if (isShortcutGuideBuiltInProcessExcluded(processPath))
            {
                return true;
            }

            return isShortcutGuideBuiltInWindowTitleExcluded(foregroundApp);
        }
        return false;
    }
}
