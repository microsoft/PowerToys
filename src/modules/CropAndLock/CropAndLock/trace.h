#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace
{
public:
    class CropAndLock : public telemetry::TraceBase
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void ActivateReparent() noexcept;
        static void ActivateThumbnail() noexcept;
        static void ActivateScreenshot() noexcept;
        static void CreateReparentWindow() noexcept;
        static void CreateThumbnailWindow() noexcept;
        static void CreateScreenshotWindow() noexcept;
    };
};
