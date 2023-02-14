#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has Awake enabled or disabled
    static void EnableRegistryPreview(const bool enabled) noexcept;
};
