#pragma once

#include <string>

namespace autostart {

bool IsEnabled();
bool SetEnabled(bool on);
std::wstring GetRegistryValueName();
std::wstring GetCurrentExePath();
bool ReconcileWithState(bool autoStart);
void SetRegistryKeyOverride(const std::wstring& subkey);

} // namespace autostart
