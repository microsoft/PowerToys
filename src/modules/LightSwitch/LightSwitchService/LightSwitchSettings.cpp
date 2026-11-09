#include "LightSwitchSettings.h"
#include "LocalizedStrings.h"
#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <array>
#include <cmath>
#include <climits>
#include <stdexcept>
#include <roapi.h>
#include <wil/resource.h>
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

    std::optional<json::JsonObject> ReadInitialSettings(HANDLE file, std::wstring& error)
    {
        // Keep the share-delete handle open throughout the read. Reopening with
        // std::ifstream can conflict with another initializer publishing its file.
        std::string contents;
        std::array<char, 4096> buffer{};
        for (;;)
        {
            DWORD read = 0;
            if (!ReadFile(file, buffer.data(), static_cast<DWORD>(buffer.size()), &read, nullptr))
            {
                const auto readError = GetLastError();
                error = LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_SETTINGS_READ_ERROR), readError);
                return std::nullopt;
            }
            if (read == 0)
                break;
            contents.append(buffer.data(), read);
        }
        json::JsonObject document;
        if (!json::JsonObject::TryParse(winrt::to_hstring(contents), document))
        {
            error = GET_RESOURCE_STRING(IDS_SETTINGS_JSON_INVALID);
            return std::nullopt;
        }
        return document;
    }

    class TemporarySettingsFile
    {
    public:
        TemporarySettingsFile(const std::wstring& destination, const wchar_t* operation)
        {
            static std::atomic<unsigned long> sequence{ 0 };
            _path = destination + L"." + operation + L"." + std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(GetTickCount64()) + L"." + std::to_wstring(++sequence) + L".tmp";
        }

        ~TemporarySettingsFile()
        {
            if (_created)
                DeleteFileW(_path.c_str());
        }

        TemporarySettingsFile(const TemporarySettingsFile&) = delete;
        TemporarySettingsFile& operator=(const TemporarySettingsFile&) = delete;

        bool Write(const std::string& contents, std::wstring& error)
        {
            wil::unique_hfile file(CreateFileW(_path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr));
            if (!file)
            {
                const auto createError = GetLastError();
                error = LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_SETTINGS_TEMPORARY_FILE_FAILED), createError);
                return false;
            }
            _created = true;
            DWORD written = 0;
            if (contents.size() > MAXDWORD ||
                !WriteFile(file.get(), contents.data(), static_cast<DWORD>(contents.size()), &written, nullptr) ||
                written != contents.size() || !FlushFileBuffers(file.get()))
            {
                error = GET_RESOURCE_STRING(IDS_SETTINGS_WRITE_FAILED);
                return false;
            }
            return true;
        }

        bool Publish(const std::wstring& destination, DWORD flags)
        {
            if (!MoveFileExW(_path.c_str(), destination.c_str(), flags))
                return false;
            _created = false;
            return true;
        }

    private:
        std::wstring _path;
        bool _created = false;
    };

    enum class SettingsFileSaveResult
    {
        Saved,
        Superseded,
        Failed,
    };

    SettingsFileSaveResult SaveSettingsDocument(
        const std::wstring& path,
        const std::wstring& original,
        const json::JsonObject& document,
        const wchar_t* operation,
        LightSwitchConfig& config,
        std::wstring& error)
    {
        LightSwitchConfig saved;
        if (!TryParseLightSwitchConfig(document, saved, error))
            return SettingsFileSaveResult::Failed;

        const auto serialized = document.Stringify();
        TemporarySettingsFile temporary(path, operation);
        if (!temporary.Write(winrt::to_string(serialized), error))
            return SettingsFileSaveResult::Failed;

        // Avoid replacing an external edit received while preparing the patch.
        const auto latest = json::from_file(path);
        if (!latest)
        {
            error = GET_RESOURCE_STRING(IDS_SETTINGS_UPDATE_READ_FAILED);
            return SettingsFileSaveResult::Failed;
        }
        if (std::wstring(latest->Stringify().c_str()) != original)
        {
            error = GET_RESOURCE_STRING(IDS_SETTINGS_UPDATE_SUPERSEDED);
            return SettingsFileSaveResult::Superseded;
        }
        if (!temporary.Publish(path, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        {
            const auto replacementError = GetLastError();
            error = LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_SETTINGS_REPLACE_FAILED), replacementError);
            return SettingsFileSaveResult::Failed;
        }

        config = std::move(saved);
        error.clear();
        return SettingsFileSaveResult::Saved;
    }

    std::string DefaultSettingsJson()
    {
        // Match LightSwitchProperties in Settings UI and the module's hotkey defaults.
        const LightSwitchConfig defaults;
        PowerToysSettings::PowerToyValues values(L"LightSwitch", L"LightSwitch");
        values.add_property(L"scheduleMode", L"Off");
        values.add_property(L"changeSystem", true);
        values.add_property(L"changeApps", true);
        values.add_property(L"lightTime", defaults.lightTime);
        values.add_property(L"darkTime", defaults.darkTime);
        values.add_property(L"latitude", defaults.latitude);
        values.add_property(L"longitude", defaults.longitude);
        values.add_property(L"sunrise_offset", defaults.sunrise_offset);
        values.add_property(L"sunset_offset", defaults.sunset_offset);
        values.add_property(L"toggle-theme-hotkey", PowerToysSettings::HotkeyObject::from_settings(true, true, false, true, 'D').get_json());
        values.add_property(L"enableDarkModeProfile", false);
        values.add_property(L"enableLightModeProfile", false);
        values.add_property(L"darkModeProfile", L"");
        values.add_property(L"lightModeProfile", L"");
        values.add_property(L"darkModeProfileId", 0);
        values.add_property(L"lightModeProfileId", 0);
        return winrt::to_string(values.serialize());
    }
}

LightSwitchSettings& LightSwitchSettings::instance()
{
    static LightSwitchSettings inst;
    return inst;
}

LightSwitchSettings::LightSwitchSettings()
{
    LightSwitchConfig config;
    std::wstring error;
    if (TryInitializeLightSwitchSettings(GetSettingsFileName(), config, error))
    {
        ApplySettingsLocked(config);
    }
    else
    {
        Logger::warn(L"[LightSwitchSettings] Could not initialize settings: {}", error);
    }
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

                    Logger::info(L"[LightSwitchSettings] Settings file stabilized, notifying worker.");
                    SetEvent(m_settingsChangedEvent);
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
            error = GET_RESOURCE_STRING(IDS_SETTINGS_MODE_UNSUPPORTED);
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
        error = GET_RESOURCE_STRING(IDS_SETTINGS_PROPERTIES_INVALID);
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

bool TryInitializeLightSwitchSettings(const std::wstring& path, LightSwitchConfig& config, std::wstring& error)
{
    const ApartmentScope apartment;
    if (!apartment.IsReady())
    {
        error = GET_RESOURCE_STRING(IDS_SETTINGS_RUNTIME_FAILED);
        return false;
    }

    try
    {
        wil::unique_hfile existing(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        if (!existing)
        {
            const auto openError = GetLastError();
            // The settings helper has already created the module directory. Missing
            // parents and unreadable files are errors, not a first-run configuration.
            if (openError != ERROR_FILE_NOT_FOUND)
            {
                error = LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_SETTINGS_OPEN_FAILED), openError);
                return false;
            }

            TemporarySettingsFile temporary(path, L"init");
            if (!temporary.Write(DefaultSettingsJson(), error))
            {
                return false;
            }

            // Publish the complete document without replacing a concurrent first
            // save from Settings UI or another service. Read whichever file won.
            const auto published = temporary.Publish(path, MOVEFILE_WRITE_THROUGH);
            const auto publishError = published ? ERROR_SUCCESS : GetLastError();
            existing.reset(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
            if (!existing)
            {
                const auto readError = GetLastError();
                error = LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_SETTINGS_OPEN_AFTER_INITIALIZATION_FAILED), readError, publishError);
                return false;
            }
            // A failed publish is only acceptable when a readable, valid file
            // actually won the race. Never replace it or return guessed defaults.
        }

        const auto document = ReadInitialSettings(existing.get(), error);
        if (!document)
        {
            return false;
        }
        return TryParseLightSwitchConfig(*document, config, error);
    }
    catch (...)
    {
        error = GET_RESOURCE_STRING(IDS_SETTINGS_INITIALIZATION_FAILED);
        return false;
    }
}

bool TryPatchLightSwitchScheduleMode(const std::wstring& path, ScheduleMode mode, LightSwitchConfig& config, std::wstring& error)
{
    const ApartmentScope apartment;
    if (!apartment.IsReady())
    {
        error = GET_RESOURCE_STRING(IDS_SETTINGS_RUNTIME_FAILED);
        return false;
    }
    if (mode != ScheduleMode::Off && mode != ScheduleMode::FixedHours &&
        mode != ScheduleMode::SunsetToSunrise && mode != ScheduleMode::FollowNightLight)
    {
        error = GET_RESOURCE_STRING(IDS_SCHEDULE_MODE_UNSUPPORTED);
        return false;
    }
    try
    {
        auto document = json::from_file(path);
        LightSwitchConfig current;
        if (!document || !TryParseLightSwitchConfig(*document, current, error))
        {
            if (!document)
            {
                error = GET_RESOURCE_STRING(IDS_SETTINGS_READ_FAILED);
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
        return SaveSettingsDocument(path, original, *document, L"cli", config, error) == SettingsFileSaveResult::Saved;
    }
    catch (...)
    {
        error = GET_RESOURCE_STRING(IDS_SETTINGS_UPDATE_FAILED);
        return false;
    }
}

SunTimesSaveResult SaveLightSwitchSunTimes(const std::wstring& path, const LightSwitchConfig& expected, int lightMinutes, int darkMinutes, LightSwitchConfig& config, std::wstring& error)
{
    const ApartmentScope apartment;
    if (!apartment.IsReady())
    {
        error = GET_RESOURCE_STRING(IDS_SETTINGS_RUNTIME_FAILED);
        return SunTimesSaveResult::Failed;
    }
    try
    {
        auto document = json::from_file(path);
        LightSwitchConfig current;
        if (!document || !TryParseLightSwitchConfig(*document, current, error))
        {
            if (!document)
                error = GET_RESOURCE_STRING(IDS_SETTINGS_READ_FAILED);
            return SunTimesSaveResult::Failed;
        }
        // Calculated values belong to one effective configuration. A later
        // schedule or location edit must win over an in-flight cache update.
        if (expected.scheduleMode != ScheduleMode::SunsetToSunrise ||
            current.scheduleMode != ScheduleMode::SunsetToSunrise ||
            !HasSameEffectiveLightSwitchSettings(expected, current))
        {
            error.clear();
            return SunTimesSaveResult::Superseded;
        }
        if (current.lightTime == lightMinutes && current.darkTime == darkMinutes)
        {
            config = std::move(current);
            error.clear();
            return SunTimesSaveResult::Saved;
        }

        const std::wstring original = document->Stringify().c_str();
        const auto properties = document->GetNamedObject(L"properties");
        const auto updateTime = [&](const wchar_t* name, int minutes) {
            auto property = properties.GetNamedObject(name, json::JsonObject{});
            property.SetNamedValue(L"value", json::value(minutes));
            properties.SetNamedValue(name, property);
        };
        updateTime(L"lightTime", lightMinutes);
        updateTime(L"darkTime", darkMinutes);
        switch (SaveSettingsDocument(path, original, *document, L"sun", config, error))
        {
        case SettingsFileSaveResult::Saved:
            return SunTimesSaveResult::Saved;
        case SettingsFileSaveResult::Superseded:
            error.clear();
            return SunTimesSaveResult::Superseded;
        default:
            return SunTimesSaveResult::Failed;
        }
    }
    catch (...)
    {
        error = GET_RESOURCE_STRING(IDS_SUN_TIMES_SAVE_FAILED);
        return SunTimesSaveResult::Failed;
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
        error = GET_RESOURCE_STRING(IDS_SETTINGS_RUNTIME_FAILED);
        return false;
    }
    std::lock_guard<std::mutex> guard(m_settingsMutex);
    const auto document = json::from_file(GetSettingsFileName());
    if (!document)
    {
        error = GET_RESOURCE_STRING(IDS_SETTINGS_READ_OR_JSON_FAILED);
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

SunTimesSaveResult LightSwitchSettings::SaveSunTimes(const LightSwitchConfig& expected, int lightMinutes, int darkMinutes, std::wstring& error)
{
    // Share the complete read-modify-write lock with CLI schedule persistence.
    std::lock_guard<std::mutex> guard(m_settingsMutex);
    LightSwitchConfig config;
    const auto result = SaveLightSwitchSunTimes(GetSettingsFileName(), expected, lightMinutes, darkMinutes, config, error);
    if (result == SunTimesSaveResult::Saved)
        ApplySettingsLocked(config);
    return result;
}

void LightSwitchSettings::ApplySettingsLocked(const LightSwitchConfig& config)
{
    const auto previous = m_settings;
    m_settings = config;
    if (previous.scheduleMode != config.scheduleMode)
    {
        Trace::LightSwitch::ScheduleModeToggled(ToString(config.scheduleMode));
    }
    if (previous.changeSystem != config.changeSystem || previous.changeApps != config.changeApps)
    {
        Trace::LightSwitch::ThemeTargetChanged(config.changeApps, config.changeSystem);
    }
}
