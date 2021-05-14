#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void SettingsChanged(const int overlay_opacity, const std::wstring& theme) noexcept;
};
