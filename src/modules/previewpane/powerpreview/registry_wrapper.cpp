#include "pch.h"
#include "registry_wrapper.h"

namespace PowerPreviewSettings
{
    LONG RegistryWrapper::SetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, CONST BYTE* data, DWORD cbData)
    {
        HKEY OpenResult;
        LONG err = RegOpenKeyExW(keyScope, subKey, 0, KEY_WRITE, &OpenResult);

        if (err == ERROR_SUCCESS)
        {
            err = RegSetValueExW(
                OpenResult,
                valueName,
                0, // This parameter is reserved and must be zero.
                dwType,
                data,
                cbData);
            RegCloseKey(OpenResult);
        }

        return err;
    }

    LONG RegistryWrapper::GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData)
    {
        HKEY OpenResult;
        LONG err = RegOpenKeyExW(keyScope, subKey, 0, KEY_READ, &OpenResult);

        if (err == ERROR_SUCCESS)
        {
            err = RegGetValueW(
                OpenResult,
                NULL,
                valueName,
                RRF_RT_ANY,
                pdwType,
                pvData,
                pcbData);
            RegCloseKey(OpenResult);
        }

        return err;
    }

    LONG RegistryWrapper::DeleteRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName)
    {
        HKEY OpenResult;
        LONG err = RegOpenKeyExW(keyScope, subKey, 0, KEY_WRITE, &OpenResult);

        if (err == ERROR_SUCCESS)
        {
            err = RegDeleteKeyValueW(
                OpenResult,
                NULL,
                valueName);
            RegCloseKey(OpenResult);
        }

        return err;
    }
}
