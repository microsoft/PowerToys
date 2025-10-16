#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

// disable warning 26471 - Don't use reinterpret_cast. A cast from void* can use static_cast
// disable warning 26492 - Don't use const_cast to cast away const
// disable warning 26493 - Don't use C-style casts
// Disable 26497 for winrt - This function function-name could be marked constexpr if compile-time evaluation is desired.
#pragma warning(push)
#pragma warning(disable : 26471 26492 26493 26497)
#include <wil/resource.h>
#pragma warning(pop)

#include <optional>
#include <string>

std::optional<std::string> exec_and_read_output(const std::wstring_view command, DWORD timeout_ms = 30000);
