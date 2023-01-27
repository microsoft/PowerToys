#pragma once
class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has PastePlain enabled or disabled
    static void EnablePastePlain(const bool enabled) noexcept;
};
