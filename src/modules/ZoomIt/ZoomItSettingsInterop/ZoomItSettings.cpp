#include "pch.h"
#include "ZoomItSettings.h"
#include "ZoomItSettings.g.cpp"
#include "../ZoomIt/ZoomItSettings.h"

namespace winrt::PowerToys::ZoomItSettingsInterop::implementation
{
    hstring ZoomItSettings::LoadSettingsJson()
    {
        return hstring{ L"" };
    }

    void ZoomItSettings::SaveSettingsJson(hstring json)
    {
    }
}
