#pragma once

#include "ZoomItSettings.g.h"

namespace winrt::PowerToys::ZoomItSettingsInterop::implementation
{
    struct ZoomItSettings : ZoomItSettingsT<ZoomItSettings>
    {
        ZoomItSettings() = default;
        static hstring LoadSettingsJson();
        static void SaveSettingsJson(hstring json);
    };
}

namespace winrt::PowerToys::ZoomItSettingsInterop::factory_implementation
{
    struct ZoomItSettings : ZoomItSettingsT<ZoomItSettings, implementation::ZoomItSettings>
    {
    };
}
