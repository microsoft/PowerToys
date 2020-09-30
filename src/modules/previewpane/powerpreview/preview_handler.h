#pragma once
#include "settings.h"

namespace PowerPreviewSettings
{
    class PreviewHandler :
        public FileExplorerPreviewSettings
    {
    private:
        // Relative(HKLM/HKCU) sub key path of Preview Handlers list in registry.
        static const LPCWSTR preview_handlers_subkey;

    public:
        PreviewHandler(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, RegistryWrapperIface* registryWrapper) :
            FileExplorerPreviewSettings(toggleSettingEnabled, toggleSettingName, toggleSettingDescription, clsid, registryValueData, registryWrapper)
        {
        }

        LONG Enable()
        {
            // Add registry value to enable preview.
            return this->m_registryWrapper->SetRegistryValue(HKEY_LOCAL_MACHINE, preview_handlers_subkey, this->GetCLSID(), REG_SZ, (LPBYTE)this->GetRegistryValueData().c_str(), (DWORD)(this->GetRegistryValueData().length() * sizeof(wchar_t)));
        }

        LONG Disable()
        {
            // Delete the registry key to disable preview.
            return this->m_registryWrapper->DeleteRegistryValue(HKEY_LOCAL_MACHINE, preview_handlers_subkey, this->GetCLSID());
        }

        bool CheckRegistryState()
        {
            DWORD dataType;
            DWORD byteCount;
            wchar_t regValue[255] = { 0 };

            LONG errorCode = this->m_registryWrapper->GetRegistryValue(HKEY_LOCAL_MACHINE, preview_handlers_subkey, this->GetCLSID(), &dataType, regValue, &byteCount);

            // Registry value was found
            if (errorCode == ERROR_SUCCESS)
            {
                // Check if the value type is string
                if (dataType == REG_SZ)
                {
                    // Check if the current registry value matches the expected value
                    if (wcscmp(regValue, this->GetRegistryValueData().c_str()) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    };
}
