#pragma once

class RegistryWrapperIface
{
public:
    // Sets a registry value under the mentioned scope(HKCR, HKLM, etc).
    virtual LONG SetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, CONST BYTE* data, DWORD cbData) = 0;

    // Delete a registry value.
    virtual LONG DeleteRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName) = 0;

    // Reads a registry value.
    virtual LONG GetRegistryValue(HKEY keyScope, LPCWSTR subKey, LPCWSTR valueName, DWORD dwType, LPDWORD pdwType, PVOID pvData, LPDWORD pcbData) = 0;
};