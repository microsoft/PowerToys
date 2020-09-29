#pragma once
#include "settings.h"

namespace PowerPreviewSettings
{
    class ThumbnailProvider :
        public FileExplorerPreviewSettings
    {
    private:
        // Relative HKCR sub key path of thumbnail provider in registry
        LPCWSTR thumbnail_provider_subkey;

    public:
        ThumbnailProvider(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, RegistryWrapperIface* registryWrapper, LPCWSTR subkey) :
            FileExplorerPreviewSettings(toggleSettingEnabled, toggleSettingName, toggleSettingDescription, clsid, registryValueData, registryWrapper), thumbnail_provider_subkey(subkey)
        {
        }

        LONG Enable()
        {
            //// Add registry value to enable thumbnail provider.
            //return this->m_registryWrapper->SetRegistryValue(HKEY_LOCAL_MACHINE, svg_thumbnail_provider_subkey, this->GetCLSID(), REG_SZ, (LPBYTE)this->GetRegistryValueData().c_str(), (DWORD)(this->GetRegistryValueData().length() * sizeof(wchar_t)));
            return 0;
        }

        LONG Disable()
        {
            //// Delete the registry key to disable thumbnail provider.
            //return this->m_registryWrapper->DeleteRegistryValue(HKEY_LOCAL_MACHINE, svg_thumbnail_provider_subkey, this->GetCLSID());
            return 0;
        }
    };
}
