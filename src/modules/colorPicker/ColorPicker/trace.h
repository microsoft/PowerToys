#pragma once
class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if ColorPicker is enabled or disabled
    static void EnableColorPicker(const bool enabled) noexcept;

};
