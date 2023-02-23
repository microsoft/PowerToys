#pragma once
#include <interface/powertoy_module_interface.h>

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has PastePlain enabled or disabled
    static void EnablePastePlain(const bool enabled) noexcept;

    // Log if the user has invoked PastePlain
    static void PastePlainInvoked() noexcept;

    // Log if a PastePlain invocation has succeeded
    static void Trace::PastePlainSuccess() noexcept;

    // Log if an error occurs in PastePlain
    static void Trace::PastePlainError(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;

    // Event to send settings telemetry.
    static void Trace::SettingsTelemetry(PowertoyModuleIface::Hotkey& hotkey) noexcept;
};
