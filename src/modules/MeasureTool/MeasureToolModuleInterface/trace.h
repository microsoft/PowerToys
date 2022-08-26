#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    static void EnableMeasureTool(const bool enabled) noexcept;

    static void BoundsToolActivated() noexcept;
    static void MeasureToolActivated() noexcept;
};
