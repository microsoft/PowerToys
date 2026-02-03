#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <optional>
#include <string>

// Implementation in exec.cpp
std::optional<std::string> exec_and_read_output(const std::wstring_view command, DWORD timeout_ms = 30000);
