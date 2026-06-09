// Config.h
//
// User-editable settings loaded from config.json. Distinct from state.json
// (which the app owns and rewrites): config.json is written once with annotated
// defaults if missing, and thereafter only ever read — never overwritten — so a
// user's hand edits are preserved. Loaded once at startup.

#pragma once

#include "Constants.h"

#include <string>

namespace desktopgrass::config {

// Valid ranges and defaults for the exposed knobs. Defaults reproduce the app's
// historical behavior exactly.
constexpr int    kConfigVersion        = 1;
constexpr int    kTargetFpsDefault      = 24;
constexpr int    kTargetFpsMin          = 5;
constexpr int    kTargetFpsMax          = 144;
constexpr double kBladeDensityDefault   = DEFAULT_DENSITY; // 2.53125
constexpr double kBladeDensityMin       = 0.2;
constexpr double kBladeDensityMax       = 5.0;
constexpr double kSwaySpeedDefault      = 1.0;
constexpr double kSwaySpeedMin          = 0.0;
constexpr double kSwaySpeedMax          = 3.0;
constexpr double kSwayAmplitudeDefault  = 1.0;
constexpr double kSwayAmplitudeMin      = 0.0;
constexpr double kSwayAmplitudeMax      = 3.0;

struct Config {
    int    version       = kConfigVersion;
    int    targetFps     = kTargetFpsDefault;
    double bladeDensity  = kBladeDensityDefault;
    double swaySpeed     = kSwaySpeedDefault;
    double swayAmplitude = kSwayAmplitudeDefault;
};

// Loads config.json from the default location, creating an annotated default
// file if it is missing. Returns clamped, validated values; on any error falls
// back to defaults without overwriting an existing file. Always succeeds.
Config LoadConfig();

// Path overload for tests: reads/creates the config at the given path.
Config LoadConfig(const std::wstring& path);

std::wstring GetConfigFilePath();

} // namespace desktopgrass::config
