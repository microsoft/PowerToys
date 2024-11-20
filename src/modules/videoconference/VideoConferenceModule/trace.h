#pragma once
#include "VideoConferenceModule.h"

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void EnableVideoConference(const bool enabled) noexcept;
    static void SettingsChanged(const struct VideoConferenceSettings &settings) noexcept;
    static void MicrophoneMuted() noexcept;
    static void CameraMuted() noexcept;
};
