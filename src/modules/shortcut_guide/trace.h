#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void HideGuide(const __int64 duration_ms, std::vector<int>& key_pressed) noexcept;
    static void EnableShortcutGuide(const bool enabled) noexcept;
    static void SettingsChanged(const int press_delay_time, const int overlay_opacity, const std::wstring& theme) noexcept;
};
