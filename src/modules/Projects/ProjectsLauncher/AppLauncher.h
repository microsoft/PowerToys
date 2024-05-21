#pragma once

#include <Windows.h>

void Launch(const std::wstring& appPath, bool startMinimized, const std::wstring& commandLineArgs, const RECT& rect) noexcept;