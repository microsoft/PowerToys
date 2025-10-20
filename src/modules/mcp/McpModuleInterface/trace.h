#pragma once

namespace Trace
{
    void RegisterProvider() noexcept;
    void UnregisterProvider() noexcept;
    void EnableMCP(bool enabled) noexcept;
}
