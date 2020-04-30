#pragma once
#include "registry_wrapper_interface.h"

namespace PowerPreviewSettings
{
    class RegistryWrapper : public RegistryWrapperIface
    {
    public:
        LONG SetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, CONST BYTE* data, DWORD cbData) override;
        LONG DeleteRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName) override;
        LONG GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData) override;
    };
}