#pragma once
#include "VideoConferenceModule.h"

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void SettingsChanged(const struct VideoConferenceSettings &settings) noexcept;
    static void MicrophoneMuted() noexcept;
    static void CameraMuted() noexcept;
};
