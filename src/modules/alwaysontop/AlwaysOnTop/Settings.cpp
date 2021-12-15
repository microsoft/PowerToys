#include "pch.h"
#include "Settings.h"

#include <ModuleConstants.h>
#include <WinHookEventIDs.h>

#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    const static wchar_t* SettingsFileName = L"settings.json";

    const static wchar_t* HotkeyID = L"hotkey";
    const static wchar_t* SoundEnabledID = L"sound-enabled";
    const static wchar_t* FrameEnabledID = L"frame-enabled";
    const static wchar_t* BlockInGameModeID = L"block-in-game-mode";
}

AlwaysOnTopSettings::AlwaysOnTopSettings()
{
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
        
        if (const auto val = values.get_bool_value(NonLocalizable::BlockInGameModeID))
        {
            m_settings.blockInGameMode = *val;
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
