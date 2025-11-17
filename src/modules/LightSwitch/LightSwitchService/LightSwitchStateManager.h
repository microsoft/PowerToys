#pragma once
#include "LightSwitchSettings.h"
#include <optional>

// Represents runtime-only information (not saved in settings.json)
struct LightSwitchState
{
    ScheduleMode lastAppliedMode = ScheduleMode::Off;
    bool isManualOverride = false;
    bool isSystemLightActive = false;
    bool isAppsLightActive = false;
    int lastEvaluatedDay = -1;
    int lastTickMinutes = -1;

    // Derived, runtime-resolved times
    int effectiveLightMinutes = 0; // the boundary we actually act on
    int effectiveDarkMinutes = 0; // includes offsets if needed
};

// The controller that reacts to settings changes, time ticks, and manual overrides.
class LightSwitchStateManager
{
public:
    LightSwitchStateManager();

    // Called when settings.json changes or stabilizes.
    void OnSettingsChanged();

    // Called every minute (from service worker tick).
    void OnTick(int currentMinutes);

    // Called when manual override is toggled (via shortcut or system change).
    void OnManualOverride();

    // Initial sync at startup to align internal state with system theme
    void SyncInitialThemeState();

    // Accessor for current state (optional, for debugging or telemetry)
    const LightSwitchState& GetState() const { return _state; }

private:
    LightSwitchState _state;
    std::mutex _stateMutex;

    void EvaluateAndApplyIfNeeded();
    bool CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon);
};
