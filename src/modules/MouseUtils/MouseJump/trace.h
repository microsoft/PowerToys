#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    static void EnableJumpTool(const bool enabled) noexcept;

    static void InvokeJumpTool() noexcept;
};
