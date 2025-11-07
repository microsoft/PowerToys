#include "pch.h"
#include "LightSwitchStateManager.h"
#include <logger.h>
#include <LightSwitchUtils.h>
#include "ThemeScheduler.h"
#include <ThemeHelper.h>

void ApplyTheme(bool shouldBeLight);

// Constructor
LightSwitchStateManager::LightSwitchStateManager()
{
    Logger::info(L"[LightSwitchStateManager] Initialized");
}

// Called when settings.json changes
void LightSwitchStateManager::OnSettingsChanged()
{
    Logger::info(L"[LightSwitchStateManager] Settings changed event received");

    // If manual override was active, clear it so new settings take effect
    if (_state.isManualOverride)
    {
        Logger::info(L"[LightSwitchStateManager] Clearing manual override due to settings update.");
        _state.isManualOverride = false;
    }

    EvaluateAndApplyIfNeeded();
}


// Called once per minute
void LightSwitchStateManager::OnTick(int currentMinutes)
{
    Logger::debug(L"[LightSwitchStateManager] Tick received: {}", currentMinutes);
    EvaluateAndApplyIfNeeded();
}

// Called when manual override is triggered
void LightSwitchStateManager::OnManualOverride()
{
    Logger::info(L"[LightSwitchStateManager] Manual override triggered");
    _state.isManualOverride = !_state.isManualOverride;

    // When entering manual override, sync internal theme state to match the current system
    if (_state.isManualOverride)
    {
        bool systemLight = GetCurrentSystemTheme();
        _state.isLightThemeActive = systemLight;
        Logger::info(L"[LightSwitchStateManager] Synced internal theme state to current system theme ({})",
                     systemLight ? L"light" : L"dark");
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
        return !(latVal == 0 && lonVal == 0);
    }
    catch (...)
    {
        return false;
    }
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
        std::wstring wmsg(e.what(), e.what() + strlen(e.what()));
        Logger::error(L"[LightSwitchService] Exception during sun time update: {}", wmsg);
    }

    return { newLightTime, newDarkTime };
}

// Internal: decide what should happen now
void LightSwitchStateManager::EvaluateAndApplyIfNeeded()
{
    const auto& _currentSettings = LightSwitchSettings::settings();
    auto now = GetNowMinutes();

    // Early exit: OFF mode just pauses activity
    if (_currentSettings.scheduleMode == ScheduleMode::Off)
    {
        Logger::debug(L"[LightSwitchStateManager] Mode is OFF — pausing service logic.");
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
            Logger::info(L"[LightSwitchStateManager] Recalculating sun times (mode/day change).");
            auto [newLightTime, newDarkTime] = update_sun_times(_currentSettings);
            _state.lastEvaluatedDay = st.wDay;
            _state.effectiveLightMinutes = newLightTime + _currentSettings.sunrise_offset;
            _state.effectiveDarkMinutes = newDarkTime + _currentSettings.sunset_offset;
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
            Logger::info(L"[LightSwitchStateManager] Manual override cleared after crossing boundary.");
            _state.isManualOverride = false;
        }
        else
        {
            Logger::debug(L"[LightSwitchStateManager] Manual override active — skipping auto apply.");
            _state.lastTickMinutes = now;
            return;
        }
    }

    _state.lastAppliedMode = _currentSettings.scheduleMode;

    bool shouldBeLight = ShouldBeLight(now, _state.effectiveLightMinutes, _state.effectiveDarkMinutes);

    // Only apply theme if there's a change or no override active
    if (!_state.isManualOverride && _state.isLightThemeActive != shouldBeLight)
    {
        Logger::info(L"[LightSwitchStateManager] Applying {} theme", shouldBeLight ? L"light" : L"dark");
        ApplyTheme(shouldBeLight);
        _state.isLightThemeActive = shouldBeLight;
    }

    _state.lastTickMinutes = now;
}



