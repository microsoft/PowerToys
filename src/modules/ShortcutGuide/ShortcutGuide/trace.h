#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void HideGuide(const __int64 duration_ms, std::vector<int>& key_pressed) noexcept;
};
