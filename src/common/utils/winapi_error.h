#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <optional>
#include <string>
#include <system_error>
#include <strsafe.h>

std::optional<std::wstring> get_last_error_message(DWORD dw);

std::wstring get_last_error_or_default(DWORD dw);

void show_last_error_message(const wchar_t* functionName, DWORD dw, const wchar_t* errorTitle);
