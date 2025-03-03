#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has ZoomIt enabled or disabled
    static void EnableCharacterMap(const bool enabled) noexcept;
    // Log that the user tried to activate the app
    static void ActivateEditor() noexcept;
};