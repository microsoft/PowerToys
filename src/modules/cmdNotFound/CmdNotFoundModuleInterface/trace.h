#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has CmdNotFound enabled or disabled
    static void EnableCmdNotFound(const bool enabled) noexcept;
};
