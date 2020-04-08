#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void EnableWindowWalker(const bool enabled) noexcept;
};
