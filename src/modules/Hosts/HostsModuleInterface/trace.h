#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has HostsFileEditor enabled or disabled
    static void EnableHostsFileEditor(const bool enabled) noexcept;

    // Log that the user tried to activate the editor
    static void ActivateEditor() noexcept;
};
