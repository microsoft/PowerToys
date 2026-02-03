#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>

#include <optional>
#include <string>

// Implementations in MsiUtils.cpp
std::optional<std::wstring> GetMsiPackageInstalledPath(bool perUser);
std::wstring GetMsiPackagePath();
