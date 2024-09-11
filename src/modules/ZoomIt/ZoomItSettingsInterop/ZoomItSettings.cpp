#include "pch.h"
#include "ZoomItSettings.h"
#include "ZoomItSettings.g.cpp"
#include "../ZoomIt/ZoomItSettings.h"
#include <common/SettingsAPI/settings_objects.h>

namespace winrt::PowerToys::ZoomItSettingsInterop::implementation
{
    CRegistry reg(_T("Software\\Sysinternals\\") APPNAME);

    hstring ZoomItSettings::LoadSettingsJson()
    {
        PowerToysSettings::PowerToyValues _settings(L"ZoomIt",L"ZoomIt");
        reg.ReadRegSettings(RegSettings);
        PREG_SETTING curSetting = RegSettings;
        while (curSetting->Valuename)
        {
            switch (curSetting->Type)
            {
            case SETTING_TYPE_DWORD:
                _settings.add_property<DWORD>(curSetting->Valuename, *static_cast<PDWORD>(curSetting->Setting));
                break;
            case SETTING_TYPE_BOOLEAN:
                _settings.add_property<BOOLEAN>(curSetting->Valuename, *static_cast<PBOOLEAN>(curSetting->Setting));
                break;
            case SETTING_TYPE_DOUBLE:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_STRING:
                _settings.add_property<std::wstring>(curSetting->Valuename, static_cast<PTCHAR>(curSetting->Setting));
                break;
            case SETTING_TYPE_DWORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_BINARY:
                // TODO: How to support this type.
                break;
            }
            curSetting++;
        }

        return _settings.get_raw_json().Stringify();
    }

    void ZoomItSettings::SaveSettingsJson(hstring json)
    {

    }
}
