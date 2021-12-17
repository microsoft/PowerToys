#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has MouseHighlighter enabled or disabled
    static void EnableMouseHighlighter(const bool enabled) noexcept;

    // Log that the user activated the module by starting a highlighting session
    static void StartHighlightingSession() noexcept;
};
