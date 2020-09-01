#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void SettingsChanged() noexcept;
    static void MicrophoneMuted() noexcept;
    static void CameraMuted() noexcept;
};
