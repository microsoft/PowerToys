#pragma once
#include "LightSwitchSettings.h"
#include <functional>
#include <optional>
#include <utility>

// Represents runtime-only information (not saved in settings.json).
struct LightSwitchState
{
    ScheduleMode lastAppliedMode = ScheduleMode::Off;
    bool isManualOverride = false;
    bool isSystemLightActive = false;
    bool isAppsLightActive = false;
    bool isNightLightActive = false;
    int lastEvaluatedDay = -1;
    int lastTickMinutes = -1;
    int effectiveLightMinutes = 0;
    int effectiveDarkMinutes = 0;
};

struct StatusSnapshot
{
    std::optional<bool> systemLight;
    std::optional<bool> appsLight;
    LightSwitchConfig config;
    bool manualOverride = false;
    bool configurationAvailable = false;
};

struct ThemeCommandResult
{
    bool success = false;
    std::wstring errorCode;
    std::wstring message;
    StatusSnapshot status;
};

// Test seams for the existing state manager's Windows and settings operations.
struct LightSwitchStateManagerDependencies
{
    std::function<bool(LightSwitchConfig&, std::wstring&)> loadSettings;
    std::function<LSTATUS(bool system, bool& light)> readTheme;
    std::function<LSTATUS(bool system, bool light)> writeTheme;
    std::function<bool()> readNightLight;
    std::function<SYSTEMTIME()> localTime;
    std::function<void(bool light)> notifyThemeChanged;
    std::function<std::pair<int, int>(const LightSwitchConfig&, const SYSTEMTIME&)> updateSunTimes;
};

class LightSwitchStateManager
{
public:
    LightSwitchStateManager();
    explicit LightSwitchStateManager(LightSwitchStateManagerDependencies dependencies);

    ThemeCommandResult OnSettingsChanged();
    void OnTick();
    void OnManualOverride();
    void OnNightLightChange();
    void SyncInitialThemeState();
    void DetectExternalThemeChange();

    LightSwitchState GetState() const;
    StatusSnapshot GetStatusSnapshot();
    ThemeCommandResult SetTheme(bool light);
    ThemeCommandResult ToggleTheme();

private:
    LightSwitchState _state;
    mutable std::mutex _stateMutex;
    LightSwitchStateManagerDependencies _dependencies;
    LightSwitchConfig _settingsSnapshot;
    bool _hasSettingsSnapshot = false;

    bool LoadSettingsLocked(std::wstring& error);
    StatusSnapshot GetStatusSnapshotLocked(const LightSwitchConfig& config);
    void SyncThemeStateLocked(const StatusSnapshot& snapshot);
    void UpdateEffectiveTimesLocked(const LightSwitchConfig& config, const SYSTEMTIME& now);
    bool ScheduledThemeLocked(const LightSwitchConfig& config, const SYSTEMTIME& now);
    LSTATUS ApplyThemeLocked(bool light, const LightSwitchConfig& config, bool& changed);
    LSTATUS EvaluateAndApplyIfNeededLocked(const LightSwitchConfig& config, const SYSTEMTIME& now);
    LSTATUS OnManualOverrideLocked(const LightSwitchConfig& config, const SYSTEMTIME& now);
    void NotifyAppliedThemeLocked(const LightSwitchConfig& config, const StatusSnapshot& snapshot);
    ThemeCommandResult CompleteCommandLocked(const LightSwitchConfig& config, const wchar_t* errorCode = L"", const std::wstring& message = L"");
    bool CoordinatesAreValid(const std::wstring& lat, const std::wstring& lon);
    void NotifyPowerDisplayThemeChanged(bool isLight);
};
