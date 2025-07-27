#include "pch.h"
#include "excluded_app.h"
#include <string_utils.h>

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

        while (!view.empty())
        {
            auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
            m_excludedApps.emplace_back(view.substr(0, pos));
            view.remove_prefix(pos);
            view = left_trim<wchar_t>(trim<wchar_t>(view));
        }

        if (m_excludedApps.empty())
        {
            return false;
        }

        if (HWND foregroundApp{ GetForegroundWindow() })
        {
            auto processPath = get_process_path(foregroundApp);
            CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

            return check_excluded_app(foregroundApp, processPath, m_excludedApps);
        }
        return false;
    }
}
