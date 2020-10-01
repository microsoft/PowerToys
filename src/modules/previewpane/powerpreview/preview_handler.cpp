#include "pch.h"
#include "preview_handler.h"

namespace PowerPreviewSettings
{
    const LPCWSTR PreviewHandlerSettings::preview_handlers_subkey = L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers";

    // Function to enable the preview handler in registry
    LONG PreviewHandlerSettings::Enable()
    {
        // Add registry value to enable preview.
        return this->m_registryWrapper->SetRegistryValue(HKEY_LOCAL_MACHINE, preview_handlers_subkey, this->GetCLSID(), REG_SZ, (LPBYTE)this->GetRegistryValueData().c_str(), (DWORD)(this->GetRegistryValueData().length() * sizeof(wchar_t)));
    }

    // Function to disable the preview handler in registry
    LONG PreviewHandlerSettings::Disable()
    {
        // Delete the registry key to disable preview.
        return this->m_registryWrapper->DeleteRegistryValue(HKEY_LOCAL_MACHINE, preview_handlers_subkey, this->GetCLSID());
    }

    // Function to check if the preview handler is enabled in registry
    bool PreviewHandlerSettings::CheckRegistryState()
    {
        DWORD dataType;
        DWORD byteCount = 255;
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

    // Function to retrieve the registry subkey
    LPCWSTR PreviewHandlerSettings::GetSubkey()
    {
        return preview_handlers_subkey;
    }
}
