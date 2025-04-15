#include "pch.h"
#include "RegistryUtils.h"

#include <strsafe.h>

#include <common/utils/winapi_error.h>

namespace RegistryUtils
{
    namespace NonLocalizable
    {
        const wchar_t RegKeyPackageId[] = L"Extensions\\ContractId\\Windows.Protocol\\PackageId\\";
        const wchar_t RegKeyPackageActivatableClassId[] = L"\\ActivatableClassId";
        const wchar_t RegKeyPackageCustomProperties[] = L"\\CustomProperties";
        const wchar_t RegValueName[] = L"Name";
    }

    HKEY OpenRootRegKey(const wchar_t* key)
    {
        HKEY hKey{ nullptr };
        if (RegOpenKeyEx(HKEY_CLASSES_ROOT, key, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
        {
            return hKey;
        }

        return nullptr;
    }

    std::vector<std::wstring> GetUriProtocolNames(const std::wstring& packageFullPath)
    {
        std::vector<std::wstring> names{};

        std::wstring keyPath = std::wstring(NonLocalizable::RegKeyPackageId) + packageFullPath + std::wstring(NonLocalizable::RegKeyPackageActivatableClassId);
        HKEY key = OpenRootRegKey(keyPath.c_str());
        if (key != nullptr)
        {
            LSTATUS result;

            // iterate over all the subkeys to get the protocol names
            DWORD index = 0;
            wchar_t keyName[256];
            DWORD keyNameSize = sizeof(keyName) / sizeof(keyName[0]);
            FILETIME lastWriteTime;

            while ((result = RegEnumKeyEx(key, index, keyName, &keyNameSize, NULL, NULL, NULL, &lastWriteTime)) != ERROR_NO_MORE_ITEMS)
            {
                if (result == ERROR_SUCCESS)
                {
                    std::wstring subkeyPath = std::wstring(keyPath) + L"\\" + std::wstring(keyName, keyNameSize) + std::wstring(NonLocalizable::RegKeyPackageCustomProperties);
                    HKEY subkey = OpenRootRegKey(subkeyPath.c_str());
                    if (subkey != nullptr)
                    {
                        DWORD dataSize;
                        wchar_t value[256];
                        result = RegGetValueW(subkey, nullptr, NonLocalizable::RegValueName, RRF_RT_REG_SZ, nullptr, value, &dataSize);
                        if (result == ERROR_SUCCESS)
                        {
                            names.emplace_back(std::wstring(value, dataSize / sizeof(wchar_t) - 1));
                        }
                        else
                        {
                            Logger::error(L"Failed to query registry value. Error: {}", get_last_error_or_default(result));
                        }
                        
                        RegCloseKey(subkey);
                    }
                }
                else
                {
                    Logger::error(L"Failed to enumerate subkey. Error: {}", get_last_error_or_default(result));
                    break;
                }

                keyNameSize = sizeof(keyName) / sizeof(keyName[0]); // Reset the buffer size
                ++index;
            }

            // Close the registry key
            RegCloseKey(key);
        }

        return names;
    }
}
