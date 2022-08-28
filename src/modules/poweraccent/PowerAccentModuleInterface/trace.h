#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has PowerAccent enabled or disabled
    static void EnablePowerAccent(const bool enabled) noexcept;
};
