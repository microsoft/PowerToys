#include "pch.h"
#include "thumbnail_provider.h"

namespace PowerPreviewSettings
{
    // Function to enable the thumbnail provider in registry
    LONG ThumbnailProviderSettings::Enable()
    {
        // Add registry value to enable thumbnail provider.
        return this->m_registryWrapper->SetRegistryValue(HKEY_CLASSES_ROOT, thumbnail_provider_subkey, nullptr, REG_SZ, (LPBYTE)this->GetCLSID(), (DWORD)(wcslen(this->GetCLSID()) * sizeof(wchar_t)));
    }

    // Function to disable the thumbnail provider in registry
    LONG ThumbnailProviderSettings::Disable()
    {
        // Delete the registry key to disable thumbnail provider.
        return this->m_registryWrapper->DeleteRegistryValue(HKEY_CLASSES_ROOT, thumbnail_provider_subkey, nullptr);
    }

    // Function to check if the thumbnail provider is enabled in registry
    bool ThumbnailProviderSettings::CheckRegistryState()
    {
        DWORD dataType;
        DWORD byteCount = 255;
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

    // Function to retrieve the registry subkey
    LPCWSTR ThumbnailProviderSettings::GetSubkey()
    {
        return thumbnail_provider_subkey;
    }
}
