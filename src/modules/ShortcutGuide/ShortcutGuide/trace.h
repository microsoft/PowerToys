#pragma once
#include "ShortcutGuideSettings.h"

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void SendGuideSession(const __int64 duration_ms, const wchar_t* close_type) noexcept;
    static void SendSettings(ShortcutGuideSettings settings) noexcept;
};
