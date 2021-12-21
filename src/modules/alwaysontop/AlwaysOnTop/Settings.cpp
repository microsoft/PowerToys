#include "pch.h"
#include "Settings.h"

#include <ModuleConstants.h>
#include <WinHookEventIDs.h>

#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/string_utils.h> // trim 

namespace NonLocalizable
{
    const static wchar_t* SettingsFileName = L"settings.json";

    const static wchar_t* HotkeyID = L"hotkey";
    const static wchar_t* SoundEnabledID = L"sound-enabled";
    const static wchar_t* FrameEnabledID = L"frame-enabled";
    const static wchar_t* FrameThicknessID = L"frame-thickness";
    const static wchar_t* FrameColorID = L"frame-color";
    const static wchar_t* BlockInGameModeID = L"block-in-game-mode";
    const static wchar_t* ExcludedAppsID = L"excluded-apps";
}

// TODO: move to common utils
inline COLORREF HexToRGB(std::wstring_view hex, const COLORREF fallbackColor = RGB(255, 255, 255))
{
    hex = left_trim<wchar_t>(trim<wchar_t>(hex), L"#");

    try
    {
        const long long tmp = std::stoll(hex.data(), nullptr, 16);
        const BYTE nR = static_cast<BYTE>((tmp & 0xFF0000) >> 16);
        const BYTE nG = static_cast<BYTE>((tmp & 0xFF00) >> 8);
        const BYTE nB = static_cast<BYTE>((tmp & 0xFF));
        return RGB(nR, nG, nB);
    }
    catch (const std::exception&)
    {
        return fallbackColor;
    }
}

AlwaysOnTopSettings::AlwaysOnTopSettings()
{
}

AlwaysOnTopSettings& AlwaysOnTopSettings::instance()
{
    static AlwaysOnTopSettings instance;
    return instance;
}

void AlwaysOnTopSettings::InitFileWatcher()
{
    const std::wstring& settingsFileName = GetSettingsFileName();
    m_settingsFileWatcher = std::make_unique<FileWatcher>(settingsFileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_SETTINGS_CHANGED, NULL, NULL);
    });
}

std::wstring AlwaysOnTopSettings::GetSettingsFileName()
{
    std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
    return saveFolderPath + L"\\" + std::wstring(NonLocalizable::SettingsFileName);
}

void AlwaysOnTopSettings::AddObserver(const NotificationCallback& callback)
{
    m_observerCallbacks.push_back(callback);
}

void AlwaysOnTopSettings::LoadSettings()
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::load_from_settings_file(NonLocalizable::ModuleKey);

        if (const auto val = values.get_json(NonLocalizable::HotkeyID))
        {
            m_settings.hotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }

        if (const auto val = values.get_bool_value(NonLocalizable::SoundEnabledID))
        {
            m_settings.enableSound = *val;
        }

        if (const auto val = values.get_bool_value(NonLocalizable::FrameEnabledID))
        {
            m_settings.enableFrame = *val;
        }

        if (const auto val = values.get_int_value(NonLocalizable::FrameThicknessID))
        {
            m_settings.frameThickness = static_cast<float>(*val);
        }
        
        if (const auto val = values.get_string_value(NonLocalizable::FrameColorID))
        {
            m_settings.frameColor = HexToRGB(*val);
        }

        if (const auto val = values.get_bool_value(NonLocalizable::BlockInGameModeID))
        {
            m_settings.blockInGameMode = *val;
        }

        if (auto val = values.get_string_value(NonLocalizable::ExcludedAppsID))
        {
            std::wstring apps = std::move(*val);
            m_settings.excludedApps.clear();
            auto excludedUppercase = apps;
            CharUpperBuffW(excludedUppercase.data(), (DWORD)excludedUppercase.length());
            std::wstring_view view(excludedUppercase);
            while (view.starts_with('\n') || view.starts_with('\r'))
            {
                view.remove_prefix(1);
            }
            while (!view.empty())
            {
                auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
                m_settings.excludedApps.emplace_back(view.substr(0, pos));
                view.remove_prefix(pos);
                while (view.starts_with('\n') || view.starts_with('\r'))
                {
                    view.remove_prefix(1);
                }
            }
        }

        NotifyObservers();
    }
    catch (...)
    {
        // Log error message and continue with default settings.
        Logger::error("Failed to read settings");
        // TODO: show localized message
    }
}

void AlwaysOnTopSettings::NotifyObservers() const
{
    for (const auto& callback : m_observerCallbacks)
    {
        callback();
    }
}
