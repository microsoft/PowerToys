#pragma once
#include <interface/powertoy_module_interface.h>

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has AdvancedPaste enabled or disabled
    static void AdvancedPaste_Enable(const bool enabled) noexcept;

    // Log if the user has invoked AdvancedPaste
    static void AdvancedPaste_Invoked(std::wstring mode) noexcept;

    // Log if an error occurs in AdvancedPaste
    static void Trace::AdvancedPaste_Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;

    // Event to send settings telemetry.
    static void Trace::AdvancedPaste_SettingsTelemetry(const PowertoyModuleIface::Hotkey& pastePlainHotkey,
                                         const PowertoyModuleIface::Hotkey& advancedPasteUIHotkey,
                                         const PowertoyModuleIface::Hotkey& pasteMarkdownHotkey,
                                         const PowertoyModuleIface::Hotkey& pasteJsonHotkey,
                                         const bool preview_custom_format_output) noexcept;
};
