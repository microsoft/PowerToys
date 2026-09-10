#include "pch.h"
#include "LightSwitchStateManager.h"
#include <logger.h>
#include <LightSwitchUtils.h>
#include "ThemeScheduler.h"
#include <ThemeHelper.h>
#include <common/interop/shared_constants.h>
#include <stdexcept>

namespace
{
    constexpr int MinutesPerDay = 24 * 60;

    int Minutes(const SYSTEMTIME& time)
    {
        return time.wHour * 60 + time.wMinute;
    }

    constexpr int NormalizeMinutes(int minutes, int offset = 0)
    {
        const auto total = static_cast<std::int64_t>(minutes) + offset;
        return static_cast<int>((total % MinutesPerDay + MinutesPerDay) % MinutesPerDay);
    }

    std::optional<std::uint64_t> LocalMinutes(const SYSTEMTIME& time)
    {
        // Encode the complete local wall-clock date without a time-zone conversion.
        // A backward clock adjustment must not look like a forward midnight wrap.
        FILETIME fileTime{};
        if (!SystemTimeToFileTime(&time, &fileTime))
            return std::nullopt;
        ULARGE_INTEGER value{};
        value.LowPart = fileTime.dwLowDateTime;
        value.HighPart = fileTime.dwHighDateTime;
        return value.QuadPart / (60ULL * 10000000ULL);
    }

    std::pair<int, int> UpdateSunTimes(const LightSwitchConfig& settings, const SYSTEMTIME& time)
    {
        const auto sun = CalculateSunriseSunset(std::stod(settings.latitude), std::stod(settings.longitude), time.wYear, time.wMonth, time.wDay);
        const int light = sun.sunriseHour * 60 + sun.sunriseMinute;
        const int dark = sun.sunsetHour * 60 + sun.sunsetMinute;
        try
        {
            auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"LightSwitch");
            values.add_property(L"lightTime", light);
            values.add_property(L"darkTime", dark);
            values.save_to_settings_file();
        }
        catch (...)
        {
            Logger::error(L"[LightSwitchStateManager] Could not save calculated sun times.");
        }
        return { light, dark };
    }

    LightSwitchStateManagerDependencies DefaultDependencies()
    {
        LightSwitchStateManagerDependencies dependencies;
        dependencies.loadSettings = [](LightSwitchConfig& config, std::wstring& error) {
            return LightSwitchSettings::instance().TryLoadSettings(config, error);
        };
        dependencies.readTheme = [](bool system, bool& light) {
            return system ? TryGetSystemTheme(light) : TryGetAppsTheme(light);
        };
        dependencies.writeTheme = [](bool system, bool light) {
            return system ? TrySetSystemTheme(light) : TrySetAppsTheme(light);
        };
        dependencies.readNightLight = IsNightLightEnabled;
        dependencies.localTime = []() {
            SYSTEMTIME now{};
            GetLocalTime(&now);
            return now;
        };
        dependencies.updateSunTimes = UpdateSunTimes;
        return dependencies;
    }

    std::wstring ThemeError(LSTATUS result)
    {
        return L"The requested theme could not be applied or verified (Windows error " + std::to_wstring(result) + L").";
    }
}

LightSwitchStateManager::LightSwitchStateManager() :
    LightSwitchStateManager(DefaultDependencies())
{
}

LightSwitchStateManager::LightSwitchStateManager(LightSwitchStateManagerDependencies dependencies) :
    _dependencies(std::move(dependencies))
{
    if (!_dependencies.loadSettings || !_dependencies.readTheme || !_dependencies.writeTheme ||
        !_dependencies.readNightLight || !_dependencies.localTime || !_dependencies.updateSunTimes)
    {
        throw std::invalid_argument("Incomplete Light Switch state manager dependencies");
    }
}

bool LightSwitchStateManager::LoadSettingsLocked(std::wstring& error)
{
    LightSwitchConfig config;
    if (!_dependencies.loadSettings(config, error))
    {
        return false;
    }
    if (!_hasSettingsSnapshot || !HasSameEffectiveLightSwitchSettings(_settingsSnapshot, config))
    {
        const bool enteringNightLight = !_hasSettingsSnapshot || _settingsSnapshot.scheduleMode != ScheduleMode::FollowNightLight;
        _state.isManualOverride = false;
        _state.lastEvaluatedDate.reset();
        if (config.scheduleMode == ScheduleMode::FollowNightLight && enteringNightLight)
        {
            _state.isNightLightActive = _dependencies.readNightLight();
        }
        _state.lastAppliedMode = config.scheduleMode;
    }
    _settingsSnapshot = std::move(config);
    _hasSettingsSnapshot = true;
    return true;
}

LightSwitchState LightSwitchStateManager::GetState() const
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    return _state;
}

StatusSnapshot LightSwitchStateManager::GetStatusSnapshotLocked(const LightSwitchConfig& config)
{
    StatusSnapshot snapshot;
    snapshot.config = config;
    snapshot.manualOverride = _state.isManualOverride;
    snapshot.configurationAvailable = _hasSettingsSnapshot;
    bool light = false;
    if (_dependencies.readTheme(true, light) == ERROR_SUCCESS)
        snapshot.systemLight = light;
    if (_dependencies.readTheme(false, light) == ERROR_SUCCESS)
        snapshot.appsLight = light;
    return snapshot;
}

StatusSnapshot LightSwitchStateManager::GetStatusSnapshot()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    return GetStatusSnapshotLocked(_settingsSnapshot);
}

void LightSwitchStateManager::SyncThemeStateLocked(const StatusSnapshot& snapshot, bool recordObservation)
{
    if (snapshot.systemLight)
    {
        _state.isSystemLightActive = *snapshot.systemLight;
        if (recordObservation)
            _lastObservedSystemTheme = snapshot.systemLight;
    }
    if (snapshot.appsLight)
    {
        _state.isAppsLightActive = *snapshot.appsLight;
        if (recordObservation)
            _lastObservedAppsTheme = snapshot.appsLight;
    }
}

ThemeCommandResult LightSwitchStateManager::CompleteCommandLocked(const LightSwitchConfig& config, const wchar_t* errorCode, const std::wstring& message)
{
    auto snapshot = GetStatusSnapshotLocked(config);
    // Reporting an error must not consume an external change that the scheduler
    // has not handled yet (for example, when the settings file is unreadable).
    SyncThemeStateLocked(snapshot, false);
    return { errorCode[0] == L'\0', errorCode, message, std::move(snapshot) };
}

ThemeCommandResult LightSwitchStateManager::OnSettingsChanged()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    const bool hadSettingsSnapshot = _hasSettingsSnapshot;
    const auto previousConfig = _settingsSnapshot;
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return CompleteCommandLocked(_settingsSnapshot, L"SETTINGS_READ_FAILED", error);
    const auto config = _settingsSnapshot;
    const auto now = _dependencies.localTime();
    if (hadSettingsSnapshot && HasSameEffectiveLightSwitchSettings(previousConfig, config))
        DetectExternalThemeChangeLocked(config, now);
    const auto result = EvaluateAndApplyIfNeededLocked(config, now);
    return result == ERROR_SUCCESS ? CompleteCommandLocked(config) : CompleteCommandLocked(config, L"THEME_WRITE_FAILED", ThemeError(result));
}

void LightSwitchStateManager::OnTick()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    std::wstring error;
    if (LoadSettingsLocked(error) && _settingsSnapshot.scheduleMode != ScheduleMode::FollowNightLight)
    {
        EvaluateAndApplyIfNeededLocked(_settingsSnapshot, _dependencies.localTime());
    }
}

void LightSwitchStateManager::OnManualOverride()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    std::wstring error;
    if (LoadSettingsLocked(error))
        OnManualOverrideLocked(_settingsSnapshot, _dependencies.localTime());
}

LSTATUS LightSwitchStateManager::OnManualOverrideLocked(const LightSwitchConfig& config, const SYSTEMTIME& now)
{
    UpdateEffectiveTimesLocked(config, now);
    bool crossedBoundary = false;
    if (config.scheduleMode == ScheduleMode::FollowNightLight)
    {
        const bool nightLight = _dependencies.readNightLight();
        crossedBoundary = _state.isNightLightActive != nightLight;
        _state.isNightLightActive = nightLight;
    }
    else if (config.scheduleMode != ScheduleMode::Off)
    {
        crossedBoundary = HasCrossedScheduleBoundaryLocked(now);
    }
    const auto snapshot = GetStatusSnapshotLocked(config);
    if (crossedBoundary)
    {
        // The old override expired before this toggle. Keep the new selection
        // only when it differs from the current plan, as for an explicit set.
        const bool scheduledLight = ScheduledThemeLocked(config, now);
        _state.isManualOverride = (config.changeSystem && snapshot.systemLight && *snapshot.systemLight != scheduledLight) ||
                                  (config.changeApps && snapshot.appsLight && *snapshot.appsLight != scheduledLight);
        RecordEvaluationTimeLocked(now);
    }
    else
    {
        _state.isManualOverride = !_state.isManualOverride;
    }
    SyncThemeStateLocked(snapshot);
    NotifyAppliedThemeLocked(config, snapshot);
    return EvaluateAndApplyIfNeededLocked(config, now);
}

void LightSwitchStateManager::OnNightLightChange()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    const bool hadSettingsSnapshot = _hasSettingsSnapshot;
    const auto previousConfig = _settingsSnapshot;
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return;
    const bool nightLight = _dependencies.readNightLight();
    const bool crossedBoundary = _settingsSnapshot.scheduleMode == ScheduleMode::FollowNightLight && _state.isNightLightActive != nightLight;
    if (crossedBoundary)
        _state.isManualOverride = false;
    _state.isNightLightActive = nightLight;
    const auto now = _dependencies.localTime();
    if (!crossedBoundary && hadSettingsSnapshot && HasSameEffectiveLightSwitchSettings(previousConfig, _settingsSnapshot))
        DetectExternalThemeChangeLocked(_settingsSnapshot, now);
    EvaluateAndApplyIfNeededLocked(_settingsSnapshot, now);
}

void LightSwitchStateManager::SyncInitialThemeState()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    SyncThemeStateLocked(GetStatusSnapshotLocked(_settingsSnapshot));
    _state.isNightLightActive = _dependencies.readNightLight();
    std::wstring error;
    if (LoadSettingsLocked(error))
        EvaluateAndApplyIfNeededLocked(_settingsSnapshot, _dependencies.localTime());
}

bool LightSwitchStateManager::CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon)
{
    try
    {
        const double latitude = std::stod(lat);
        const double longitude = std::stod(lon);
        return !(latitude == 0 && longitude == 0) && latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
    }
    catch (...)
    {
        return false;
    }
}

void LightSwitchStateManager::UpdateEffectiveTimesLocked(const LightSwitchConfig& config, const SYSTEMTIME& now)
{
    if (config.scheduleMode == ScheduleMode::SunsetToSunrise && CoordinatesAreValid(config.latitude, config.longitude))
    {
        const auto localMinutes = LocalMinutes(now);
        if (!localMinutes)
            return;
        const auto date = *localMinutes / MinutesPerDay;
        if (_state.lastEvaluatedDate != date)
        {
            const auto [light, dark] = _dependencies.updateSunTimes(config, now);
            _state.lastEvaluatedDate = date;
            _state.effectiveLightMinutes = NormalizeMinutes(light, config.sunrise_offset);
            _state.effectiveDarkMinutes = NormalizeMinutes(dark, config.sunset_offset);
        }
    }
    else if (config.scheduleMode == ScheduleMode::FixedHours)
    {
        _state.effectiveLightMinutes = NormalizeMinutes(config.lightTime);
        _state.effectiveDarkMinutes = NormalizeMinutes(config.darkTime);
    }
}

void LightSwitchStateManager::RecordEvaluationTimeLocked(const SYSTEMTIME& now)
{
    _lastTickTime = LocalMinutes(now);
    _lastTickLightMinutes = _state.effectiveLightMinutes;
    _lastTickDarkMinutes = _state.effectiveDarkMinutes;
}

bool LightSwitchStateManager::HasCrossedScheduleBoundaryLocked(const SYSTEMTIME& now) const
{
    const auto current = LocalMinutes(now);
    if (!_lastTickTime || !current || *current <= *_lastTickTime)
        return false;
    if (*current - *_lastTickTime >= static_cast<std::uint64_t>(MinutesPerDay))
        return true;

    const int previousMinutes = static_cast<int>(*_lastTickTime % MinutesPerDay);
    const int currentMinutes = static_cast<int>(*current % MinutesPerDay);
    if (*_lastTickTime / MinutesPerDay != *current / MinutesPerDay)
    {
        // Sun times can change at midnight. Use each date's own boundaries.
        return previousMinutes < _lastTickLightMinutes || previousMinutes < _lastTickDarkMinutes ||
               currentMinutes >= _state.effectiveLightMinutes || currentMinutes >= _state.effectiveDarkMinutes;
    }
    return (previousMinutes < _state.effectiveLightMinutes && currentMinutes >= _state.effectiveLightMinutes) ||
           (previousMinutes < _state.effectiveDarkMinutes && currentMinutes >= _state.effectiveDarkMinutes);
}

bool LightSwitchStateManager::ScheduledThemeLocked(const LightSwitchConfig& config, const SYSTEMTIME& now)
{
    return config.scheduleMode == ScheduleMode::FollowNightLight ? !_state.isNightLightActive :
                                                                   ShouldBeLight(Minutes(now), _state.effectiveLightMinutes, _state.effectiveDarkMinutes);
}

LSTATUS LightSwitchStateManager::ApplyThemeLocked(bool light, const LightSwitchConfig& config, bool& changed)
{
    LSTATUS result = ERROR_SUCCESS;
    changed = false;
    for (const bool system : { true, false })
    {
        if (!(system ? config.changeSystem : config.changeApps))
            continue;
        bool actual = false;
        if (_dependencies.readTheme(system, actual) != ERROR_SUCCESS || actual != light)
        {
            const auto writeResult = _dependencies.writeTheme(system, light);
            if (writeResult != ERROR_SUCCESS && result == ERROR_SUCCESS)
                result = writeResult;
            changed = true;
        }
    }
    const auto snapshot = GetStatusSnapshotLocked(config);
    SyncThemeStateLocked(snapshot);
    if ((config.changeSystem && snapshot.systemLight != std::optional<bool>(light)) ||
        (config.changeApps && snapshot.appsLight != std::optional<bool>(light)))
    {
        if (result == ERROR_SUCCESS)
            result = ERROR_WRITE_FAULT;
    }
    return result;
}

LSTATUS LightSwitchStateManager::EvaluateAndApplyIfNeededLocked(const LightSwitchConfig& config, const SYSTEMTIME& now)
{
    if (config.scheduleMode == ScheduleMode::Off)
    {
        RecordEvaluationTimeLocked(now);
        return ERROR_SUCCESS;
    }
    UpdateEffectiveTimesLocked(config, now);
    if (_state.isManualOverride)
    {
        // Night Light overrides end on an actual Night Light transition, not a clock tick.
        if (config.scheduleMode == ScheduleMode::FollowNightLight)
        {
            RecordEvaluationTimeLocked(now);
            return ERROR_SUCCESS;
        }
        if (!HasCrossedScheduleBoundaryLocked(now))
        {
            RecordEvaluationTimeLocked(now);
            return ERROR_SUCCESS;
        }
        _state.isManualOverride = false;
    }
    _state.lastAppliedMode = config.scheduleMode;
    bool changed = false;
    const auto result = ApplyThemeLocked(ScheduledThemeLocked(config, now), config, changed);
    if (result == ERROR_SUCCESS && changed)
        NotifyAppliedThemeLocked(config, GetStatusSnapshotLocked(config));
    else if (result != ERROR_SUCCESS)
        Logger::warn(L"[LightSwitchStateManager] Scheduled theme application failed (error: {}).", result);
    RecordEvaluationTimeLocked(now);
    return result;
}

void LightSwitchStateManager::NotifyAppliedThemeLocked(const LightSwitchConfig& config, const StatusSnapshot& snapshot)
{
    const auto theme = config.changeSystem ? snapshot.systemLight : (config.changeApps ? snapshot.appsLight : std::nullopt);
    if (!theme)
        return;
    if (_dependencies.notifyThemeChanged)
        _dependencies.notifyThemeChanged(*theme);
    else
        NotifyPowerDisplayThemeChanged(*theme);
}

ThemeCommandResult LightSwitchStateManager::SetTheme(bool light)
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return CompleteCommandLocked(_settingsSnapshot, L"SETTINGS_READ_FAILED", error);
    const auto config = _settingsSnapshot;
    if (!config.changeSystem && !config.changeApps)
        return CompleteCommandLocked(config, L"NO_TARGETS", L"No theme targets are enabled in Light Switch settings.");
    bool changed = false;
    const auto result = ApplyThemeLocked(light, config, changed);
    if (result != ERROR_SUCCESS)
        return CompleteCommandLocked(config, L"THEME_WRITE_FAILED", ThemeError(result));

    const auto now = _dependencies.localTime();
    UpdateEffectiveTimesLocked(config, now);
    if (config.scheduleMode == ScheduleMode::FollowNightLight)
        _state.isNightLightActive = _dependencies.readNightLight();
    _state.isManualOverride = config.scheduleMode != ScheduleMode::Off && light != ScheduledThemeLocked(config, now);
    RecordEvaluationTimeLocked(now);
    if (changed)
        NotifyAppliedThemeLocked(config, GetStatusSnapshotLocked(config));
    return CompleteCommandLocked(config);
}

ThemeCommandResult LightSwitchStateManager::ToggleTheme()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return CompleteCommandLocked(_settingsSnapshot, L"SETTINGS_READ_FAILED", error);
    const auto config = _settingsSnapshot;
    if (!config.changeSystem && !config.changeApps)
        return CompleteCommandLocked(config, L"NO_TARGETS", L"No theme targets are enabled in Light Switch settings.");
    const auto before = GetStatusSnapshotLocked(config);
    if ((config.changeSystem && !before.systemLight) || (config.changeApps && !before.appsLight))
        return CompleteCommandLocked(config, L"THEME_READ_FAILED", L"The current theme could not be read. No themes were changed.");

    LSTATUS result = ERROR_SUCCESS;
    for (const bool system : { true, false })
    {
        if (!(system ? config.changeSystem : config.changeApps))
            continue;
        const auto writeResult = _dependencies.writeTheme(system, !(system ? *before.systemLight : *before.appsLight));
        if (writeResult != ERROR_SUCCESS && result == ERROR_SUCCESS)
            result = writeResult;
    }
    const auto after = GetStatusSnapshotLocked(config);
    SyncThemeStateLocked(after);
    if ((config.changeSystem && after.systemLight != std::optional<bool>(!*before.systemLight)) ||
        (config.changeApps && after.appsLight != std::optional<bool>(!*before.appsLight)))
    {
        if (result == ERROR_SUCCESS)
            result = ERROR_WRITE_FAULT;
    }
    if (result == ERROR_SUCCESS)
        result = OnManualOverrideLocked(config, _dependencies.localTime());
    return result == ERROR_SUCCESS ? CompleteCommandLocked(config) : CompleteCommandLocked(config, L"THEME_WRITE_FAILED", ThemeError(result));
}

void LightSwitchStateManager::DetectExternalThemeChange()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    const bool hadSettingsSnapshot = _hasSettingsSnapshot;
    const auto previousConfig = _settingsSnapshot;
    std::wstring error;
    if (!LoadSettingsLocked(error) || _settingsSnapshot.scheduleMode == ScheduleMode::Off || _state.isManualOverride)
        return;
    // A minute tick can read an edit before its debounced settings notification.
    // Let the scheduler apply that edit before comparing Windows with the new plan.
    if (hadSettingsSnapshot && !HasSameEffectiveLightSwitchSettings(previousConfig, _settingsSnapshot))
        return;
    DetectExternalThemeChangeLocked(_settingsSnapshot, _dependencies.localTime());
}

void LightSwitchStateManager::DetectExternalThemeChangeLocked(const LightSwitchConfig& config, const SYSTEMTIME& now)
{
    if (config.scheduleMode == ScheduleMode::Off || _state.isManualOverride)
        return;
    UpdateEffectiveTimesLocked(config, now);
    const bool nightLight = config.scheduleMode == ScheduleMode::FollowNightLight ? _dependencies.readNightLight() : _state.isNightLightActive;
    const bool scheduledLight = config.scheduleMode == ScheduleMode::FollowNightLight ? !nightLight : ScheduledThemeLocked(config, now);
    const auto snapshot = GetStatusSnapshotLocked(config);
    // An unchanged Windows theme at a new scheduled boundary is not an external
    // edit. Require a change from the last observation as well as a plan mismatch.
    const bool mismatch = (config.changeSystem && snapshot.systemLight && *snapshot.systemLight != scheduledLight && snapshot.systemLight != _lastObservedSystemTheme) ||
                          (config.changeApps && snapshot.appsLight && *snapshot.appsLight != scheduledLight && snapshot.appsLight != _lastObservedAppsTheme);
    if (mismatch)
    {
        Logger::info(L"[LightSwitchStateManager] External theme change detected.");
        _state.isManualOverride = true;
        if (config.scheduleMode == ScheduleMode::FollowNightLight)
            _state.isNightLightActive = nightLight;
        SyncThemeStateLocked(snapshot);
        RecordEvaluationTimeLocked(now);
        NotifyAppliedThemeLocked(config, snapshot);
    }
}
// Notify PowerDisplay that LightSwitch applied a new theme.
void LightSwitchStateManager::NotifyPowerDisplayThemeChanged(bool isLight)
{
    try
    {
        // The event carries only the resulting theme. PowerDisplay owns profile
        // enablement, reference validation, and application.
        const wchar_t* eventName = isLight ? CommonSharedConstants::LIGHT_SWITCH_LIGHT_THEME_EVENT : CommonSharedConstants::LIGHT_SWITCH_DARK_THEME_EVENT;

        Logger::info(L"[LightSwitchStateManager] Notifying PowerDisplay about theme change (isLight: {})", isLight);

        HANDLE hThemeEvent = CreateEventW(nullptr, FALSE, FALSE, eventName);
        if (!hThemeEvent)
        {
            Logger::warn(L"[LightSwitchStateManager] Failed to create theme event (error: {})", GetLastError());
            return;
        }

        if (!SetEvent(hThemeEvent))
        {
            Logger::warn(L"[LightSwitchStateManager] Failed to signal theme event '{}' (error: {})", eventName, GetLastError());
        }
        else
        {
            Logger::info(L"[LightSwitchStateManager] Theme event signaled to PowerDisplay: {}", eventName);
        }

        CloseHandle(hThemeEvent);
    }
    catch (...)
    {
        Logger::error(L"[LightSwitchStateManager] Failed to notify PowerDisplay");
    }
}
