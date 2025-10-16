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

std::optional<std::wstring> GetMsiPackageInstalledPath(bool perUser);
std::wstring GetMsiPackagePath();
