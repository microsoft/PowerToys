#include "LightSwitchSettings.h"
#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>
#include "SettingsObserver.h"

#include <filesystem>
#include <fstream>
#include <WinHookEventIDs.h>
#include <logger.h>

using namespace std;

LightSwitchSettings& LightSwitchSettings::instance()
{
    static LightSwitchSettings inst;
    return inst;
}

LightSwitchSettings::LightSwitchSettings()
{
    LoadSettings();
}

std::wstring LightSwitchSettings::GetSettingsFileName()
{
    return PTSettingsHelper::get_module_save_file_location(L"LightSwitch");
}

void LightSwitchSettings::InitFileWatcher()
{
    if (!m_settingsChangedEvent)
    {
        m_settingsChangedEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    }

    if (!m_settingsFileWatcher)
    {
        m_settingsFileWatcher = std::make_unique<FileWatcher>(
            GetSettingsFileName(),
            [this]() {
                Logger::info(L"[LightSwitchSettings] Settings file changed, signaling event.");
                SetEvent(m_settingsChangedEvent);
            });
    }
}

void LightSwitchSettings::AddObserver(SettingsObserver& observer)
{
    m_observers.insert(&observer);
}

void LightSwitchSettings::RemoveObserver(SettingsObserver& observer)
{
    m_observers.erase(&observer);
}

void LightSwitchSettings::NotifyObservers(SettingId id) const
{
    for (auto observer : m_observers)
    {
        if (observer->WantsToBeNotified(id))
        {
            observer->SettingsUpdate(id);
        }
    }
}

void LightSwitchSettings::LoadSettings()
{
    try
    {
        PowerToysSettings::PowerToyValues values =
            PowerToysSettings::PowerToyValues::load_from_settings_file(L"LightSwitch");

        if (const auto jsonVal = values.get_string_value(L"scheduleMode"))
        {
            auto val = *jsonVal;
            auto newMode = FromString(val);
            if (m_settings.scheduleMode != newMode)
            {
                m_settings.scheduleMode = newMode;
                NotifyObservers(SettingId::ScheduleMode);
            }
        }

        // Latitude
        if (const auto jsonVal = values.get_string_value(L"latitude"))
        {
            auto val = *jsonVal;
            if (m_settings.latitude != val)
            {
                m_settings.latitude = val;
                NotifyObservers(SettingId::Latitude);
            }
        }

        // Longitude
        if (const auto jsonVal = values.get_string_value(L"longitude"))
        {
            auto val = *jsonVal;
            if (m_settings.longitude != val)
            {
                m_settings.longitude = val;
                NotifyObservers(SettingId::Longitude);
            }
        }

        // LightTime
        if (const auto jsonVal = values.get_int_value(L"lightTime"))
        {
            auto val = *jsonVal;
            if (m_settings.lightTime != val)
            {
                m_settings.lightTime = val;
                NotifyObservers(SettingId::LightTime);
            }
        }

        // DarkTime
        if (const auto jsonVal = values.get_int_value(L"darkTime"))
        {
            auto val = *jsonVal;
            if (m_settings.darkTime != val)
            {
                m_settings.darkTime = val;
                NotifyObservers(SettingId::DarkTime);
            }
        }

        // Offset
        if (const auto jsonVal = values.get_int_value(L"sunrise_offset"))
        {
            auto val = *jsonVal;
            if (m_settings.sunrise_offset != val)
            {
                m_settings.sunrise_offset = val;
                NotifyObservers(SettingId::Sunrise_Offset);
            }
        }

        if (const auto jsonVal = values.get_int_value(L"sunset_offset"))
        {
            auto val = *jsonVal;
            if (m_settings.sunset_offset != val)
            {
                m_settings.sunset_offset = val;
                NotifyObservers(SettingId::Sunset_Offset);
            }
        }

        // ChangeSystem
        if (const auto jsonVal = values.get_bool_value(L"changeSystem"))
        {
            auto val = *jsonVal;
            if (m_settings.changeSystem != val)
            {
                m_settings.changeSystem = val;
                NotifyObservers(SettingId::ChangeSystem);
            }
        }

        // ChangeApps
        if (const auto jsonVal = values.get_bool_value(L"changeApps"))
        {
            auto val = *jsonVal;
            if (m_settings.changeApps != val)
            {
                m_settings.changeApps = val;
                NotifyObservers(SettingId::ChangeApps);
            }
        }

        if (const auto jsonVal = values.get_bool_value(L"wallpaperEnabled"))
        {
            auto val = *jsonVal;
            if (m_settings.wallpaperEnabled != val)
            {
                m_settings.wallpaperEnabled = val;
                NotifyObservers(SettingId::WallpaperEnabled);
            }
        }
        
        if (const auto jsonVal = values.get_bool_value(L"wallpaperVirtualDesktopEnabled"))
        {
            auto val = *jsonVal;
            if (m_settings.wallpaperVirtualDesktop != val)
            {
                m_settings.wallpaperVirtualDesktop = val;
                NotifyObservers(SettingId::WallpaperVirtualDesktopEnabled);
            }
        }

        if (const auto jsonVal = values.get_int_value(L"wallpaperStyleLight"))
        {
            auto val = *jsonVal;
            if (m_settings.wallpaperStyleLight != val)
            {
                m_settings.wallpaperStyleLight = val;
                NotifyObservers(SettingId::WallpaperStyleLight);
            }
        }

        if (const auto jsonVal = values.get_int_value(L"wallpaperStyleDark"))
        {
            auto val = *jsonVal;
            if (m_settings.wallpaperStyleDark != val)
            {
                m_settings.wallpaperStyleDark = val;
                NotifyObservers(SettingId::WallpaperStyleDark);
            }
        }

        if (const auto jsonVal = values.get_string_value(L"wallpaperPathLight"))
        {
            auto val = *jsonVal;
            if (m_settings.wallpaperPathLight != val)
            {
                m_settings.wallpaperPathLight = val;
                NotifyObservers(SettingId::WallpaperPathLight);
            }
        }

        if (const auto jsonVal = values.get_string_value(L"wallpaperPathDark"))
        {
            auto val = *jsonVal;
            if (m_settings.wallpaperPathDark != val)
            {
                m_settings.wallpaperPathDark = val;
                NotifyObservers(SettingId::WallpaperPathDark);
            }
        }
    }
    catch (...)
    {
        // Keeps defaults if load fails
    }
}