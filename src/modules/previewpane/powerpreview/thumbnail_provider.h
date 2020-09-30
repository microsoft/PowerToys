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
            // Add registry value to enable thumbnail provider.
            return this->m_registryWrapper->SetRegistryValue(HKEY_CLASSES_ROOT, thumbnail_provider_subkey, nullptr, REG_SZ, (LPBYTE)this->GetCLSID(), (DWORD)(wcslen(this->GetCLSID()) * sizeof(wchar_t)));
        }

        LONG Disable()
        {
            // Delete the registry key to disable thumbnail provider.
            return this->m_registryWrapper->DeleteRegistryValue(HKEY_CLASSES_ROOT, thumbnail_provider_subkey, this->GetCLSID());
        }

        bool CheckRegistryState()
        {
            DWORD dataType;
            DWORD byteCount;
            wchar_t regValue[255] = { 0 };

            LONG errorCode = this->m_registryWrapper->GetRegistryValue(HKEY_CLASSES_ROOT, thumbnail_provider_subkey, nullptr, &dataType, regValue, &byteCount);

            // Registry value was found
            if (errorCode == ERROR_SUCCESS)
            {
                // Check if the value type is string
                if (dataType == REG_SZ)
                {
                    // Check if the current registry value matches the expected value
                    if (wcscmp(regValue, this->GetCLSID()) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    };
}
