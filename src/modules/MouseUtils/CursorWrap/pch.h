#pragma once

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <windows.h>

#include <atomic>
#include <thread>
#include <vector>

// Note: Common includes moved to individual source files due to include path issues
// #include <common/SettingsAPI/settings_helpers.h>
// #include <common/logger/logger.h>
// #include <common/utils/logger_helper.h>