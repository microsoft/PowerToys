// Configuration helper - mock for orchestration test
// Related to issue #45364

#pragma once

#include <string>
#include <optional>

namespace PowerToys {
namespace Config {

template<typename T>
inline T GetConfigValue(const std::wstring& key, T defaultValue) {
    return defaultValue;
}

inline bool IsFeatureEnabled(const std::wstring& featureName) {
    return true;
}

} // namespace Config
} // namespace PowerToys
