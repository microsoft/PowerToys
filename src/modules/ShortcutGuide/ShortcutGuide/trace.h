#pragma once
#include "ShortcutGuideSettings.h"

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void HideGuide(const __int64 duration_ms) noexcept;
    static void SendSettings(ShortcutGuideSettings settings) noexcept;
};
