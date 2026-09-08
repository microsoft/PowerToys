#include "LightSwitchSettings.h"
#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>
#include "SettingsObserver.h"
#include <filesystem>
#include <fstream>
#include <cmath>
#include <climits>
#include <stdexcept>
#include <roapi.h>
#include <logger.h>
#include <LightSwitchService/trace.h>

using namespace std;

namespace
{
    struct ApartmentScope
    {
        HRESULT result = RoInitialize(RO_INIT_MULTITHREADED);
        ~ApartmentScope()
        {
            if (SUCCEEDED(result))
                RoUninitialize();
        }
        bool IsReady() const
        {
            return SUCCEEDED(result) || result == RPC_E_CHANGED_MODE;
        }
    };
}

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
                        std::lock_guard<std::mutex> lock(m_debounceMutex);
                        const auto elapsed = steady_clock::now() - m_lastChangeTime;
                        if (elapsed >= seconds(1))
                            break;
                    }

                    if (stop.stop_requested())
                        return;

                    {
                        std::lock_guard<std::mutex> lock(m_debounceMutex);
                        m_debouncePending = false;
                    }

                    Logger::info(L"[LightSwitchSettings] Settings file stabilized, reloading.");

                    try
                    {
                        LoadSettings();
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

    // Release the file watcher so it closes file handles and background threads
    if (m_settingsFileWatcher)
    {
        m_settingsFileWatcher.reset();
        Logger::info(L"[LightSwitchSettings] File watcher stopped.");
    }

    if (m_debounceThread.joinable())
    {
        m_debounceThread.request_stop();
        m_debounceThread.join();
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

bool TryParseLightSwitchConfig(const json::JsonObject& values, LightSwitchConfig& config, std::wstring& error)
{
    try
    {
        auto properties = values.GetNamedObject(L"properties");
        LightSwitchConfig parsed;
        const std::wstring mode = properties.GetNamedObject(L"scheduleMode").GetNamedString(L"value").c_str();
        if (mode != L"Off" && mode != L"FixedHours" && mode != L"SunsetToSunrise" && mode != L"FollowNightLight")
        {
            error = L"The configured scheduleMode is not supported.";
            return false;
        }
        parsed.scheduleMode = FromString(mode);
        parsed.changeSystem = properties.GetNamedObject(L"changeSystem").GetNamedBoolean(L"value");
        parsed.changeApps = properties.GetNamedObject(L"changeApps").GetNamedBoolean(L"value");

        const auto readString = [&](const wchar_t* name, std::wstring& destination) {
            if (properties.HasKey(name))
            {
                destination = properties.GetNamedObject(name).GetNamedString(L"value").c_str();
            }
        };
        const auto readInteger = [&](const wchar_t* name, int& destination) {
            if (properties.HasKey(name))
            {
                const double value = properties.GetNamedObject(name).GetNamedNumber(L"value");
                if (!std::isfinite(value) || std::trunc(value) != value || value < INT_MIN || value > INT_MAX)
                {
                    throw std::invalid_argument("Invalid integer setting");
                }
                destination = static_cast<int>(value);
            }
        };
        readString(L"latitude", parsed.latitude);
        readString(L"longitude", parsed.longitude);
        readInteger(L"lightTime", parsed.lightTime);
        readInteger(L"darkTime", parsed.darkTime);
        readInteger(L"sunrise_offset", parsed.sunrise_offset);
        readInteger(L"sunset_offset", parsed.sunset_offset);
        config = std::move(parsed);
        error.clear();
        return true;
    }
    catch (...)
    {
        error = L"Light Switch settings contain missing or invalid properties.";
        return false;
    }
}

bool HasSameEffectiveLightSwitchSettings(const LightSwitchConfig& left, const LightSwitchConfig& right)
{
    // Sun times are calculated by the service and echoed back through settings.json.
    const bool compareTimes = left.scheduleMode != ScheduleMode::SunsetToSunrise;
    return left.scheduleMode == right.scheduleMode &&
           left.latitude == right.latitude && left.longitude == right.longitude &&
           left.sunrise_offset == right.sunrise_offset && left.sunset_offset == right.sunset_offset &&
           left.changeSystem == right.changeSystem && left.changeApps == right.changeApps &&
           (!compareTimes || (left.lightTime == right.lightTime && left.darkTime == right.darkTime));
}

bool TryPatchLightSwitchScheduleMode(const std::wstring& path, ScheduleMode mode, LightSwitchConfig& config, std::wstring& error)
{
    const ApartmentScope apartment;
    if (!apartment.IsReady())
    {
        error = L"The settings JSON runtime could not be initialized.";
        return false;
    }
    if (mode != ScheduleMode::Off && mode != ScheduleMode::FixedHours &&
        mode != ScheduleMode::SunsetToSunrise && mode != ScheduleMode::FollowNightLight)
    {
        error = L"The requested schedule mode is not supported.";
        return false;
    }
    std::wstring temporaryPath;
    HANDLE file = INVALID_HANDLE_VALUE;
    try
    {
        auto document = json::from_file(path);
        LightSwitchConfig current;
        if (!document || !TryParseLightSwitchConfig(*document, current, error))
        {
            if (!document)
            {
                error = L"Light Switch settings could not be read.";
            }
            return false;
        }
        if (current.scheduleMode == mode)
        {
            config = std::move(current);
            error.clear();
            return true;
        }

        const std::wstring original = document->Stringify().c_str();
        auto property = document->GetNamedObject(L"properties").GetNamedObject(L"scheduleMode");
        property.SetNamedValue(L"value", json::value(ToString(mode)));
        const auto serialized = winrt::to_string(document->Stringify());
        static std::atomic<unsigned long> sequence{ 0 };
        temporaryPath = path + L".cli." + std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(++sequence) + L".tmp";
        file = CreateFileW(temporaryPath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            error = L"A temporary settings file could not be created (error " + std::to_wstring(GetLastError()) + L").";
            return false;
        }
        DWORD written = 0;
        const bool saved = serialized.size() <= MAXDWORD &&
                           WriteFile(file, serialized.data(), static_cast<DWORD>(serialized.size()), &written, nullptr) &&
                           written == serialized.size() && FlushFileBuffers(file);
        CloseHandle(file);
        file = INVALID_HANDLE_VALUE;
        if (!saved)
        {
            DeleteFileW(temporaryPath.c_str());
            error = L"Light Switch settings could not be written.";
            return false;
        }

        // Avoid replacing a settings edit that arrived while we prepared the patch.
        const auto latest = json::from_file(path);
        if (!latest || std::wstring(latest->Stringify().c_str()) != original)
        {
            DeleteFileW(temporaryPath.c_str());
            error = L"Light Switch settings changed during the update. Try the command again.";
            return false;
        }
        if (!MoveFileExW(temporaryPath.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        {
            const auto replacementError = GetLastError();
            DeleteFileW(temporaryPath.c_str());
            error = L"Light Switch settings could not be replaced (error " + std::to_wstring(replacementError) + L").";
            return false;
        }
        temporaryPath.clear();
        const auto savedDocument = json::from_file(path);
        if (!savedDocument || !TryParseLightSwitchConfig(*savedDocument, config, error) ||
            config.scheduleMode != mode || savedDocument->Stringify() != document->Stringify())
        {
            error = L"The saved Light Switch settings could not be verified.";
            return false;
        }
        error.clear();
        return true;
    }
    catch (...)
    {
        if (file != INVALID_HANDLE_VALUE)
        {
            CloseHandle(file);
        }
        if (!temporaryPath.empty())
        {
            DeleteFileW(temporaryPath.c_str());
        }
        error = L"Light Switch settings could not be updated.";
        return false;
    }
}

void LightSwitchSettings::LoadSettings()
{
    LightSwitchConfig config;
    std::wstring error;
    TryLoadSettings(config, error);
}

bool LightSwitchSettings::TryLoadSettings(LightSwitchConfig& config, std::wstring& error)
{
    const ApartmentScope apartment;
    if (!apartment.IsReady())
    {
        error = L"The settings JSON runtime could not be initialized.";
        return false;
    }
    std::lock_guard<std::mutex> guard(m_settingsMutex);
    const auto document = json::from_file(GetSettingsFileName());
    if (!document)
    {
        error = L"Light Switch settings could not be read or contain invalid JSON.";
        return false;
    }
    LightSwitchConfig loaded;
    if (!TryParseLightSwitchConfig(*document, loaded, error))
    {
        return false;
    }
    ApplySettingsLocked(loaded);
    config = m_settings;
    return true;
}

bool LightSwitchSettings::TrySetScheduleMode(ScheduleMode mode, LightSwitchConfig& config, std::wstring& error)
{
    std::lock_guard<std::mutex> guard(m_settingsMutex);
    if (!TryPatchLightSwitchScheduleMode(GetSettingsFileName(), mode, config, error))
    {
        return false;
    }
    ApplySettingsLocked(config);
    return true;
}

void LightSwitchSettings::ApplySettingsLocked(const LightSwitchConfig& config)
{
    const auto previous = m_settings;
    m_settings = config;
    if (previous.scheduleMode != config.scheduleMode)
    {
        Trace::LightSwitch::ScheduleModeToggled(ToString(config.scheduleMode));
        NotifyObservers(SettingId::ScheduleMode);
    }
    if (previous.latitude != config.latitude)
        NotifyObservers(SettingId::Latitude);
    if (previous.longitude != config.longitude)
        NotifyObservers(SettingId::Longitude);
    if (previous.lightTime != config.lightTime)
        NotifyObservers(SettingId::LightTime);
    if (previous.darkTime != config.darkTime)
        NotifyObservers(SettingId::DarkTime);
    if (previous.sunrise_offset != config.sunrise_offset)
        NotifyObservers(SettingId::Sunrise_Offset);
    if (previous.sunset_offset != config.sunset_offset)
        NotifyObservers(SettingId::Sunset_Offset);
    if (previous.changeSystem != config.changeSystem)
        NotifyObservers(SettingId::ChangeSystem);
    if (previous.changeApps != config.changeApps)
        NotifyObservers(SettingId::ChangeApps);
    if (previous.changeSystem != config.changeSystem || previous.changeApps != config.changeApps)
    {
        Trace::LightSwitch::ThemeTargetChanged(config.changeApps, config.changeSystem);
    }
}
