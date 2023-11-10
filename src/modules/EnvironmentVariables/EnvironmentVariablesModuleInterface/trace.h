#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has EnvironmentVariables enabled or disabled
    static void EnableEnvironmentVariables(const bool enabled) noexcept;

    // Log that the user tried to activate the editor
    static void ActivateEnvironmentVariables() noexcept;
};
