#include "pch.h"
#include "LightSwitchStateManager.h"
#include <logger.h>
#include <LightSwitchUtils.h>
#include "ThemeScheduler.h"
#include <ThemeHelper.h>
#include <common/interop/shared_constants.h>

void ApplyTheme(bool shouldBeLight);

// Constructor
LightSwitchStateManager::LightSwitchStateManager()
{
    Logger::info(L"[LightSwitchStateManager] Initialized");
}

// Called when settings.json changes
void LightSwitchStateManager::OnSettingsChanged()
{
    std::lock_guard<std::mutex> lock(_stateMutex);

    // If manual override was active, clear it so new settings take effect
    if (_state.isManualOverride)
    {
        _state.isManualOverride = false;
    }

    EvaluateAndApplyIfNeeded();
}

// Called once per minute
void LightSwitchStateManager::OnTick()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    if (_state.lastAppliedMode != ScheduleMode::FollowNightLight)
    {
        EvaluateAndApplyIfNeeded();
    }
}

// Called when manual override is triggered (via hotkey)
void LightSwitchStateManager::OnManualOverride()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    Logger::info(L"[LightSwitchStateManager] Manual override triggered");
    _state.isManualOverride = !_state.isManualOverride;

    // When entering manual override, sync internal theme state to match the current system
    // The hotkey handler in ModuleInterface has already toggled the theme, so we read the new state
    if (_state.isManualOverride)
    {
        _state.isSystemLightActive = GetCurrentSystemTheme();
        _state.isAppsLightActive = GetCurrentAppsTheme();

        Logger::debug(L"[LightSwitchStateManager] Synced internal theme state to current system theme ({}) and apps theme ({}).",
                      (_state.isSystemLightActive ? L"light" : L"dark"),
                      (_state.isAppsLightActive ? L"light" : L"dark"));

        // Notify PowerDisplay about the theme change triggered by hotkey
        // The theme has already been applied by ModuleInterface, we just need to notify PowerDisplay
        NotifyPowerDisplay(_state.isSystemLightActive);
    }

    EvaluateAndApplyIfNeeded();
}

// Runs with the registry observer detects a change in Night Light settings.
void LightSwitchStateManager::OnNightLightChange()
{
    std::lock_guard<std::mutex> lock(_stateMutex);

    bool newNightLightState = IsNightLightEnabled();

    // In Follow Night Light mode, treat a Night Light toggle as a boundary
    if (_state.lastAppliedMode == ScheduleMode::FollowNightLight && _state.isManualOverride)
    {
        Logger::info(L"[LightSwitchStateManager] Night Light changed while manual override active; "
                     L"treating as a boundary and clearing manual override.");
        _state.isManualOverride = false;
    }

    if (newNightLightState != _state.isNightLightActive)
    {
        Logger::info(L"[LightSwitchStateManager] Night Light toggled to {}",
                     newNightLightState ? L"ON" : L"OFF");

        _state.isNightLightActive = newNightLightState;
    }
    else
    {
        Logger::debug(L"[LightSwitchStateManager] Night Light change event fired, but no actual change.");
    }

    EvaluateAndApplyIfNeeded();
}

// Helpers
bool LightSwitchStateManager::CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon)
{
    try
    {
        double latVal = std::stod(lat);
        double lonVal = std::stod(lon);
        return !(latVal == 0 && lonVal == 0) && (latVal >= -90.0 && latVal <= 90.0) && (lonVal >= -180.0 && lonVal <= 180.0);
    }
    catch (...)
    {
        return false;
    }
}

void LightSwitchStateManager::SyncInitialThemeState()
{
    std::lock_guard<std::mutex> lock(_stateMutex);
    _state.isSystemLightActive = GetCurrentSystemTheme();
    _state.isAppsLightActive = GetCurrentAppsTheme();
    _state.isNightLightActive = IsNightLightEnabled();
    Logger::debug(L"[LightSwitchStateManager] Synced initial state to current system theme ({})",
                  _state.isSystemLightActive ? L"light" : L"dark");
    Logger::debug(L"[LightSwitchStateManager] Synced initial state to current apps theme ({})",
                  _state.isAppsLightActive ? L"light" : L"dark");
    
    // This will ensure that the theme is applied according to current settings at startup
    EvaluateAndApplyIfNeeded();
}

static std::pair<int, int> update_sun_times(auto& settings)
{
    double latitude = std::stod(settings.latitude);
    double longitude = std::stod(settings.longitude);

    SYSTEMTIME st;
    GetLocalTime(&st);

    SunTimes newTimes = CalculateSunriseSunset(latitude, longitude, st.wYear, st.wMonth, st.wDay);

    int newLightTime = newTimes.sunriseHour * 60 + newTimes.sunriseMinute;
    int newDarkTime = newTimes.sunsetHour * 60 + newTimes.sunsetMinute;

    try
    {
        auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"LightSwitch");
        values.add_property(L"lightTime", newLightTime);
        values.add_property(L"darkTime", newDarkTime);
        values.save_to_settings_file();

        Logger::info(L"[LightSwitchService] Updated sun times and saved to config.");
    }
    catch (const std::exception& e)
    {
        std::string msg = e.what();
        std::wstring wmsg(msg.begin(), msg.end());
        Logger::error(L"[LightSwitchService] Exception during sun time update: {}", wmsg);
    }

    return { newLightTime, newDarkTime };
}

// Internal: decide what should happen now
void LightSwitchStateManager::EvaluateAndApplyIfNeeded()
{
    LightSwitchSettings::instance().LoadSettings();
    const auto& _currentSettings = LightSwitchSettings::settings();
    auto now = GetNowMinutes();

    // Early exit: OFF mode just pauses activity
    if (_currentSettings.scheduleMode == ScheduleMode::Off)
    {
        _state.lastTickMinutes = now;
        return;
    }

    bool coordsValid = CoordinatesAreValid(_currentSettings.latitude, _currentSettings.longitude);

    // Handle Sun Mode recalculation
    if (_currentSettings.scheduleMode == ScheduleMode::SunsetToSunrise && coordsValid)
    {
        SYSTEMTIME st;
        GetLocalTime(&st);
        bool newDay = (_state.lastEvaluatedDay != st.wDay);
        bool modeChangedToSun = (_state.lastAppliedMode != ScheduleMode::SunsetToSunrise &&
                                 _currentSettings.scheduleMode == ScheduleMode::SunsetToSunrise);

        if (newDay || modeChangedToSun)
        {
            auto [newLightTime, newDarkTime] = update_sun_times(_currentSettings);
            _state.lastEvaluatedDay = st.wDay;
            _state.effectiveLightMinutes = newLightTime + _currentSettings.sunrise_offset;
            _state.effectiveDarkMinutes = newDarkTime + _currentSettings.sunset_offset;
        }
        else
        {
            _state.effectiveLightMinutes = _currentSettings.lightTime + _currentSettings.sunrise_offset;
            _state.effectiveDarkMinutes = _currentSettings.darkTime + _currentSettings.sunset_offset;
        }
    }
    else if (_currentSettings.scheduleMode == ScheduleMode::FixedHours)
    {
        _state.effectiveLightMinutes = _currentSettings.lightTime;
        _state.effectiveDarkMinutes = _currentSettings.darkTime;
    }

    // Handle manual override logic
    if (_state.isManualOverride)
    {
        bool crossedBoundary = false;
        if (_state.lastTickMinutes != -1)
        {
            int prev = _state.lastTickMinutes;

            // Handle midnight wraparound safely
            if (now < prev)
            {
                crossedBoundary =
                    (prev <= _state.effectiveLightMinutes || now >= _state.effectiveLightMinutes) ||
                    (prev <= _state.effectiveDarkMinutes || now >= _state.effectiveDarkMinutes);
            }
            else
            {
                crossedBoundary =
                    (prev < _state.effectiveLightMinutes && now >= _state.effectiveLightMinutes) ||
                    (prev < _state.effectiveDarkMinutes && now >= _state.effectiveDarkMinutes);
            }
        }

        if (crossedBoundary)
        {
            _state.isManualOverride = false;
        }
        else
        {
            _state.lastTickMinutes = now;
            return;
        }
    }

    _state.lastAppliedMode = _currentSettings.scheduleMode;

    bool shouldBeLight = false;
    if (_currentSettings.scheduleMode == ScheduleMode::FollowNightLight)
    {
        shouldBeLight = !_state.isNightLightActive;
    }
    else
    {
        shouldBeLight = ShouldBeLight(now, _state.effectiveLightMinutes, _state.effectiveDarkMinutes);
    }

    bool appsNeedsToChange = _currentSettings.changeApps && (_state.isAppsLightActive != shouldBeLight);
    bool systemNeedsToChange = _currentSettings.changeSystem && (_state.isSystemLightActive != shouldBeLight);

    /* Logger::debug(
        L"[LightSwitchStateManager] now = {:02d}:{:02d}, light boundary = {:02d}:{:02d} ({}), dark boundary = {:02d}:{:02d} ({})",
        now / 60,
        now % 60,
        _state.effectiveLightMinutes / 60,
        _state.effectiveLightMinutes % 60,
        _state.effectiveLightMinutes,
        _state.effectiveDarkMinutes / 60,
        _state.effectiveDarkMinutes % 60,
        _state.effectiveDarkMinutes); */

    /* Logger::debug("should be light = {}, apps needs change = {}, system needs change = {}",
                  shouldBeLight ? "true" : "false",
                  appsNeedsToChange ? "true" : "false",
                  systemNeedsToChange ? "true" : "false"); */

    // Only apply theme if there's a change or no override active
    if (!_state.isManualOverride && (appsNeedsToChange || systemNeedsToChange))
    {
        Logger::info(L"[LightSwitchStateManager] Applying {} theme", shouldBeLight ? L"light" : L"dark");
        ApplyTheme(shouldBeLight);

        _state.isSystemLightActive = GetCurrentSystemTheme();
        _state.isAppsLightActive = GetCurrentAppsTheme();

        // Notify PowerDisplay to apply display profile if configured
        NotifyPowerDisplay(shouldBeLight);
    }

    _state.lastTickMinutes = now;
}

// Notify PowerDisplay module about theme change to apply display profiles
void LightSwitchStateManager::NotifyPowerDisplay(bool isLight)
{
    const auto& settings = LightSwitchSettings::settings();

    // Check if any profile is enabled and configured
    bool shouldNotify = false;

    if (isLight && settings.enableLightModeProfile && !settings.lightModeProfile.empty())
    {
        shouldNotify = true;
    }
    else if (!isLight && settings.enableDarkModeProfile && !settings.darkModeProfile.empty())
    {
        shouldNotify = true;
    }

    if (!shouldNotify)
    {
        return;
    }

    try
    {
        // Signal PowerDisplay with the specific theme event
        // Using separate events for light/dark eliminates race conditions where PowerDisplay
        // might read the registry before LightSwitch has finished updating it
        const wchar_t* eventName = isLight
            ? CommonSharedConstants::LIGHT_SWITCH_LIGHT_THEME_EVENT
            : CommonSharedConstants::LIGHT_SWITCH_DARK_THEME_EVENT;

        Logger::info(L"[LightSwitchStateManager] Notifying PowerDisplay about theme change (isLight: {})", isLight);

        HANDLE hThemeEvent = CreateEventW(nullptr, FALSE, FALSE, eventName);
        if (hThemeEvent)
        {
            SetEvent(hThemeEvent);
            CloseHandle(hThemeEvent);
            Logger::info(L"[LightSwitchStateManager] Theme event signaled to PowerDisplay: {}", eventName);
        }
        else
        {
            Logger::warn(L"[LightSwitchStateManager] Failed to create theme event (error: {})", GetLastError());
        }
    }
    catch (...)
    {
        Logger::error(L"[LightSwitchStateManager] Failed to notify PowerDisplay");
    }
}
