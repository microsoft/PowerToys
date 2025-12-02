#include "pch.h"
#include "MsiUtils.h"

std::optional<std::wstring> GetMsiPackageInstalledPath(bool perUser)
{
    constexpr size_t guid_length = 39;
    wchar_t productId[guid_length];
    const std::wstring upgradeCode = perUser ? POWER_TOYS_UPGRADE_CODE_USER : POWER_TOYS_UPGRADE_CODE;
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(upgradeCode.c_str(), 0, 0, productId); !found)
    {
        return std::nullopt;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(productId); !installed)
    {
        return std::nullopt;
    }

    DWORD bufferSize = MAX_PATH;
    wchar_t buffer[MAX_PATH];
    if (ERROR_SUCCESS == MsiGetProductInfoW(productId, INSTALLPROPERTY_INSTALLLOCATION, buffer, &bufferSize) && bufferSize)
    {
        return buffer;
    }

    DWORD packagePathSize = 0;

    if (ERROR_SUCCESS != MsiGetProductInfoW(productId, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &packagePathSize))
    {
        return std::nullopt;
    }

    std::wstring packagePath(++packagePathSize, L'\0');

    if (ERROR_SUCCESS != MsiGetProductInfoW(productId, INSTALLPROPERTY_LOCALPACKAGE, packagePath.data(), &packagePathSize))
    {
        return std::nullopt;
    }
    packagePath.resize(size(packagePath) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    wchar_t path[MAX_PATH];
    DWORD pathSize = MAX_PATH;
    MsiGetComponentPathW(productId, POWERTOYS_EXE_COMPONENT, path, &pathSize);
    if (!pathSize)
    {
        return std::nullopt;
    }
    PathCchRemoveFileSpec(path, pathSize);
    return path;
}

std::wstring GetMsiPackagePath()
{
    std::wstring packagePath;
    wchar_t productString[39];
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, productString); !found)
    {
        return packagePath;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(productString); !installed)
    {
        return packagePath;
    }

    DWORD packagePathSize = 0;

    if (const bool hasPackagePath = ERROR_SUCCESS == MsiGetProductInfoW(productString, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &packagePathSize); !hasPackagePath)
    {
        return packagePath;
    }

    packagePath = std::wstring(++packagePathSize, L'\0');
    if (const bool gotPackagePath = ERROR_SUCCESS == MsiGetProductInfoW(productString, INSTALLPROPERTY_LOCALPACKAGE, packagePath.data(), &packagePathSize); !gotPackagePath)
    {
        packagePath.clear();
        return packagePath;
    }

    packagePath.resize(size(packagePath) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    return packagePath;
}
