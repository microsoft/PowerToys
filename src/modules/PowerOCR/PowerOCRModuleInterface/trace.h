#pragma once
class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has MousePointerCrosshairs enabled or disabled
    static void EnablePowerOCR(const bool enabled) noexcept;
};
