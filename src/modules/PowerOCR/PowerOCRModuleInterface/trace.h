#pragma once
class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has PowerOCR enabled or disabled
    static void EnablePowerOCR(const bool enabled) noexcept;
};
