#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has MousePointerCrosshair enabled or disabled
    static void EnableMousePointerCrosshair(const bool enabled) noexcept;

    // Log that the user activated the module by having the crosshair be drawn
    static void StartDrawingCrosshair() noexcept;
};
