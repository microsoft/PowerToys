#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>
#include <pathcch.h>
#include <Msi.h>

#include <optional>
#include <string>

namespace // Strings in this namespace should not be localized
{
    const inline wchar_t POWER_TOYS_UPGRADE_CODE[] = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const inline wchar_t POWER_TOYS_UPGRADE_CODE_USER[] = L"{D8B559DB-4C98-487A-A33F-50A8EEE42726}";
    const inline wchar_t POWERTOYS_EXE_COMPONENT[] = L"{A2C66D91-3485-4D00-B04D-91844E6B345B}";
}

std::optional<std::wstring> GetMsiPackageInstalledPath(bool perUser)
{
    constexpr size_t guid_length = 39;
    wchar_t product_ID[guid_length];
    std::wstring upgradeCode = (perUser ? POWER_TOYS_UPGRADE_CODE_USER : POWER_TOYS_UPGRADE_CODE);
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(upgradeCode.c_str(), 0, 0, product_ID); !found)
    {
        return std::nullopt;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(product_ID); !installed)
    {
        return std::nullopt;
    }

    DWORD buf_size = MAX_PATH;
    wchar_t buf[MAX_PATH];
    if (ERROR_SUCCESS == MsiGetProductInfoW(product_ID, INSTALLPROPERTY_INSTALLLOCATION, buf, &buf_size) && buf_size)
    {
        return buf;
    }

    DWORD package_path_size = 0;

    if (ERROR_SUCCESS != MsiGetProductInfoW(product_ID, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size))
    {
        return std::nullopt;
    }
    std::wstring package_path(++package_path_size, L'\0');

    if (ERROR_SUCCESS != MsiGetProductInfoW(product_ID, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size))
    {
        return std::nullopt;
    }
    package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    wchar_t path[MAX_PATH];
    DWORD path_size = MAX_PATH;
    MsiGetComponentPathW(product_ID, POWERTOYS_EXE_COMPONENT, path, &path_size);
    if (!path_size)
    {
        return std::nullopt;
    }
    PathCchRemoveFileSpec(path, path_size);
    return path;
}

std::wstring GetMsiPackagePath()
{
    std::wstring package_path;
    wchar_t GUID_product_string[39];
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, GUID_product_string); !found)
    {
        return package_path;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(GUID_product_string); !installed)
    {
        return package_path;
    }

    DWORD package_path_size = 0;

    if (const bool has_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size); !has_package_path)
    {
        return package_path;
    }

    package_path = std::wstring(++package_path_size, L'\0');
    if (const bool got_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size); !got_package_path)
    {
        package_path = {};
        return package_path;
    }

    package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    return package_path;
}
