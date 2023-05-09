#include "pch.h"
#include "Settings.h"

#include <ModuleConstants.h>
#include <SettingsObserver.h>
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
    const static wchar_t* BlockInGameModeID = L"do-not-activate-on-game-mode";
    const static wchar_t* ExcludedAppsID = L"excluded-apps";
    const static wchar_t* FrameAccentColor = L"frame-accent-color";
    const static wchar_t* RoundCornersEnabledID = L"round-corners-enabled";
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
    m_uiSettings.ColorValuesChanged([&](winrt::Windows::UI::ViewManagement::UISettings const& settings,
                                        winrt::Windows::Foundation::IInspectable const& args)
    {
        if (m_settings.frameAccentColor)
        {
            NotifyObservers(SettingId::FrameAccentColor);
        }
    });
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

void AlwaysOnTopSettings::AddObserver(SettingsObserver& observer)
{
    m_observers.insert(&observer);
}

void AlwaysOnTopSettings::RemoveObserver(SettingsObserver& observer)
{
    auto iter = m_observers.find(&observer);
    if (iter != m_observers.end())
    {
        m_observers.erase(iter);
    }
}

void AlwaysOnTopSettings::LoadSettings()
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::load_from_settings_file(NonLocalizable::ModuleKey);

        if (const auto jsonVal = values.get_json(NonLocalizable::HotkeyID))
        {
            auto val = PowerToysSettings::HotkeyObject::from_json(*jsonVal);
            if (m_settings.hotkey.get_modifiers() != val.get_modifiers() || m_settings.hotkey.get_key() != val.get_key() || m_settings.hotkey.get_code() != val.get_code())
            {
                m_settings.hotkey = val;
                NotifyObservers(SettingId::Hotkey);
            }
        }
        
        if (const auto jsonVal = values.get_bool_value(NonLocalizable::SoundEnabledID))
        {
            auto val = *jsonVal;
            if (m_settings.enableSound != val)
            {
                m_settings.enableSound = val;
                NotifyObservers(SettingId::SoundEnabled);
            }
        }

        if (const auto jsonVal = values.get_int_value(NonLocalizable::FrameThicknessID))
        {
            auto val = *jsonVal;
            if (m_settings.frameThickness != val)
            {
                m_settings.frameThickness = val;
                NotifyObservers(SettingId::FrameThickness);
            }
        }

        if (const auto jsonVal = values.get_string_value(NonLocalizable::FrameColorID))
        {
            auto val = HexToRGB(*jsonVal);
            if (m_settings.frameColor != val)
            {
                m_settings.frameColor = val;
                NotifyObservers(SettingId::FrameColor);
            }
        }

        if (const auto jsonVal = values.get_bool_value(NonLocalizable::FrameEnabledID))
        {
            auto val = *jsonVal;
            if (m_settings.enableFrame != val)
            {
                m_settings.enableFrame = val;
                NotifyObservers(SettingId::FrameEnabled);
            }            
        }

        if (const auto jsonVal = values.get_bool_value(NonLocalizable::BlockInGameModeID))
        {
            auto val = *jsonVal;
            if (m_settings.blockInGameMode != val)
            {
                m_settings.blockInGameMode = val;
                NotifyObservers(SettingId::BlockInGameMode);
            }
        }

        if (const auto jsonVal = values.get_bool_value(NonLocalizable::RoundCornersEnabledID))
        {
            auto val = *jsonVal;
            if (m_settings.roundCornersEnabled != val)
            {
                m_settings.roundCornersEnabled = val;
                NotifyObservers(SettingId::RoundCornersEnabled);
            }
        }

        if (auto jsonVal = values.get_string_value(NonLocalizable::ExcludedAppsID))
        {
            std::wstring apps = std::move(*jsonVal);
            std::vector<std::wstring> excludedApps;
            auto excludedUppercase = apps;
            CharUpperBuffW(excludedUppercase.data(), static_cast<DWORD>(excludedUppercase.length()));
            std::wstring_view view(excludedUppercase);
            view = left_trim<wchar_t>(trim<wchar_t>(view));

            while (!view.empty())
            {
                auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
                excludedApps.emplace_back(view.substr(0, pos));
                view.remove_prefix(pos);
                view = left_trim<wchar_t>(trim<wchar_t>(view));
            }

            if (m_settings.excludedApps != excludedApps)
            {
                m_settings.excludedApps = excludedApps;
                NotifyObservers(SettingId::ExcludeApps);
            }
        }

        if (const auto jsonVal = values.get_bool_value(NonLocalizable::FrameAccentColor))
        {
            auto val = *jsonVal;
            if (m_settings.frameAccentColor != val)
            {
                m_settings.frameAccentColor = val;
                NotifyObservers(SettingId::FrameAccentColor);
            }
        }
    }
    catch (...)
    {
        // Log error message and continue with default settings.
        Logger::error("Failed to read settings");
        // TODO: show localized message
    }
}

void AlwaysOnTopSettings::NotifyObservers(SettingId id) const
{
    for (auto observer : m_observers)
    {
        if (observer->WantsToBeNotified(id))
        {
            observer->SettingsUpdate(id);
        }
    }
}
