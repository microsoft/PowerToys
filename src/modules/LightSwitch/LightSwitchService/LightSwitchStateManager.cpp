#include "pch.h"
#include "LightSwitchStateManager.h"
#include "LocalizedStrings.h"
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
            std::wstring error;
            if (LightSwitchSettings::instance().SaveSunTimes(settings, light, dark, error) == SunTimesSaveResult::Failed)
                Logger::error(L"[LightSwitchStateManager] Could not save calculated sun times: {}", error);
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
        dependencies.readNightLight = []() -> std::optional<bool> {
            bool enabled = false;
            return TryGetNightLightState(enabled) == ERROR_SUCCESS ? std::optional<bool>(enabled) : std::nullopt;
        };
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
        return LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_THEME_WRITE_FAILED), result);
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
        _pendingThemeNotification.reset();
        if (config.scheduleMode == ScheduleMode::FollowNightLight && enteringNightLight)
        {
            _hasNightLightState = false;
            RefreshNightLightStateLocked(config);
        }
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

std::optional<bool> LightSwitchStateManager::RefreshNightLightStateLocked(const LightSwitchConfig& config)
{
    const auto nightLight = _dependencies.readNightLight();
    if (!nightLight)
        return std::nullopt;
    const bool crossedBoundary = config.scheduleMode == ScheduleMode::FollowNightLight &&
                                 _hasNightLightState && _state.isNightLightActive != *nightLight;
    if (crossedBoundary)
        _state.isManualOverride = false;
    _state.isNightLightActive = *nightLight;
    _hasNightLightState = true;
    return crossedBoundary;
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

void LightSwitchStateManager::RecordThemeObservationsLocked(const StatusSnapshot& snapshot)
{
    if (snapshot.systemLight)
        _lastObservedSystemTheme = snapshot.systemLight;
    if (snapshot.appsLight)
        _lastObservedAppsTheme = snapshot.appsLight;
}

ThemeCommandResult LightSwitchStateManager::CompleteCommandLocked(const LightSwitchConfig& config, const wchar_t* errorCode, const std::wstring& message)
{
    return CompleteCommandLocked(GetStatusSnapshotLocked(config), errorCode, message);
}

ThemeCommandResult LightSwitchStateManager::CompleteCommandLocked(StatusSnapshot snapshot, const wchar_t* errorCode, const std::wstring& message)
{
    // Reuse verified theme readings, but report the override after command handling.
    snapshot.manualOverride = _state.isManualOverride;
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
    // External detection records the current Night Light baseline when it finds
    // a new manual choice. Sampling afterwards preserves that newer override.
    if (config.scheduleMode == ScheduleMode::FollowNightLight && !RefreshNightLightStateLocked(config).has_value())
        return CompleteCommandLocked(config, L"THEME_READ_FAILED", GET_RESOURCE_STRING(IDS_NIGHT_LIGHT_READ_FAILED));
    auto snapshot = GetStatusSnapshotLocked(config);
    const auto result = EvaluateAndApplyIfNeededLocked(config, now, &snapshot);
    return result == ERROR_SUCCESS ? CompleteCommandLocked(std::move(snapshot)) : CompleteCommandLocked(std::move(snapshot), L"THEME_WRITE_FAILED", ThemeError(result));
}

void LightSwitchStateManager::OnTick()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    const bool hadSettingsSnapshot = _hasSettingsSnapshot;
    const auto previousConfig = _settingsSnapshot;
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return;
    const auto now = _dependencies.localTime();
    // A minute tick can read an edit before its debounced settings notification.
    // Apply that edit before comparing Windows with the new plan.
    if (!hadSettingsSnapshot || HasSameEffectiveLightSwitchSettings(previousConfig, _settingsSnapshot))
        DetectExternalThemeChangeLocked(_settingsSnapshot, now);
    // Detect external choices before sampling Night Light so a newly established
    // override is not cleared by an older, previously unobserved transition.
    // Retry missed notifications and failed writes even without another transition.
    if (_settingsSnapshot.scheduleMode == ScheduleMode::FollowNightLight && !RefreshNightLightStateLocked(_settingsSnapshot).has_value())
        return;
    EvaluateAndApplyIfNeededLocked(_settingsSnapshot, now);
}

LSTATUS LightSwitchStateManager::OnManualOverrideLocked(const LightSwitchConfig& config, const SYSTEMTIME& now, StatusSnapshot& snapshot)
{
    _pendingThemeNotification.reset();
    UpdateEffectiveTimesLocked(config, now);
    bool crossedBoundary = false;
    bool unknownNightLight = false;
    if (config.scheduleMode == ScheduleMode::FollowNightLight)
    {
        const auto nightLight = _dependencies.readNightLight();
        unknownNightLight = !nightLight.has_value();
        if (nightLight)
        {
            crossedBoundary = !_hasNightLightState || _state.isNightLightActive != *nightLight;
            _state.isNightLightActive = *nightLight;
        }
        // A recovered sample must not mistake an older, unobserved transition
        // for one that happened after this new manual choice.
        _hasNightLightState = nightLight.has_value();
    }
    else if (config.scheduleMode != ScheduleMode::Off)
    {
        crossedBoundary = HasCrossedScheduleBoundaryLocked(now);
    }
    if (unknownNightLight)
    {
        _state.isManualOverride = true;
    }
    else if (crossedBoundary)
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
    RecordThemeObservationsLocked(snapshot);
    NotifyAppliedThemeLocked(config, snapshot);
    return EvaluateAndApplyIfNeededLocked(config, now, &snapshot);
}

void LightSwitchStateManager::OnNightLightChange()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    const bool hadSettingsSnapshot = _hasSettingsSnapshot;
    const auto previousConfig = _settingsSnapshot;
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return;
    const auto crossedBoundary = _settingsSnapshot.scheduleMode == ScheduleMode::FollowNightLight ?
                                     RefreshNightLightStateLocked(_settingsSnapshot) : std::optional<bool>(false);
    if (!crossedBoundary.has_value())
        return;
    const auto now = _dependencies.localTime();
    if (!*crossedBoundary && hadSettingsSnapshot && HasSameEffectiveLightSwitchSettings(previousConfig, _settingsSnapshot))
        DetectExternalThemeChangeLocked(_settingsSnapshot, now);
    EvaluateAndApplyIfNeededLocked(_settingsSnapshot, now);
}

void LightSwitchStateManager::SyncInitialThemeState()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    RecordThemeObservationsLocked(GetStatusSnapshotLocked(_settingsSnapshot));
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

LSTATUS LightSwitchStateManager::ApplyThemeLocked(bool light, const LightSwitchConfig& config, bool& changed, StatusSnapshot& snapshot, bool preserveUntouchedObservations)
{
    LSTATUS result = ERROR_SUCCESS;
    changed = false;
    bool attemptedSystemWrite = false;
    bool attemptedAppsWrite = false;
    for (const bool system : { true, false })
    {
        if (!(system ? config.changeSystem : config.changeApps))
            continue;
        bool actual = false;
        if (_dependencies.readTheme(system, actual) != ERROR_SUCCESS || actual != light)
        {
            // If readback fails, this write's result must not later look like
            // an external edit relative to the value observed before the write.
            (system ? _lastObservedSystemTheme : _lastObservedAppsTheme).reset();
            (system ? attemptedSystemWrite : attemptedAppsWrite) = true;
            const auto writeResult = _dependencies.writeTheme(system, light);
            if (writeResult != ERROR_SUCCESS && result == ERROR_SUCCESS)
                result = writeResult;
            changed = true;
        }
    }
    snapshot = GetStatusSnapshotLocked(config);
    if ((config.changeSystem && snapshot.systemLight != std::optional<bool>(light)) ||
        (config.changeApps && snapshot.appsLight != std::optional<bool>(light)))
    {
        if (result == ERROR_SUCCESS)
            result = ERROR_WRITE_FAULT;
    }
    // A failed command may have skipped a target already changed by an external
    // editor. Leave that observation pending, while recording our own write
    // attempts so their partial results do not become external overrides.
    const bool recordAllObservations = result == ERROR_SUCCESS || !preserveUntouchedObservations;
    if (recordAllObservations)
    {
        RecordThemeObservationsLocked(snapshot);
    }
    else
    {
        if (attemptedSystemWrite && snapshot.systemLight)
            _lastObservedSystemTheme = snapshot.systemLight;
        if (attemptedAppsWrite && snapshot.appsLight)
            _lastObservedAppsTheme = snapshot.appsLight;
    }
    return result;
}

LSTATUS LightSwitchStateManager::EvaluateAndApplyIfNeededLocked(const LightSwitchConfig& config, const SYSTEMTIME& now, StatusSnapshot* finalSnapshot)
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
    if (config.scheduleMode == ScheduleMode::FollowNightLight && !_hasNightLightState)
        return ERROR_READ_FAULT;
    const bool light = ScheduledThemeLocked(config, now);
    if (_pendingThemeNotification != std::optional<bool>(light))
        _pendingThemeNotification.reset();
    bool changed = false;
    StatusSnapshot snapshot;
    const auto result = ApplyThemeLocked(light, config, changed, snapshot);
    if (finalSnapshot)
        *finalSnapshot = snapshot;
    if (changed)
        _pendingThemeNotification = light;
    if (result == ERROR_SUCCESS && _pendingThemeNotification)
    {
        NotifyAppliedThemeLocked(config, snapshot);
        _pendingThemeNotification.reset();
    }
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
    const bool hadSettingsSnapshot = _hasSettingsSnapshot;
    const auto previousConfig = _settingsSnapshot;
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return CompleteCommandLocked(_settingsSnapshot, L"SETTINGS_READ_FAILED", error);
    const auto config = _settingsSnapshot;
    if (!config.changeSystem && !config.changeApps)
        return CompleteCommandLocked(config, L"NO_TARGETS", GET_RESOURCE_STRING(IDS_THEME_TARGETS_DISABLED));
    // A successful command confirms or supersedes a pending scheduled notification.
    // A failed write leaves the schedule responsible for reconciling its result.
    const auto previousSystemTheme = _lastObservedSystemTheme;
    const auto previousAppsTheme = _lastObservedAppsTheme;
    const bool unchangedSettings = hadSettingsSnapshot && HasSameEffectiveLightSwitchSettings(previousConfig, config);
    bool changed = false;
    StatusSnapshot snapshot;
    const auto result = ApplyThemeLocked(light, config, changed, snapshot, unchangedSettings);
    if (result != ERROR_SUCCESS)
    {
        // A failed verification can hide a write that took effect. Let a later
        // successful retry confirm it even when that retry needs no more writes.
        const bool unreadableTarget = (config.changeSystem && !snapshot.systemLight) || (config.changeApps && !snapshot.appsLight);
        const bool targetsMatch = (!config.changeSystem || snapshot.systemLight == std::optional<bool>(light)) &&
                                  (!config.changeApps || snapshot.appsLight == std::optional<bool>(light));
        if (changed && !_pendingThemeNotification && (unreadableTarget || targetsMatch))
            _pendingThemeNotification = light;
        // Handle a skipped external choice now so its override expires at the
        // next boundary, rather than dating it from a delayed recovery poll.
        if (unchangedSettings)
            DetectExternalThemeChangeLocked(config, _dependencies.localTime());
        return CompleteCommandLocked(std::move(snapshot), L"THEME_WRITE_FAILED", ThemeError(result));
    }

    const auto now = _dependencies.localTime();
    UpdateEffectiveTimesLocked(config, now);
    if (config.scheduleMode == ScheduleMode::FollowNightLight)
    {
        const auto nightLight = _dependencies.readNightLight();
        _hasNightLightState = nightLight.has_value();
        if (nightLight)
            _state.isNightLightActive = *nightLight;
    }
    _state.isManualOverride = config.scheduleMode != ScheduleMode::Off &&
                              ((config.scheduleMode == ScheduleMode::FollowNightLight && !_hasNightLightState) || light != ScheduledThemeLocked(config, now));
    RecordEvaluationTimeLocked(now);
    // A no-op set can confirm an external choice before the minute poll sees it.
    // Acknowledge that transition once, even though no registry write was needed.
    const bool observedChange = (config.changeSystem && previousSystemTheme && snapshot.systemLight != previousSystemTheme) ||
                                (config.changeApps && previousAppsTheme && snapshot.appsLight != previousAppsTheme);
    if (changed || observedChange || _pendingThemeNotification)
        NotifyAppliedThemeLocked(config, snapshot);
    _pendingThemeNotification.reset();
    return CompleteCommandLocked(std::move(snapshot));
}

ThemeCommandResult LightSwitchStateManager::ToggleTheme()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    std::wstring error;
    if (!LoadSettingsLocked(error))
        return CompleteCommandLocked(_settingsSnapshot, L"SETTINGS_READ_FAILED", error);
    const auto config = _settingsSnapshot;
    if (!config.changeSystem && !config.changeApps)
        return CompleteCommandLocked(config, L"NO_TARGETS", GET_RESOURCE_STRING(IDS_THEME_TARGETS_DISABLED));
    const auto before = GetStatusSnapshotLocked(config);
    if ((config.changeSystem && !before.systemLight) || (config.changeApps && !before.appsLight))
        return CompleteCommandLocked(before, L"THEME_READ_FAILED", GET_RESOURCE_STRING(IDS_THEME_TOGGLE_READ_FAILED));

    LSTATUS result = ERROR_SUCCESS;
    for (const bool system : { true, false })
    {
        if (!(system ? config.changeSystem : config.changeApps))
            continue;
        (system ? _lastObservedSystemTheme : _lastObservedAppsTheme).reset();
        const auto writeResult = _dependencies.writeTheme(system, !(system ? *before.systemLight : *before.appsLight));
        if (writeResult != ERROR_SUCCESS && result == ERROR_SUCCESS)
            result = writeResult;
    }
    auto after = GetStatusSnapshotLocked(config);
    RecordThemeObservationsLocked(after);
    const bool targetsMatch = (!config.changeSystem || after.systemLight == std::optional<bool>(!*before.systemLight)) &&
                              (!config.changeApps || after.appsLight == std::optional<bool>(!*before.appsLight));
    if (!targetsMatch)
    {
        if (result == ERROR_SUCCESS)
            result = ERROR_WRITE_FAULT;
    }
    if (result == ERROR_SUCCESS)
        result = OnManualOverrideLocked(config, _dependencies.localTime(), after);
    else if (!_pendingThemeNotification)
    {
        // As for an explicit set, a later successful command can confirm these
        // writes without changing the registry again. Preserve an earlier pending
        // scheduled notification until the scheduler or a successful command resolves it.
        const bool unreadableTarget = (config.changeSystem && !after.systemLight) || (config.changeApps && !after.appsLight);
        const bool observedChange = (config.changeSystem && after.systemLight && after.systemLight != before.systemLight) ||
                                    (config.changeApps && after.appsLight && after.appsLight != before.appsLight);
        if (unreadableTarget || observedChange)
        {
            // A partial toggle can leave initially mixed targets at one theme.
            // Reconcile that verified result, even when the other write failed.
            const auto theme = config.changeSystem ? after.systemLight : after.appsLight;
            _pendingThemeNotification = theme.value_or(config.changeSystem ? !*before.systemLight : !*before.appsLight);
        }
    }
    return result == ERROR_SUCCESS ? CompleteCommandLocked(std::move(after)) : CompleteCommandLocked(std::move(after), L"THEME_WRITE_FAILED", ThemeError(result));
}

void LightSwitchStateManager::DetectExternalThemeChangeLocked(const LightSwitchConfig& config, const SYSTEMTIME& now)
{
    if (config.scheduleMode == ScheduleMode::Off || _state.isManualOverride)
        return;
    UpdateEffectiveTimesLocked(config, now);
    const auto nightLight = config.scheduleMode == ScheduleMode::FollowNightLight ? _dependencies.readNightLight() : std::optional<bool>(_state.isNightLightActive);
    if (!nightLight)
        return;
    const bool scheduledLight = config.scheduleMode == ScheduleMode::FollowNightLight ? !*nightLight : ScheduledThemeLocked(config, now);
    const auto snapshot = GetStatusSnapshotLocked(config);
    // An unchanged Windows theme at a new scheduled boundary is not an external
    // edit. Require a known earlier observation: recovering access to an unreadable
    // theme alone is not evidence that an external editor changed it.
    const bool mismatch = (config.changeSystem && snapshot.systemLight && _lastObservedSystemTheme && *snapshot.systemLight != scheduledLight && snapshot.systemLight != _lastObservedSystemTheme) ||
                          (config.changeApps && snapshot.appsLight && _lastObservedAppsTheme && *snapshot.appsLight != scheduledLight && snapshot.appsLight != _lastObservedAppsTheme);
    if (mismatch)
    {
        Logger::info(L"[LightSwitchStateManager] External theme change detected.");
        _pendingThemeNotification.reset();
        _state.isManualOverride = true;
        if (config.scheduleMode == ScheduleMode::FollowNightLight)
        {
            _state.isNightLightActive = *nightLight;
            _hasNightLightState = true;
        }
        RecordThemeObservationsLocked(snapshot);
        RecordEvaluationTimeLocked(now);
        NotifyAppliedThemeLocked(config, snapshot);
    }
    else
    {
        // Matching the current plan is also an observation. Retaining an older
        // value here would invent an external edit at the next boundary.
        RecordThemeObservationsLocked(snapshot);
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
