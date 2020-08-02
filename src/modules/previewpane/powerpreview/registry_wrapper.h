#pragma once
#include "registry_wrapper_interface.h"

namespace PowerPreviewSettings
{
    class RegistryWrapper : public RegistryWrapperIface
    {
    public:
        virtual LONG SetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, CONST BYTE* data, DWORD cbData);
        virtual LONG DeleteRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName);
        virtual LONG GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData);
    };
}