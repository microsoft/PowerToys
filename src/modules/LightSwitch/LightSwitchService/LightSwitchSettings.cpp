#include "LightSwitchSettings.h"
#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>
#include "SettingsObserver.h"
#include "ThemeHelper.h"
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
                using namespace std::chrono;

                {
                    std::lock_guard<std::mutex> lock(m_debounceMutex);
                    m_lastChangeTime = steady_clock::now();
                    if (m_debouncePending)
                        return;
                    m_debouncePending = true;
                }

                m_debounceThread = std::jthread([this](std::stop_token stop) {
                    using namespace std::chrono;
                    while (!stop.stop_requested())
                    {
                        std::this_thread::sleep_for(seconds(3));

                        auto elapsed = steady_clock::now() - m_lastChangeTime;
                        if (elapsed >= seconds(1))
                            break;
                    }

                    {
                        std::lock_guard<std::mutex> lock(m_debounceMutex);
                        m_debouncePending = false;
                    }

                    Logger::info(L"[LightSwitchSettings] Settings file stabilized, reloading.");

                    try
                    {
                        LoadSettings();
                        ApplyThemeIfNecessary();
                        SetEvent(m_settingsChangedEvent);
                    }
                    catch (const std::exception& e)
                    {
                        std::wstring wmsg;
                        wmsg.assign(e.what(), e.what() + strlen(e.what()));
                        Logger::error(L"[LightSwitchSettings] Exception during debounced reload: {}", wmsg);
                    }
                });
            });
    }
}

LightSwitchSettings::~LightSwitchSettings()
{
    Logger::info(L"[LightSwitchSettings] Cleaning up settings resources...");

    // Stop and join the debounce thread (std::jthread auto-joins, but we can signal stop too)
    if (m_debounceThread.joinable())
    {
        m_debounceThread.request_stop();
    }

    // Release the file watcher so it closes file handles and background threads
    if (m_settingsFileWatcher)
    {
        m_settingsFileWatcher.reset();
        Logger::info(L"[LightSwitchSettings] File watcher stopped.");
    }

    // Close the Windows event handle
    if (m_settingsChangedEvent)
    {
        CloseHandle(m_settingsChangedEvent);
        m_settingsChangedEvent = nullptr;
        Logger::info(L"[LightSwitchSettings] Settings changed event closed.");
    }

    Logger::info(L"[LightSwitchSettings] Cleanup complete.");
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

HANDLE LightSwitchSettings::GetSettingsChangedEvent() const
{
    return m_settingsChangedEvent;
}

void LightSwitchSettings::LoadSettings()
{
    std::lock_guard<std::mutex> guard(m_settingsMutex);
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
    }
    catch (...)
    {
        // Keeps defaults if load fails
    }
}

void LightSwitchSettings::ApplyThemeIfNecessary()
{
    std::lock_guard<std::mutex> guard(m_settingsMutex);

    SYSTEMTIME st;
    GetLocalTime(&st);
    int nowMinutes = st.wHour * 60 + st.wMinute;

    bool shouldBeLight = false;
    if (m_settings.lightTime < m_settings.darkTime)
        shouldBeLight = (nowMinutes >= m_settings.lightTime && nowMinutes < m_settings.darkTime);
    else
        shouldBeLight = (nowMinutes >= m_settings.lightTime || nowMinutes < m_settings.darkTime);

    bool isSystemCurrentlyLight = GetCurrentSystemTheme();
    bool isAppsCurrentlyLight = GetCurrentAppsTheme();

    if (shouldBeLight)
    {
        if (m_settings.changeSystem && !isSystemCurrentlyLight)
        {
            SetSystemTheme(true);
            Logger::info(L"[LightSwitchService] Changing system theme to light mode.");
        }
        if (m_settings.changeApps && !isAppsCurrentlyLight)
        {
            SetAppsTheme(true);
            Logger::info(L"[LightSwitchService] Changing apps theme to light mode.");
        }
    }
    else
    {
        if (m_settings.changeSystem && isSystemCurrentlyLight)
        {
            SetSystemTheme(false);
            Logger::info(L"[LightSwitchService] Changing system theme to dark mode.");
        }
        if (m_settings.changeApps && isAppsCurrentlyLight)
        {
            SetAppsTheme(false);
            Logger::info(L"[LightSwitchService] Changing apps theme to dark mode.");
        }
    }
}